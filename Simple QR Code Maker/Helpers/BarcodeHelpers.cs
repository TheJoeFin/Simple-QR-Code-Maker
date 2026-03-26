using ImageMagick;
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
    /// <summary>
    /// Calculate the maximum safe logo size percentage based on QR code error correction level and version
    /// </summary>
    /// <param name="text">The text to encode in the QR code</param>
    /// <param name="correctionLevel">The error correction level</param>
    /// <returns>Maximum safe logo size as a percentage (0-100)</returns>
    // public static int GetMaxLogoSizePercentage(string text, ErrorCorrectionLevel correctionLevel)
    public static int GetMaxLogoSizePercentage(ErrorCorrectionLevel correctionLevel)
    {
        // Error correction capacity by level (percentage of code that can be damaged and still readable)
        // These are the theoretical maximum recovery percentages
        return correctionLevel.ToString() switch
        {
            "L" => 20,  // Low: ~7%
            "M" => 23,  // Medium: ~15%
            "Q" => 35,  // Quartile: ~25%
            "H" => 40,  // High: ~30%
            _ => 20
        };
    }

    /// <summary>
    /// Rasterizes SVG content to a System.Drawing.Bitmap at the specified dimensions.
    /// Uses Magick.NET; the SVG is scaled to fit preserving aspect ratio.
    /// </summary>
    public static Bitmap RasterizeSvgToBitmap(string svgContent, int width, int height)
    {
        MagickReadSettings settings = new()
        {
            Format = MagickFormat.Svg,
            Width = (uint)width,
            Height = (uint)height,
            BackgroundColor = MagickColors.Transparent,
        };
        byte[] svgBytes = System.Text.Encoding.UTF8.GetBytes(svgContent);
        using MagickImage image = new(svgBytes, settings);
        image.Format = MagickFormat.Png32;
        using MemoryStream ms = new();
        image.Write(ms);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    public static WriteableBitmap GetQrCodeBitmapFromText(string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0, double logoPaddingPixels = 8.0)
    {
        // Always pass fully opaque colors to ZXing — if the background color has A=0, ZXing fills
        // nothing (transparent brush = SourceOver no-op) and the bitmap initializes to black, making
        // foreground and background pixels indistinguishable. ApplyAlphaToQrBitmap handles alpha.
        BitmapRenderer bitmapRenderer = new()
        {
            Foreground = System.Drawing.Color.FromArgb(255, foreground.R, foreground.G, foreground.B),
            Background = System.Drawing.Color.FromArgb(255, background.R, background.G, background.B)
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

        // ZXing's BitmapRenderer uses Format32bppRgb (no alpha). If either color has
        // transparency, post-process the bitmap to apply the correct alpha channel.
        using Bitmap rawBitmap = barcodeWriter.Write(text);
        bool needsAlpha = foreground.A < 255 || background.A < 255;
        Bitmap bitmap = needsAlpha ? ApplyAlphaToQrBitmap(rawBitmap, foreground, background) : rawBitmap;

        try
        {
            // If a logo is provided, overlay it on the center of the QR code
            if (logoImage != null)
            {
                // Get the QR code details to calculate module size
                QRCode qrCode = ZXing.QrCode.Internal.Encoder.encode(text, correctionLevel);
                int moduleCount = qrCode.Version.DimensionForVersion;
                OverlayLogoOnQrCode(bitmap, logoImage, logoSizePercentage, moduleCount, encodingOptions.Margin, logoPaddingPixels, background);
            }

            using MemoryStream ms = new();
            bitmap.Save(ms, ImageFormat.Png);
            WriteableBitmap bitmapImage = new(encodingOptions.Width, encodingOptions.Height);
            ms.Position = 0;
            bitmapImage.SetSource(ms.AsRandomAccessStream());

            return bitmapImage;
        }
        finally
        {
            if (needsAlpha) bitmap.Dispose();
        }
    }

    /// <summary>
    /// Converts a ZXing-generated bitmap (Format32bppRgb, no alpha) to a Format32bppArgb
    /// bitmap with the correct alpha channel applied to foreground and background pixels.
    /// </summary>
    private static Bitmap ApplyAlphaToQrBitmap(Bitmap source, System.Drawing.Color foreground, System.Drawing.Color background)
    {
        // Clone to Format32bppArgb first: Format32bppRgb stores the alpha byte as 0 in memory,
        // so SetRemapTable would fail to match OldColor.A=255. The clone conversion sets alpha=255
        // for every pixel (all Format32bppRgb pixels are fully opaque by definition).
        using Bitmap argbSource = source.Clone(
            new System.Drawing.Rectangle(0, 0, source.Width, source.Height),
            PixelFormat.Format32bppArgb);

        Bitmap result = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(result);
        g.Clear(System.Drawing.Color.Transparent);

        // Remap the opaque source colors to the user's alpha-aware colors
        ColorMap[] colorMaps =
        [
            new ColorMap
            {
                OldColor = System.Drawing.Color.FromArgb(255, background.R, background.G, background.B),
                NewColor = background
            },
            new ColorMap
            {
                OldColor = System.Drawing.Color.FromArgb(255, foreground.R, foreground.G, foreground.B),
                NewColor = foreground
            }
        ];

        using ImageAttributes attributes = new();
        attributes.SetRemapTable(colorMaps);

        g.DrawImage(argbSource,
            new System.Drawing.Rectangle(0, 0, result.Width, result.Height),
            0, 0, argbSource.Width, argbSource.Height,
            GraphicsUnit.Pixel, attributes);

        return result;
    }

    private static void OverlayLogoOnQrCode(Bitmap qrCodeBitmap, Bitmap logo, double sizePercentage, int moduleCount, int margin, double logoPaddingPixels, System.Drawing.Color backgroundColor)
    {
        // Calculate the pixel size of each QR code module
        // The total size includes the margin on both sides
        int totalModules = moduleCount + (margin * 2);
        double modulePixelSize = (double)qrCodeBitmap.Width / totalModules;

        // Calculate the punchout space size based on the size percentage
        // This is the area that will be covered (blocking QR code modules)
        int punchoutSizePixels = (int)(Math.Min(qrCodeBitmap.Width, qrCodeBitmap.Height) * (sizePercentage / 100.0));

        // Round the punchout size to the nearest module boundary
        int punchoutSizeModules = (int)Math.Round(punchoutSizePixels / modulePixelSize);
        // Ensure it's at least 1 module and odd number for better centering
        if (punchoutSizeModules < 1) punchoutSizeModules = 1;
        if (punchoutSizeModules % 2 == 0) punchoutSizeModules++; // Make it odd for symmetry

        // Convert back to pixels, aligned to module boundaries
        int punchoutSize = (int)(punchoutSizeModules * modulePixelSize);

        // Calculate the position to center the punchout area
        int punchoutX = (qrCodeBitmap.Width - punchoutSize) / 2;
        int punchoutY = (qrCodeBitmap.Height - punchoutSize) / 2;

        // Convert padding to actual pixels
        // Positive padding = logo smaller than punchout (adds white space)
        // Negative padding = logo larger than punchout (logo extends beyond white background)
        int paddingPixels = (int)Math.Round(logoPaddingPixels);

        // Calculate the actual logo display size
        // Logo fills the punchout area minus the padding on all sides
        int logoDisplayWidth = Math.Max(1, punchoutSize - (Math.Abs(paddingPixels) * 2));
        int logoDisplayHeight = Math.Max(1, punchoutSize - (Math.Abs(paddingPixels) * 2));

        // If padding is negative, logo is larger than punchout
        if (paddingPixels < 0)
        {
            logoDisplayWidth = punchoutSize + (Math.Abs(paddingPixels) * 2);
            logoDisplayHeight = punchoutSize + (Math.Abs(paddingPixels) * 2);
        }

        // Calculate logo dimensions preserving aspect ratio
        float aspectRatio = (float)logo.Width / logo.Height;
        int logoWidth, logoHeight;

        if (aspectRatio > 1) // Wider than tall
        {
            logoWidth = logoDisplayWidth;
            logoHeight = (int)(logoDisplayWidth / aspectRatio);
        }
        else // Taller than wide or square
        {
            logoHeight = logoDisplayHeight;
            logoWidth = (int)(logoDisplayHeight * aspectRatio);
        }

        // Center the logo within the punchout area (or offset if larger)
        int logoX = punchoutX + (punchoutSize - logoWidth) / 2;
        int logoY = punchoutY + (punchoutSize - logoHeight) / 2;

        using Graphics g = Graphics.FromImage(qrCodeBitmap);
        // Set high quality rendering
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

        // Draw the punchout background with the same color as the QR code background.
        // Use SourceCopy so a transparent background color actually clears those pixels
        // rather than being ignored by the default SourceOver compositing.
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        using SolidBrush backgroundBrush = new(backgroundColor);
        g.FillRectangle(backgroundBrush, punchoutX, punchoutY, punchoutSize, punchoutSize);

        // Reset to SourceOver so the logo composites correctly over the punchout
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

        // Draw the logo scaled to fit within or extend beyond the punchout
        g.DrawImage(logo, logoX, logoY, logoWidth, logoHeight);
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

    public static SvgImage GetSvgQrCodeForText(string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0, double logoPaddingPixels = 8.0, string? logoSvgContent = null)
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
        if (logoImage != null || logoSvgContent != null)
        {
            // Get the QR code details to calculate module size
            QRCode qrCode = ZXing.QrCode.Internal.Encoder.encode(text, correctionLevel);
            int moduleCount = qrCode.Version.DimensionForVersion;
            svg = EmbedLogoInSvg(svg, logoImage, logoSizePercentage, moduleCount, encodingOptions.Margin, logoPaddingPixels, background, logoSvgContent);
        }

        return svg;
    }

    private static SvgImage EmbedLogoInSvg(SvgImage svg, Bitmap? logo, double sizePercentage, int moduleCount, int margin, double logoPaddingPixels, System.Drawing.Color backgroundColor, string? logoSvgContent = null)
    {
        const int svgSize = 1024; // Should match the encoding options Width/Height

        // Calculate the pixel size of each QR code module
        int totalModules = moduleCount + (margin * 2);
        double modulePixelSize = (double)svgSize / totalModules;

        // Calculate the punchout space size based on the size percentage
        // This is the area that will be covered (blocking QR code modules)
        int punchoutSizePixels = (int)(svgSize * (sizePercentage / 100.0));

        // Round the punchout size to the nearest module boundary
        int punchoutSizeModules = (int)Math.Round(punchoutSizePixels / modulePixelSize);
        if (punchoutSizeModules < 1) punchoutSizeModules = 1;
        if (punchoutSizeModules % 2 == 0) punchoutSizeModules++;

        int punchoutSize = (int)(punchoutSizeModules * modulePixelSize);

        // Calculate the position to center the punchout area
        int punchoutX = (svgSize - punchoutSize) / 2;
        int punchoutY = (svgSize - punchoutSize) / 2;

        // Convert padding to actual pixels
        // Positive padding = logo smaller than punchout (adds white space)
        // Negative padding = logo larger than punchout (logo extends beyond white background)
        int paddingPixels = (int)Math.Round(logoPaddingPixels);

        // Calculate the actual logo display size
        // Logo fills the punchout area minus the padding on all sides
        int logoDisplayWidth = Math.Max(1, punchoutSize - (Math.Abs(paddingPixels) * 2));
        int logoDisplayHeight = Math.Max(1, punchoutSize - (Math.Abs(paddingPixels) * 2));

        // If padding is negative, logo is larger than punchout
        if (paddingPixels < 0)
        {
            logoDisplayWidth = punchoutSize + (Math.Abs(paddingPixels) * 2);
            logoDisplayHeight = punchoutSize + (Math.Abs(paddingPixels) * 2);
        }

        // Build the SVG logo element with punchout background and logo
        // Use rgba() when the background has any transparency so the punchout is correctly transparent
        string backgroundColorFill = backgroundColor.A < 255
            ? $"rgba({backgroundColor.R},{backgroundColor.G},{backgroundColor.B},{backgroundColor.A / 255.0:F3})"
            : $"rgb({backgroundColor.R},{backgroundColor.G},{backgroundColor.B})";

        string logoSvgElement;

        if (logoSvgContent != null)
        {
            // SVG logo: inline as a nested <svg> element so all viewers render it correctly
            int logoWidth = logoDisplayWidth;
            int logoHeight = logoDisplayHeight;
            int logoX = punchoutX + (punchoutSize - logoWidth) / 2;
            int logoY = punchoutY + (punchoutSize - logoHeight) / 2;

            string viewBox = ExtractSvgViewBox(logoSvgContent);
            string svgInner = ExtractSvgInnerContent(logoSvgContent);
            string viewBoxAttr = string.IsNullOrEmpty(viewBox) ? string.Empty : $@" viewBox=""{viewBox}""";

            logoSvgElement = $@"
  <!-- Logo punchout background -->
  <rect x=""{punchoutX}"" y=""{punchoutY}"" width=""{punchoutSize}"" height=""{punchoutSize}"" fill=""{backgroundColorFill}""/>
  <!-- Logo SVG (inlined) -->
  <svg x=""{logoX}"" y=""{logoY}"" width=""{logoWidth}"" height=""{logoHeight}""{viewBoxAttr} preserveAspectRatio=""xMidYMid meet"" overflow=""visible"">
    {svgInner}
  </svg>";
        }
        else
        {
            // Raster logo: calculate AR and embed as base64 PNG
            float aspectRatio = (float)logo!.Width / logo.Height;
            int logoWidth, logoHeight;

            if (aspectRatio > 1) // Wider than tall
            {
                logoWidth = logoDisplayWidth;
                logoHeight = (int)(logoDisplayWidth / aspectRatio);
            }
            else // Taller than wide or square
            {
                logoHeight = logoDisplayHeight;
                logoWidth = (int)(logoDisplayHeight * aspectRatio);
            }

            // Center the logo within the punchout area (or offset if larger)
            int logoX = punchoutX + (punchoutSize - logoWidth) / 2;
            int logoY = punchoutY + (punchoutSize - logoHeight) / 2;

            // Resize the logo to the display size before encoding to reduce SVG file size
            Bitmap resizedLogo = new(logoWidth, logoHeight);
            using (Graphics g = Graphics.FromImage(resizedLogo))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.DrawImage(logo, 0, 0, logoWidth, logoHeight);
            }

            string base64Logo;
            using (MemoryStream ms = new())
            {
                resizedLogo.Save(ms, ImageFormat.Png);
                byte[] imageBytes = ms.ToArray();
                base64Logo = Convert.ToBase64String(imageBytes);
            }
            resizedLogo.Dispose();

            logoSvgElement = $@"
  <!-- Logo punchout background -->
  <rect x=""{punchoutX}"" y=""{punchoutY}"" width=""{punchoutSize}"" height=""{punchoutSize}"" fill=""{backgroundColorFill}""/>
  <!-- Logo image -->
  <image x=""{logoX}"" y=""{logoY}"" width=""{logoWidth}"" height=""{logoHeight}"" href=""data:image/png;base64,{base64Logo}""/>";
        }

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
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = [BarcodeFormat.QR_CODE, BarcodeFormat.DATA_MATRIX, BarcodeFormat.AZTEC, BarcodeFormat.PDF_417],
                PureBarcode = false,
            }
        };

        Result[] results = barcodeReader.DecodeMultiple(bitmap);

        List<(string, Result)> strings = [];

        if (results == null || results.Length == 0)
            return strings;

        foreach (Result result in results)
            if (!string.IsNullOrWhiteSpace(result.Text))
                strings.Add((result.Text, result));

        return strings;
    }

    /// <summary>
    /// Extracts the viewBox value from an SVG string (e.g. "0 0 600 530").
    /// Falls back to constructing one from width/height attributes if no viewBox is present.
    /// </summary>
    private static string ExtractSvgViewBox(string svgContent)
    {
        // Prefer an explicit viewBox attribute
        System.Text.RegularExpressions.Match viewBoxMatch =
            System.Text.RegularExpressions.Regex.Match(
                svgContent,
                @"viewBox\s*=\s*[""']([^""']+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (viewBoxMatch.Success)
            return viewBoxMatch.Groups[1].Value;

        // Fall back to synthesising viewBox from width/height on the <svg> element
        System.Text.RegularExpressions.Match widthMatch =
            System.Text.RegularExpressions.Regex.Match(
                svgContent,
                @"<svg[^>]*\swidth\s*=\s*[""']([0-9.]+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        System.Text.RegularExpressions.Match heightMatch =
            System.Text.RegularExpressions.Regex.Match(
                svgContent,
                @"<svg[^>]*\sheight\s*=\s*[""']([0-9.]+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (widthMatch.Success && heightMatch.Success)
            return $"0 0 {widthMatch.Groups[1].Value} {heightMatch.Groups[1].Value}";

        return string.Empty;
    }

    /// <summary>
    /// Extracts the inner content of an SVG string — everything between the
    /// end of the opening &lt;svg&gt; tag and the last &lt;/svg&gt;.
    /// Correctly skips any leading XML declaration or DOCTYPE preamble.
    /// </summary>
    private static string ExtractSvgInnerContent(string svgContent)
    {
        // Locate the <svg element itself (skipping <?xml ...?> and other preamble)
        int svgTagStart = svgContent.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        if (svgTagStart < 0)
            return string.Empty;

        // Find the closing > of the opening <svg ...> tag
        int openTagEnd = svgContent.IndexOf('>', svgTagStart);
        if (openTagEnd < 0)
            return string.Empty;

        // Find the last </svg> (the one that closes the root element)
        int closeTagStart = svgContent.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
        if (closeTagStart < 0 || closeTagStart <= openTagEnd)
            return string.Empty;

        return svgContent[(openTagEnd + 1)..closeTagStart];
    }
}