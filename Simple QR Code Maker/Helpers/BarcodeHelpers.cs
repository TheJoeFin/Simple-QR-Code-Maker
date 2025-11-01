using Microsoft.UI.Xaml.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using Windows.Storage;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;
using ZXing.Rendering;
using ZXing.Windows.Compatibility;
using static ZXing.Rendering.SvgRenderer;

namespace Simple_QR_Code_Maker.Helpers;

public static class BarcodeHelpers
{
    private const int LOGO_PADDING = 8;
    
    public static WriteableBitmap GetQrCodeBitmapFromText(string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0)
    {
        BitmapRenderer bitmapRenderer = new()
        {
            Foreground = foreground,
            Background = background
        };

        BarcodeWriter barcodeWriter = new()
        {
            Format = BarcodeFormat.QR_CODE,
            Renderer = bitmapRenderer,
        };

        EncodingOptions encodingOptions = new()
        {
            Width = 1024,
            Height = 1024,
            Margin = 2,
        };
        encodingOptions.Hints.Add(EncodeHintType.ERROR_CORRECTION, correctionLevel);
        barcodeWriter.Options = encodingOptions;

        using Bitmap bitmap = barcodeWriter.Write(text);
        
        // If a logo is provided, overlay it on the center of the QR code
        if (logoImage != null)
        {
            OverlayLogoOnQrCode(bitmap, logoImage, logoSizePercentage);
        }
        
        using MemoryStream ms = new();
        bitmap.Save(ms, ImageFormat.Png);
        WriteableBitmap bitmapImage = new(encodingOptions.Width, encodingOptions.Height);
        ms.Position = 0;
        bitmapImage.SetSource(ms.AsRandomAccessStream());

        return bitmapImage;
    }

    private static void OverlayLogoOnQrCode(Bitmap qrCode, Bitmap logo, double sizePercentage = 20.0)
    {
        // Calculate the maximum size of the logo based on the size percentage
        int maxLogoSize = (int)(Math.Min(qrCode.Width, qrCode.Height) * (sizePercentage / 100.0));
        
        // Calculate logo dimensions preserving aspect ratio
        float aspectRatio = (float)logo.Width / logo.Height;
        int logoWidth, logoHeight;
        
        if (aspectRatio > 1) // Wider than tall
        {
            logoWidth = maxLogoSize;
            logoHeight = (int)(maxLogoSize / aspectRatio);
        }
        else // Taller than wide or square
        {
            logoHeight = maxLogoSize;
            logoWidth = (int)(maxLogoSize * aspectRatio);
        }
        
        // Calculate the position to center the logo
        int x = (qrCode.Width - logoWidth) / 2;
        int y = (qrCode.Height - logoHeight) / 2;
        
        // Calculate background rectangle size (fits the actual logo dimensions)
        int bgWidth = logoWidth + (LOGO_PADDING * 2);
        int bgHeight = logoHeight + (LOGO_PADDING * 2);
        int bgX = x - LOGO_PADDING;
        int bgY = y - LOGO_PADDING;

        using Graphics g = Graphics.FromImage(qrCode);
        // Set high quality rendering
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

        // Draw a white background rectangle behind the logo for better visibility
        using SolidBrush whiteBrush = new(System.Drawing.Color.White);
        g.FillRectangle(whiteBrush, bgX, bgY, bgWidth, bgHeight);

        // Draw the logo with preserved aspect ratio
        g.DrawImage(logo, x, y, logoWidth, logoHeight);
    }

    /// <summary>
    /// Calculate the smallest side of a QR code based on the distance between the camera and the QR code
    /// </summary>
    /// <param name="distance">Distance of camera from QR Code (in)</param>
    /// <param name="numberOfBlocks">Number of blocks in the QR Code (Version)</param>
    /// <returns>The smallest size (in) of a QR Code scanning at the given distance</returns>
    public static double SmallestCodeSide(double distance, int numberOfBlocks)
    {
        // TODO when margin or padding can be set in settings
        // account for padding on both sides
        int padding = 2 * 2;

        double blockSize = (distance + 2.721) / 1759.1;

        // check if the block size is too small for normal printer
        if (blockSize < 0.007)
            return 0;

        double codeSize = blockSize * (numberOfBlocks + padding);
        return codeSize;
    }

    public static double ContrastRatioLossFrac(double contrastRatio)
    {
        double x1 = 21;
        double y1 = 1;
        double x2 = 2.5;
        double y2 = 0.8;

        double slope = (y2 - y1) / (x2 - x1);
        double yIntercept = y1 - (slope * x1);

        return (slope * contrastRatio) + yIntercept;
    }

    public static string SmallestSideWithUnits(double distance, int numberOfBlocks, Windows.UI.Color foreground, Windows.UI.Color background)
    {
        bool isMetric = RegionInfo.CurrentRegion.IsMetric;
        double smallestSide = SmallestCodeSide(distance, numberOfBlocks);

        if (smallestSide == 0)
            return "Error at selected max distance.";

        double contrastRatio = ColorHelpers.GetContrastRatio(foreground, background);

        if (contrastRatio < 2.5)
            return "Color contrast too low";

        double fractionalLoss = ContrastRatioLossFrac(contrastRatio);

        smallestSide /= fractionalLoss;

        if (!isMetric)
            return $"{smallestSide:F2} x {smallestSide:F2} in";

        double smallestSideCm = smallestSide * 2.54;
        return $"{smallestSideCm:F2} x {smallestSideCm:F2} cm";
    }

    public static SvgImage GetSvgQrCodeForText(string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0)
    {
        SvgRenderer svgRenderer = new()
        {
            Foreground = new SvgRenderer.Color(foreground.A, foreground.R, foreground.G, foreground.B),
            Background = new SvgRenderer.Color(background.A, background.R, background.G, background.B),
        };

        BarcodeWriterSvg barcodeWriter = new()
        {
            Format = BarcodeFormat.QR_CODE,
            Renderer = svgRenderer
        };

        EncodingOptions encodingOptions = new()
        {
            Width = 1024,
            Height = 1024,
            Margin = 2,
        };
        encodingOptions.Hints.Add(EncodeHintType.ERROR_CORRECTION, correctionLevel);
        barcodeWriter.Options = encodingOptions;

        SvgImage svg = barcodeWriter.Write(text);

        // If a logo is provided, embed it in the SVG
        if (logoImage != null)
        {
            svg = EmbedLogoInSvg(svg, logoImage, logoSizePercentage);
        }

        return svg;
    }

    private static SvgImage EmbedLogoInSvg(SvgImage svg, Bitmap logo, double sizePercentage = 20.0)
    {
        // Calculate logo dimensions preserving aspect ratio
        const int svgSize = 1024; // Should match the encoding options Width/Height
        int maxLogoSize = (int)(svgSize * (sizePercentage / 100.0));
        
        // Calculate logo dimensions preserving aspect ratio
        float aspectRatio = (float)logo.Width / logo.Height;
        int logoWidth, logoHeight;
        
        if (aspectRatio > 1) // Wider than tall
        {
            logoWidth = maxLogoSize;
            logoHeight = (int)(maxLogoSize / aspectRatio);
        }
        else // Taller than wide or square
        {
            logoHeight = maxLogoSize;
            logoWidth = (int)(maxLogoSize * aspectRatio);
        }
        
        // Resize the logo before encoding to reduce SVG file size
        Bitmap resizedLogo = new(logoWidth, logoHeight);
        using (Graphics g = Graphics.FromImage(resizedLogo))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.DrawImage(logo, 0, 0, logoWidth, logoHeight);
        }
        
        // Convert resized logo to base64 for embedding
        string base64Logo;
        using (MemoryStream ms = new())
        {
            resizedLogo.Save(ms, ImageFormat.Png);
            byte[] imageBytes = ms.ToArray();
            base64Logo = Convert.ToBase64String(imageBytes);
        }
        resizedLogo.Dispose();
        
        // Calculate centered position
        int x = (svgSize - logoWidth) / 2;
        int y = (svgSize - logoHeight) / 2;
        
        // Calculate background rectangle size
        int bgWidth = logoWidth + (LOGO_PADDING * 2);
        int bgHeight = logoHeight + (LOGO_PADDING * 2);
        int bgX = x - LOGO_PADDING;
        int bgY = y - LOGO_PADDING;

        // Insert the logo into the SVG content
        string logoSvgElement = $@"
  <!-- Logo background -->
  <rect x=""{bgX}"" y=""{bgY}"" width=""{bgWidth}"" height=""{bgHeight}"" fill=""white""/>
  <!-- Logo image with preserved aspect ratio -->
  <image x=""{x}"" y=""{y}"" width=""{logoWidth}"" height=""{logoHeight}"" href=""data:image/png;base64,{base64Logo}""/>";

        // Find the closing </svg> tag and insert the logo before it
        string modifiedContent = svg.Content.Replace("</svg>", logoSvgElement + "\n</svg>");
        
        return new SvgImage(modifiedContent);
    }

    public static IEnumerable<(string, Result)> GetStringsFromImageFile(StorageFile storageFile)
    {
        Bitmap bitmap = new(storageFile.Path);

        return GetStringsFromBitmap(bitmap);
    }

    public static IEnumerable<(string, Result)> GetStringsFromBitmap(Bitmap bitmap)
    {
        BarcodeReader barcodeReader = new()
        {
            AutoRotate = true,
            Options =
            {
                TryHarder = true,
                TryInverted = true,
            }
        };

        Result[] results = barcodeReader.DecodeMultiple(bitmap);

        List<(string, Result)> strings = new();

        if (results == null || results.Length == 0)
            return strings;

        foreach (Result result in results)
            if (!string.IsNullOrWhiteSpace(result.Text))
                strings.Add((result.Text, result));

        return strings;
    }
}