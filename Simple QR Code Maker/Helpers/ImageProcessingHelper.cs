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

        double width = Math.Max(
            Distance(topLeft, topRight),
            Distance(bottomLeft, bottomRight));
        
        double height = Math.Max(
            Distance(topLeft, bottomLeft),
            Distance(topRight, bottomRight));

        var sourceCoordinates = new[]
        {
            topLeft.X, topLeft.Y,
            topRight.X, topRight.Y,
            bottomRight.X, bottomRight.Y,
            bottomLeft.X, bottomLeft.Y
        };

        var destinationCoordinates = new[]
        {
            0.0, 0.0,
            width, 0.0,
            width, height,
            0.0, height
        };

        var distortArgs = new double[16];
        Array.Copy(sourceCoordinates, 0, distortArgs, 0, 8);
        Array.Copy(destinationCoordinates, 0, distortArgs, 8, 8);
        result.Distort(DistortMethod.Perspective, distortArgs);

        if (borderPixels > 0)
        {
            result.BorderColor = MagickColors.White;
            result.Border((uint)borderPixels);
        }

        return result;
    }

    public static async Task<MagickImage> LoadImageFromStorageFile(StorageFile file)
    {
        using var stream = await file.OpenStreamForReadAsync();
        var image = new MagickImage(stream);
        return image;
    }

    public static MagickImage LoadImageFromBitmap(Bitmap bitmap)
    {
        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
        memoryStream.Position = 0;
        return new MagickImage(memoryStream);
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
