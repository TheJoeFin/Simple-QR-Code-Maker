using ImageMagick;
using System.Drawing;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Helpers;

public static class ImageProcessingHelper
{
    public static MagickImage ApplyGrayscale(MagickImage image)
    {
        var result = (MagickImage)image.Clone();
        result.Grayscale();
        return result;
    }

    public static MagickImage InvertColors(MagickImage image)
    {
        var result = (MagickImage)image.Clone();
        result.Negate();
        return result;
    }

    public static MagickImage AdjustContrast(MagickImage image, double contrastValue)
    {
        var result = (MagickImage)image.Clone();
        result.BrightnessContrast(new Percentage(0), new Percentage(contrastValue));
        return result;
    }

    public static MagickImage AdjustLevels(MagickImage image, double blackPoint, double whitePoint, double gamma = 1.0)
    {
        var result = (MagickImage)image.Clone();
        var blackPercentage = new Percentage(blackPoint);
        var whitePercentage = new Percentage(whitePoint);
        result.Level(blackPercentage, whitePercentage, gamma);
        return result;
    }

    public static MagickImage SetBlackPoint(MagickImage image, MagickColor blackPointColor)
    {
        var result = (MagickImage)image.Clone();
        
        double threshold = (blackPointColor.R + blackPointColor.G + blackPointColor.B) / (3.0 * 255.0);
        result.Level(new Percentage(threshold * 100), new Percentage(100), 1.0);
        
        return result;
    }

    public static MagickImage SetWhitePoint(MagickImage image, MagickColor whitePointColor)
    {
        var result = (MagickImage)image.Clone();
        
        double threshold = (whitePointColor.R + whitePointColor.G + whitePointColor.B) / (3.0 * 255.0);
        result.Level(new Percentage(0), new Percentage(threshold * 100), 1.0);
        
        return result;
    }

    public static MagickImage CorrectPerspectiveDistortion(
        MagickImage image,
        Point topLeft,
        Point topRight,
        Point bottomRight,
        Point bottomLeft,
        int borderPixels = 20)
    {
        var result = (MagickImage)image.Clone();

        // CRITICAL: Ensure EXIF orientation is applied before perspective correction
        // This prevents coordinate misalignment when the image has rotation metadata
        result.AutoOrient();

        System.Diagnostics.Debug.WriteLine($"=== Perspective Correction ===");
        System.Diagnostics.Debug.WriteLine($"Image dimensions (after orientation): {result.Width} x {result.Height}");
        System.Diagnostics.Debug.WriteLine($"Source corners:");
        System.Diagnostics.Debug.WriteLine($"  TopLeft: ({topLeft.X}, {topLeft.Y})");
        System.Diagnostics.Debug.WriteLine($"  TopRight: ({topRight.X}, {topRight.Y})");
        System.Diagnostics.Debug.WriteLine($"  BottomRight: ({bottomRight.X}, {bottomRight.Y})");
        System.Diagnostics.Debug.WriteLine($"  BottomLeft: ({bottomLeft.X}, {bottomLeft.Y})");

        // Validate corners are within image bounds
        if (!IsPointInBounds(topLeft, result.Width, result.Height) ||
            !IsPointInBounds(topRight, result.Width, result.Height) ||
            !IsPointInBounds(bottomRight, result.Width, result.Height) ||
            !IsPointInBounds(bottomLeft, result.Width, result.Height))
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: One or more corners are outside image bounds (0,0) to ({result.Width-1},{result.Height-1})");
            throw new ArgumentException($"One or more corners are outside the image boundaries. Image size: {result.Width}x{result.Height}. Please ensure all corners are within the image.");
        }

        // Check if all corners are the same (common bug from coordinate conversion issues)
        if (topLeft == topRight && topRight == bottomRight && bottomRight == bottomLeft)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: All four corners are identical at ({topLeft.X}, {topLeft.Y})");
            System.Diagnostics.Debug.WriteLine($"This suggests a coordinate conversion bug - check that display coordinates are properly converted to image pixel coordinates.");
            throw new ArgumentException($"All four corners are at the same location ({topLeft.X}, {topLeft.Y}). This indicates a coordinate conversion error. Please try selecting the corners again.");
        }

        // Validate that points form a valid quadrilateral
        if (!IsValidQuadrilateral(topLeft, topRight, bottomRight, bottomLeft))
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: Invalid quadrilateral - points are collinear or degenerate");
            throw new ArgumentException("The four corners do not form a valid quadrilateral. Please ensure the points are not collinear and form a proper shape.");
        }

        // Calculate the output dimensions based on the distances between corners
        double width = Math.Max(
            Distance(topLeft, topRight),
            Distance(bottomLeft, bottomRight));

        double height = Math.Max(
            Distance(topLeft, bottomLeft),
            Distance(topRight, bottomRight));

        System.Diagnostics.Debug.WriteLine($"Output size: {width:F2} x {height:F2}");

        // Ensure dimensions are valid
        if (width < 10 || height < 10)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: Output dimensions too small: {width:F2} x {height:F2}");
            throw new ArgumentException($"The selected area is too small ({width:F0}x{height:F0}). Please select a larger region.");
        }

        // ImageMagick Distort expects: source_x1, source_y1, dest_x1, dest_y1, source_x2, source_y2, dest_x2, dest_y2, ...
        var distortArgs = new double[]
        {
            topLeft.X, topLeft.Y, 0, 0,
            topRight.X, topRight.Y, width, 0,
            bottomRight.X, bottomRight.Y, width, height,
            bottomLeft.X, bottomLeft.Y, 0, height
        };

        System.Diagnostics.Debug.WriteLine($"Distort args: [{string.Join(", ", distortArgs.Select(d => d.ToString("F2")))}]");

        try
        {
            // Set the virtual canvas size to exactly match our desired output
            result.VirtualPixelMethod = VirtualPixelMethod.Transparent;
            result.Distort(DistortMethod.Perspective, distortArgs);

            // Explicitly crop to the exact dimensions we calculated
            // This removes any extra virtual canvas that might have been created
            uint cropWidth = (uint)Math.Ceiling(width);
            uint cropHeight = (uint)Math.Ceiling(height);
            result.Crop(new MagickGeometry(0, 0, cropWidth, cropHeight));
            result.ResetPage(); // Reset the page/canvas to the cropped size

            System.Diagnostics.Debug.WriteLine($"Cropped to exact size: {result.Width} x {result.Height}");

            if (borderPixels > 0)
            {
                result.BorderColor = MagickColors.White;
                result.Border((uint)borderPixels);
            }

            System.Diagnostics.Debug.WriteLine($"Final size with border: {result.Width} x {result.Height}");
        }
        catch (ImageMagick.MagickOptionErrorException ex) when (ex.Message.Contains("Unsolvable Matrix"))
        {
            System.Diagnostics.Debug.WriteLine($"ERROR: ImageMagick perspective transform failed - unsolvable matrix");
            System.Diagnostics.Debug.WriteLine($"This usually means the points are too close together or form an invalid shape");
            throw new InvalidOperationException("Cannot apply perspective correction: the selected corners form an invalid shape. Please ensure the corners outline a clear quadrilateral.", ex);
        }

        return result;
    }

    private static bool IsValidQuadrilateral(Point p1, Point p2, Point p3, Point p4)
    {
        // Check if any three points are collinear (which would make this degenerate)
        if (AreCollinear(p1, p2, p3) || AreCollinear(p2, p3, p4) || 
            AreCollinear(p3, p4, p1) || AreCollinear(p4, p1, p2))
        {
            return false;
        }

        // Check if points are too close together (minimum 5 pixel separation)
        if (Distance(p1, p2) < 5 || Distance(p2, p3) < 5 || 
            Distance(p3, p4) < 5 || Distance(p4, p1) < 5)
        {
            return false;
        }

        // Check for self-intersecting quadrilateral
        // A valid quadrilateral should have diagonals that intersect inside
        return true;
    }

    private static bool IsPointInBounds(Point point, uint imageWidth, uint imageHeight)
    {
        return point.X >= 0 && point.X < imageWidth && point.Y >= 0 && point.Y < imageHeight;
    }

    private static bool AreCollinear(Point p1, Point p2, Point p3)
    {
        // Calculate the cross product to check collinearity
        // If cross product is close to 0, points are collinear
        long crossProduct = (long)(p2.X - p1.X) * (p3.Y - p1.Y) - 
                           (long)(p2.Y - p1.Y) * (p3.X - p1.X);

        // Allow small tolerance for floating point errors (within 1 pixel area)
        return Math.Abs(crossProduct) < 1;
    }

    public static async Task<MagickImage> LoadImageFromStorageFile(StorageFile file)
    {
        using var stream = await file.OpenStreamForReadAsync();

        bool isSvg = string.Equals(file.FileType, ".svg", StringComparison.OrdinalIgnoreCase);
        MagickImage image;

        if (isSvg)
        {
            var settings = new MagickReadSettings
            {
                Format = MagickFormat.Svg,
                Density = new Density(300, DensityUnit.PixelsPerInch),
                BackgroundColor = MagickColors.White,
            };
            image = new MagickImage(stream, settings);
            image.Format = MagickFormat.Png;
        }
        else
        {
            image = new MagickImage(stream);
            image.AutoOrient();
        }

        System.Diagnostics.Debug.WriteLine($"Loaded image from {file.Name}:");
        System.Diagnostics.Debug.WriteLine($"  Final dimensions: {image.Width}x{image.Height}");

        return image;
    }

    public static MagickImage LoadImageFromBitmap(Bitmap bitmap)
    {
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
        memoryStream.Position = 0;
        var image = new MagickImage(memoryStream);

        // Apply EXIF orientation if present
        image.AutoOrient();

        System.Diagnostics.Debug.WriteLine($"Loaded image from Bitmap:");
        System.Diagnostics.Debug.WriteLine($"  Final dimensions: {image.Width}x{image.Height}");
        System.Diagnostics.Debug.WriteLine($"  Orientation applied: AutoOrient() called");

        return image;
    }

    public static Bitmap ConvertToBitmap(MagickImage image)
    {
        using var memoryStream = new MemoryStream();
        image.Write(memoryStream, MagickFormat.Png);
        memoryStream.Position = 0;
        return new Bitmap(memoryStream);
    }

    public static async Task<string> SaveToTemporaryFile(MagickImage image)
    {
        string cachePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{DateTimeOffset.Now.Ticks}.png");
        await image.WriteAsync(cachePath);
        return cachePath;
    }

    private static double Distance(Point p1, Point p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
