using ImageMagick;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.RegularExpressions;
using Windows.Storage;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;
using ZXing.Rendering;
using ZXing.Windows.Compatibility;
using static ZXing.Rendering.SvgRenderer;

namespace Simple_QR_Code_Maker.Helpers;

public static partial class BarcodeHelpers
{
    private const int MaxQrRenderSize = 1024;

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

    public static WriteableBitmap GetQrCodeBitmapFromText(string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0, double logoPaddingPixels = 8.0, double qrPaddingModules = 2.0, QrFramePreset framePreset = QrFramePreset.None, string? frameText = null)
    {
        using Bitmap bitmap = CreateQrCodeBitmap(
            text,
            correctionLevel,
            foreground,
            background,
            logoImage,
            logoSizePercentage,
            logoPaddingPixels,
            qrPaddingModules,
            framePreset,
            frameText,
            out _);
        using MemoryStream ms = new();
        bitmap.Save(ms, ImageFormat.Png);

        WriteableBitmap bitmapImage = new(bitmap.Width, bitmap.Height);
        ms.Position = 0;
        bitmapImage.SetSource(ms.AsRandomAccessStream());

        return bitmapImage;
    }

    public static void SaveQrCodePngToStream(Stream outputStream, string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0, double logoPaddingPixels = 8.0, double qrPaddingModules = 2.0, QrFramePreset framePreset = QrFramePreset.None, string? frameText = null)
    {
        using Bitmap bitmap = CreateQrCodeBitmap(
            text,
            correctionLevel,
            foreground,
            background,
            logoImage,
            logoSizePercentage,
            logoPaddingPixels,
            qrPaddingModules,
            framePreset,
            frameText,
            out _);
        bitmap.Save(outputStream, ImageFormat.Png);
    }

    internal static void SaveQrCodePngToStream(Stream outputStream, string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background, Bitmap? logoImage, double logoSizePercentage, double logoPaddingPixels, double qrPaddingModules, QrFramePreset framePreset, string? frameText, out QrImageLayoutMetrics imageLayout)
    {
        using Bitmap bitmap = CreateQrCodeBitmap(
            text,
            correctionLevel,
            foreground,
            background,
            logoImage,
            logoSizePercentage,
            logoPaddingPixels,
            qrPaddingModules,
            framePreset,
            frameText,
            out int qrRenderSize);
        imageLayout = GetQrImageLayoutMetrics(qrRenderSize, framePreset);
        bitmap.Save(outputStream, ImageFormat.Png);
    }

    internal static QrImageLayoutMetrics GetQrImageLayoutMetrics(
        string text,
        ErrorCorrectionLevel correctionLevel,
        double qrPaddingModules = 2.0,
        QrFramePreset framePreset = QrFramePreset.None)
    {
        int normalizedQrPaddingModules = NormalizeQrPaddingModules(qrPaddingModules);
        QRCode qrCode = ZXing.QrCode.Internal.Encoder.encode(text, correctionLevel);
        int moduleCount = qrCode.Version.DimensionForVersion;
        int renderSize = GetQrRenderSize(moduleCount, normalizedQrPaddingModules);
        return GetQrImageLayoutMetrics(renderSize, framePreset);
    }

    private static QrImageLayoutMetrics GetQrImageLayoutMetrics(int qrRenderSize, QrFramePreset framePreset)
    {
        switch (framePreset)
        {
            case QrFramePreset.BottomLabel:
                {
                    int outerPadding = ScaleMetric(qrRenderSize, 0.05, 36);
                    int labelHeight = ScaleMetric(qrRenderSize, 0.16, 84);
                    int labelSpacing = ScaleMetric(qrRenderSize, 0.035, 20);
                    int canvasWidth = qrRenderSize + (outerPadding * 2);
                    int canvasHeight = qrRenderSize + (outerPadding * 2) + labelSpacing + labelHeight;
                    return new QrImageLayoutMetrics(canvasWidth, canvasHeight, qrRenderSize, qrRenderSize);
                }
            case QrFramePreset.RoundedFrame:
                {
                    int outerPadding = ScaleMetric(qrRenderSize, 0.1, 56);
                    int labelHeight = ScaleMetric(qrRenderSize, 0.14, 80);
                    int labelSpacing = ScaleMetric(qrRenderSize, 0.03, 18);
                    int canvasWidth = qrRenderSize + (outerPadding * 2);
                    int canvasHeight = qrRenderSize + (outerPadding * 2) + labelSpacing + labelHeight;
                    return new QrImageLayoutMetrics(canvasWidth, canvasHeight, qrRenderSize, qrRenderSize);
                }
            case QrFramePreset.CornerCallout:
                {
                    int outerPadding = ScaleMetric(qrRenderSize, 0.11, 64);
                    int labelHeight = ScaleMetric(qrRenderSize, 0.13, 72);
                    int labelSpacing = ScaleMetric(qrRenderSize, 0.04, 24);
                    int canvasWidth = qrRenderSize + (outerPadding * 2);
                    int canvasHeight = qrRenderSize + (outerPadding * 2) + labelSpacing + labelHeight;
                    return new QrImageLayoutMetrics(canvasWidth, canvasHeight, qrRenderSize, qrRenderSize);
                }
            default:
                return new QrImageLayoutMetrics(qrRenderSize, qrRenderSize, qrRenderSize, qrRenderSize);
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

    private static Bitmap CreateQrCodeBitmap(string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background, Bitmap? logoImage, double logoSizePercentage, double logoPaddingPixels, double qrPaddingModules, QrFramePreset framePreset, string? frameText, out int qrRenderSize)
    {
        // Always pass fully opaque colors to ZXing — if the background color has A=0, ZXing fills
        // nothing (transparent brush = SourceOver no-op) and the bitmap initializes to black, making
        // foreground and background pixels indistinguishable. ApplyAlphaToQrBitmap handles alpha.
        int normalizedQrPaddingModules = NormalizeQrPaddingModules(qrPaddingModules);
        QRCode qrCode = ZXing.QrCode.Internal.Encoder.encode(text, correctionLevel);
        int moduleCount = qrCode.Version.DimensionForVersion;
        qrRenderSize = GetQrRenderSize(moduleCount, normalizedQrPaddingModules);

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
            Width = qrRenderSize,
            Height = qrRenderSize,
            Margin = normalizedQrPaddingModules,
        };
        encodingOptions.Hints.Add(EncodeHintType.ERROR_CORRECTION, correctionLevel);
        barcodeWriter.Options = encodingOptions;

        Bitmap rawBitmap = barcodeWriter.Write(text);
        bool needsAlpha = foreground.A < 255 || background.A < 255;
        Bitmap bitmap = rawBitmap;

        if (needsAlpha)
        {
            bitmap = ApplyAlphaToQrBitmap(rawBitmap, foreground, background);
            rawBitmap.Dispose();
        }

        try
        {
            if (logoImage != null)
            {
                OverlayLogoOnQrCode(bitmap, logoImage, logoSizePercentage, moduleCount, encodingOptions.Margin, logoPaddingPixels, background);
            }

            Bitmap framedBitmap = AddFrameToBitmap(bitmap, foreground, background, framePreset, frameText);
            if (!ReferenceEquals(framedBitmap, bitmap))
            {
                bitmap.Dispose();
                bitmap = framedBitmap;
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static Bitmap AddFrameToBitmap(Bitmap qrBitmap, System.Drawing.Color foreground, System.Drawing.Color background, QrFramePreset framePreset, string? frameText)
    {
        string? resolvedFrameText = QrFramePresetCatalog.ResolveText(framePreset, frameText);
        if (string.IsNullOrWhiteSpace(resolvedFrameText))
            return qrBitmap;

        return framePreset switch
        {
            QrFramePreset.BottomLabel => AddBottomLabelFrameToBitmap(qrBitmap, resolvedFrameText, foreground, background),
            QrFramePreset.RoundedFrame => AddRoundedFrameToBitmap(qrBitmap, resolvedFrameText, foreground, background),
            QrFramePreset.CornerCallout => AddCornerCalloutFrameToBitmap(qrBitmap, resolvedFrameText, foreground, background),
            _ => qrBitmap,
        };
    }

    private static Bitmap AddBottomLabelFrameToBitmap(Bitmap qrBitmap, string frameText, System.Drawing.Color foreground, System.Drawing.Color background)
    {
        int outerPadding = ScaleMetric(qrBitmap.Width, 0.05, 36);
        int labelHeight = ScaleMetric(qrBitmap.Width, 0.16, 84);
        int labelSpacing = ScaleMetric(qrBitmap.Width, 0.035, 20);
        int labelWidth = qrBitmap.Width + ScaleMetric(qrBitmap.Width, 0.08, 40);
        int outlineThickness = ScaleMetric(qrBitmap.Width, 0.014, 8);
        int accentHeight = ScaleMetric(qrBitmap.Width, 0.014, 8);
        int qrX = outerPadding;
        int qrY = outerPadding;
        int canvasWidth = qrBitmap.Width + (outerPadding * 2);
        int canvasHeight = qrY + qrBitmap.Height + labelSpacing + labelHeight + outerPadding;
        int labelX = (canvasWidth - labelWidth) / 2;
        int labelY = qrY + qrBitmap.Height + labelSpacing;
        int accentWidth = Math.Max(labelWidth / 4, ScaleMetric(qrBitmap.Width, 0.18, 120));
        int accentX = (canvasWidth - accentWidth) / 2;
        int accentY = labelY - (labelSpacing / 2);
        Bitmap canvas = new(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);

        using Graphics g = Graphics.FromImage(canvas);
        PrepareGraphics(g);
        g.Clear(System.Drawing.Color.Transparent);
        g.DrawImage(qrBitmap, qrX, qrY, qrBitmap.Width, qrBitmap.Height);

        using GraphicsPath labelPath = CreateRoundedRectanglePath(new System.Drawing.RectangleF(labelX, labelY, labelWidth, labelHeight), labelHeight / 2f);
        using GraphicsPath accentPath = CreateRoundedRectanglePath(new System.Drawing.RectangleF(accentX, accentY, accentWidth, accentHeight), accentHeight / 2f);
        using SolidBrush backgroundBrush = new(background);
        using SolidBrush foregroundBrush = new(foreground);
        using Pen outlinePen = new(foreground, outlineThickness);

        g.FillPath(backgroundBrush, labelPath);
        g.DrawPath(outlinePen, labelPath);
        g.FillPath(foregroundBrush, accentPath);
        DrawFrameText(g, frameText, foreground, new System.Drawing.RectangleF(labelX, labelY, labelWidth, labelHeight), outlineThickness);

        return canvas;
    }

    private static Bitmap AddRoundedFrameToBitmap(Bitmap qrBitmap, string frameText, System.Drawing.Color foreground, System.Drawing.Color background)
    {
        int outerPadding = ScaleMetric(qrBitmap.Width, 0.1, 56);
        int borderInset = ScaleMetric(qrBitmap.Width, 0.032, 18);
        int labelHeight = ScaleMetric(qrBitmap.Width, 0.14, 80);
        int labelSpacing = ScaleMetric(qrBitmap.Width, 0.03, 18);
        int outlineThickness = ScaleMetric(qrBitmap.Width, 0.022, 14);
        int qrX = outerPadding;
        int qrY = outerPadding;
        int canvasWidth = qrBitmap.Width + (outerPadding * 2);
        int canvasHeight = qrY + qrBitmap.Height + outerPadding + labelSpacing + labelHeight;
        float borderRadius = ScaleMetric(qrBitmap.Width, 0.085, 42);
        System.Drawing.RectangleF borderRect = new(
            qrX - borderInset,
            qrY - borderInset,
            qrBitmap.Width + (borderInset * 2),
            qrBitmap.Height + (borderInset * 2));
        int labelWidth = (int)Math.Round(borderRect.Width * 0.72, MidpointRounding.AwayFromZero);
        int labelX = (canvasWidth - labelWidth) / 2;
        int labelY = (int)Math.Round(borderRect.Bottom + labelSpacing, MidpointRounding.AwayFromZero);
        Bitmap canvas = new(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);

        using Graphics g = Graphics.FromImage(canvas);
        PrepareGraphics(g);
        g.Clear(System.Drawing.Color.Transparent);

        using GraphicsPath borderPath = CreateRoundedRectanglePath(borderRect, borderRadius);
        using SolidBrush backgroundBrush = new(background);
        using Pen outlinePen = new(foreground, outlineThickness);

        g.FillPath(backgroundBrush, borderPath);
        g.DrawPath(outlinePen, borderPath);
        g.DrawImage(qrBitmap, qrX, qrY, qrBitmap.Width, qrBitmap.Height);
        DrawLabelPill(g, frameText, foreground, background, new System.Drawing.RectangleF(labelX, labelY, labelWidth, labelHeight), outlineThickness);

        return canvas;
    }

    private static Bitmap AddCornerCalloutFrameToBitmap(Bitmap qrBitmap, string frameText, System.Drawing.Color foreground, System.Drawing.Color background)
    {
        int outerPadding = ScaleMetric(qrBitmap.Width, 0.11, 64);
        int bracketInset = ScaleMetric(qrBitmap.Width, 0.03, 16);
        int bracketLength = ScaleMetric(qrBitmap.Width, 0.14, 72);
        int outlineThickness = ScaleMetric(qrBitmap.Width, 0.024, 15);
        int labelHeight = ScaleMetric(qrBitmap.Width, 0.13, 72);
        int labelSpacing = ScaleMetric(qrBitmap.Width, 0.04, 24);
        int labelWidth = ScaleMetric(qrBitmap.Width, 0.72, 520);
        int canvasWidth = qrBitmap.Width + (outerPadding * 2);
        int canvasHeight = labelHeight + labelSpacing + qrBitmap.Height + (outerPadding * 2);
        int labelX = (canvasWidth - labelWidth) / 2;
        int labelY = outerPadding / 3;
        int qrX = outerPadding;
        int qrY = labelY + labelHeight + labelSpacing;
        System.Drawing.RectangleF bracketRect = new(
            qrX - bracketInset,
            qrY - bracketInset,
            qrBitmap.Width + (bracketInset * 2),
            qrBitmap.Height + (bracketInset * 2));
        Bitmap canvas = new(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);

        using Graphics g = Graphics.FromImage(canvas);
        PrepareGraphics(g);
        g.Clear(System.Drawing.Color.Transparent);
        g.DrawImage(qrBitmap, qrX, qrY, qrBitmap.Width, qrBitmap.Height);
        DrawLabelPill(g, frameText, foreground, background, new System.Drawing.RectangleF(labelX, labelY, labelWidth, labelHeight), outlineThickness);
        DrawCornerCalloutBrackets(g, foreground, bracketRect, bracketLength, outlineThickness, labelX + (labelWidth / 2f), labelY + labelHeight);

        return canvas;
    }

    private static void PrepareGraphics(Graphics g)
    {
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    }

    private static void DrawLabelPill(Graphics g, string frameText, System.Drawing.Color foreground, System.Drawing.Color background, System.Drawing.RectangleF rect, float outlineThickness)
    {
        using GraphicsPath labelPath = CreateRoundedRectanglePath(rect, rect.Height / 2f);
        using SolidBrush backgroundBrush = new(background);
        using Pen outlinePen = new(foreground, outlineThickness);

        g.FillPath(backgroundBrush, labelPath);
        g.DrawPath(outlinePen, labelPath);
        DrawFrameText(g, frameText, foreground, rect, outlineThickness);
    }

    private static void DrawFrameText(Graphics g, string frameText, System.Drawing.Color foreground, System.Drawing.RectangleF rect, float outlineThickness)
    {
        System.Drawing.RectangleF textRect = GetFrameTextRect(rect, outlineThickness);
        float fontSize = GetFittedFrameFontSize(frameText, textRect.Size);
        using Font font = new("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using SolidBrush textBrush = new(foreground);
        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        g.DrawString(frameText, font, textBrush, textRect, format);
    }

    private static System.Drawing.RectangleF GetFrameTextRect(System.Drawing.RectangleF rect, float outlineThickness)
    {
        float horizontalPadding = Math.Max(outlineThickness * 2.5f, rect.Width * 0.08f);
        float verticalPadding = Math.Max(outlineThickness * 1.5f, rect.Height * 0.14f);
        float width = Math.Max(1f, rect.Width - (horizontalPadding * 2f));
        float height = Math.Max(1f, rect.Height - (verticalPadding * 2f));

        return new System.Drawing.RectangleF(
            rect.X + horizontalPadding,
            rect.Y + verticalPadding,
            width,
            height);
    }

    private static float GetFittedFrameFontSize(string frameText, System.Drawing.SizeF availableSize)
    {
        const float measurementFontSize = 100f;

        using Bitmap measurementBitmap = new(1, 1);
        using Graphics measurementGraphics = Graphics.FromImage(measurementBitmap);
        PrepareGraphics(measurementGraphics);
        using Font measurementFont = new("Segoe UI", measurementFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using StringFormat format = new()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        System.Drawing.SizeF measuredSize = measurementGraphics.MeasureString(frameText, measurementFont, PointF.Empty, format);
        if (measuredSize.Width <= 0 || measuredSize.Height <= 0)
            return Math.Max(1f, availableSize.Height);

        float scaleX = availableSize.Width / measuredSize.Width;
        float scaleY = availableSize.Height / measuredSize.Height;
        float scale = Math.Min(scaleX, scaleY) * 0.98f;

        return Math.Max(1f, measurementFontSize * scale);
    }

    private static void DrawCornerCalloutBrackets(Graphics g, System.Drawing.Color foreground, System.Drawing.RectangleF bracketRect, float bracketLength, float outlineThickness, float labelCenterX, float labelBottomY)
    {
        using Pen outlinePen = new(foreground, outlineThickness)
        {
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
        };

        float left = bracketRect.Left;
        float top = bracketRect.Top;
        float right = bracketRect.Right;
        float bottom = bracketRect.Bottom;

        g.DrawLines(outlinePen,
        [
            new System.Drawing.PointF(left + bracketLength, top),
            new System.Drawing.PointF(left, top),
            new System.Drawing.PointF(left, top + bracketLength),
        ]);
        g.DrawLines(outlinePen,
        [
            new System.Drawing.PointF(right - bracketLength, top),
            new System.Drawing.PointF(right, top),
            new System.Drawing.PointF(right, top + bracketLength),
        ]);
        g.DrawLines(outlinePen,
        [
            new System.Drawing.PointF(left + bracketLength, bottom),
            new System.Drawing.PointF(left, bottom),
            new System.Drawing.PointF(left, bottom - bracketLength),
        ]);
        g.DrawLines(outlinePen,
        [
            new System.Drawing.PointF(right - bracketLength, bottom),
            new System.Drawing.PointF(right, bottom),
            new System.Drawing.PointF(right, bottom - bracketLength),
        ]);

        float connectorBottom = top - (outlineThickness * 1.2f);
        if (connectorBottom > labelBottomY)
        {
            g.DrawLine(outlinePen, labelCenterX, labelBottomY, labelCenterX, connectorBottom);
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(System.Drawing.RectangleF rect, float radius)
    {
        float diameter = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
        GraphicsPath path = new();

        if (diameter <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static int ScaleMetric(int baseSize, double ratio, int minimum)
    {
        int scaledValue = (int)Math.Round(baseSize * ratio, MidpointRounding.AwayFromZero);
        return Math.Max(scaledValue, minimum);
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
    public static bool IsSizeRecommendationAvailableForPadding(double qrPaddingModules)
    {
        int normalizedQrPaddingModules = NormalizeQrPaddingModules(qrPaddingModules);
        return normalizedQrPaddingModules is >= 1 and <= 4;
    }

    public static double SmallestCodeSide(double distance, int numberOfBlocks, double qrPaddingModules = 2.0)
    {
        int padding = NormalizeQrPaddingModules(qrPaddingModules) * 2;

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

    public static bool TryGetMinimumRecommendedSideMillimeters(
        double distance,
        int numberOfBlocks,
        Windows.UI.Color foreground,
        Windows.UI.Color background,
        out double minimumSideMillimeters,
        double qrPaddingModules = 2.0)
    {
        minimumSideMillimeters = 0;

        if (foreground.A < 255 || background.A < 255)
        {
            return false;
        }

        if (!IsSizeRecommendationAvailableForPadding(qrPaddingModules))
        {
            return false;
        }

        double smallestSideInches = SmallestCodeSide(distance, numberOfBlocks, qrPaddingModules);
        if (smallestSideInches == 0)
        {
            return false;
        }

        double contrastRatio = ColorHelpers.GetContrastRatio(foreground, background);
        if (contrastRatio < 2.5)
        {
            return false;
        }

        double fractionalLoss = ContrastRatioLossFrac(contrastRatio);
        minimumSideMillimeters = (smallestSideInches / fractionalLoss) * 25.4;
        return true;
    }

    public static QrCodeSizeRecommendation GetSizeRecommendation(double distance, int numberOfBlocks, Windows.UI.Color foreground, Windows.UI.Color background, double qrPaddingModules = 2.0)
    {
        if (foreground.A < 255 || background.A < 255)
        {
            return new QrCodeSizeRecommendation(
                QrCodeSizeRecommendationKind.TransparencyDependent,
                "Exact minimum size depends on the final print surface when transparency is used.");
        }

        if (!IsSizeRecommendationAvailableForPadding(qrPaddingModules))
        {
            int normalizedQrPaddingModules = NormalizeQrPaddingModules(qrPaddingModules);
            return new QrCodeSizeRecommendation(
                QrCodeSizeRecommendationKind.PaddingDependent,
                $"Padding {normalizedQrPaddingModules} is outside the predictable 1-4 module range. Borderless and heavy-border QR Codes scan too variably for reliable print sizing.");
        }

        bool isMetric = RegionInfo.CurrentRegion.IsMetric;
        double smallestSide = SmallestCodeSide(distance, numberOfBlocks, qrPaddingModules);

        if (smallestSide == 0)
        {
            return new QrCodeSizeRecommendation(
                QrCodeSizeRecommendationKind.Error,
                "Error at selected max distance.");
        }

        double contrastRatio = ColorHelpers.GetContrastRatio(foreground, background);

        if (contrastRatio < 2.5)
        {
            return new QrCodeSizeRecommendation(
                QrCodeSizeRecommendationKind.LowContrast,
                "Color contrast too low");
        }

        if (!TryGetMinimumRecommendedSideMillimeters(distance, numberOfBlocks, foreground, background, out double minimumSideMillimeters, qrPaddingModules))
        {
            return new QrCodeSizeRecommendation(
                QrCodeSizeRecommendationKind.Error,
                "Error at selected max distance.");
        }

        if (!isMetric)
        {
            double smallestSideInches = minimumSideMillimeters / 25.4;
            return new QrCodeSizeRecommendation(
                QrCodeSizeRecommendationKind.Exact,
                $"{smallestSideInches:F2} x {smallestSideInches:F2} in");
        }

        double smallestSideCm = minimumSideMillimeters / 10.0;
        return new QrCodeSizeRecommendation(
            QrCodeSizeRecommendationKind.Exact,
            $"{smallestSideCm:F2} x {smallestSideCm:F2} cm");
    }

    public static SvgImage GetSvgQrCodeForText(string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0, double logoPaddingPixels = 8.0, string? logoSvgContent = null, double qrPaddingModules = 2.0, QrFramePreset framePreset = QrFramePreset.None, string? frameText = null)
    {
        int normalizedQrPaddingModules = NormalizeQrPaddingModules(qrPaddingModules);
        QRCode qrCode = ZXing.QrCode.Internal.Encoder.encode(text, correctionLevel);
        int moduleCount = qrCode.Version.DimensionForVersion;
        int renderSize = GetQrRenderSize(moduleCount, normalizedQrPaddingModules);

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
            Width = renderSize,
            Height = renderSize,
            Margin = normalizedQrPaddingModules,
        };
        encodingOptions.Hints.Add(EncodeHintType.ERROR_CORRECTION, correctionLevel);
        barcodeWriter.Options = encodingOptions;

        SvgImage svg = barcodeWriter.Write(text);

        // If a logo is provided, embed it in the SVG
        if (logoImage != null || logoSvgContent != null)
        {
            svg = EmbedLogoInSvg(svg, logoImage, logoSizePercentage, moduleCount, encodingOptions.Margin, renderSize, logoPaddingPixels, background, logoSvgContent);
        }

        svg = ApplyFrameToSvg(svg, renderSize, foreground, background, framePreset, frameText);

        return svg;
    }

    private static SvgImage ApplyFrameToSvg(SvgImage svg, int qrRenderSize, System.Drawing.Color foreground, System.Drawing.Color background, QrFramePreset framePreset, string? frameText)
    {
        string? resolvedFrameText = QrFramePresetCatalog.ResolveText(framePreset, frameText);
        if (string.IsNullOrWhiteSpace(resolvedFrameText))
            return svg;

        string svgBody = ExtractSvgInnerContent(svg.Content);
        if (string.IsNullOrWhiteSpace(svgBody))
            return svg;

        string qrGroup;
        string frameMarkup;
        string canvasWidth;
        string canvasHeight;

        switch (framePreset)
        {
            case QrFramePreset.BottomLabel:
                {
                    int outerPadding = ScaleMetric(qrRenderSize, 0.05, 36);
                    int labelHeight = ScaleMetric(qrRenderSize, 0.16, 84);
                    int labelSpacing = ScaleMetric(qrRenderSize, 0.035, 20);
                    int labelWidth = qrRenderSize + ScaleMetric(qrRenderSize, 0.08, 40);
                    int outlineThickness = ScaleMetric(qrRenderSize, 0.014, 8);
                    int accentHeight = ScaleMetric(qrRenderSize, 0.014, 8);
                    int qrX = outerPadding;
                    int qrY = outerPadding;
                    int totalWidth = qrRenderSize + (outerPadding * 2);
                    int totalHeight = qrY + qrRenderSize + labelSpacing + labelHeight + outerPadding;
                    int labelX = (totalWidth - labelWidth) / 2;
                    int labelY = qrY + qrRenderSize + labelSpacing;
                    int accentWidth = Math.Max(labelWidth / 4, ScaleMetric(qrRenderSize, 0.18, 120));
                    int accentX = (totalWidth - accentWidth) / 2;
                    int accentY = labelY - (labelSpacing / 2);

                    qrGroup = CreateTranslatedSvgGroup(svgBody, qrX, qrY);
                    frameMarkup = $"""
  <rect x="{SvgValue(labelX)}" y="{SvgValue(labelY)}" width="{SvgValue(labelWidth)}" height="{SvgValue(labelHeight)}" rx="{SvgValue(labelHeight / 2.0)}" ry="{SvgValue(labelHeight / 2.0)}" fill="{ToSvgColor(background)}" stroke="{ToSvgColor(foreground)}" stroke-width="{SvgValue(outlineThickness)}"/>
  <rect x="{SvgValue(accentX)}" y="{SvgValue(accentY)}" width="{SvgValue(accentWidth)}" height="{SvgValue(accentHeight)}" rx="{SvgValue(accentHeight / 2.0)}" ry="{SvgValue(accentHeight / 2.0)}" fill="{ToSvgColor(foreground)}"/>
  {CreateSvgTextElement(resolvedFrameText, new System.Drawing.RectangleF(labelX, labelY, labelWidth, labelHeight), outlineThickness, foreground)}
""";
                    canvasWidth = SvgValue(totalWidth);
                    canvasHeight = SvgValue(totalHeight);
                    break;
                }
            case QrFramePreset.RoundedFrame:
                {
                    int outerPadding = ScaleMetric(qrRenderSize, 0.1, 56);
                    int borderInset = ScaleMetric(qrRenderSize, 0.032, 18);
                    int labelHeight = ScaleMetric(qrRenderSize, 0.14, 80);
                    int labelSpacing = ScaleMetric(qrRenderSize, 0.03, 18);
                    int outlineThickness = ScaleMetric(qrRenderSize, 0.022, 14);
                    int qrX = outerPadding;
                    int qrY = outerPadding;
                    int totalWidth = qrRenderSize + (outerPadding * 2);
                    int totalHeight = qrY + qrRenderSize + outerPadding + labelSpacing + labelHeight;
                    int borderX = qrX - borderInset;
                    int borderY = qrY - borderInset;
                    int borderWidth = qrRenderSize + (borderInset * 2);
                    int borderHeight = qrRenderSize + (borderInset * 2);
                    int borderRadius = ScaleMetric(qrRenderSize, 0.085, 42);
                    int labelWidth = (int)Math.Round(borderWidth * 0.72, MidpointRounding.AwayFromZero);
                    int labelX = (totalWidth - labelWidth) / 2;
                    int labelY = borderY + borderHeight + labelSpacing;

                    qrGroup = CreateTranslatedSvgGroup(svgBody, qrX, qrY);
                    frameMarkup = $"""
  <rect x="{SvgValue(borderX)}" y="{SvgValue(borderY)}" width="{SvgValue(borderWidth)}" height="{SvgValue(borderHeight)}" rx="{SvgValue(borderRadius)}" ry="{SvgValue(borderRadius)}" fill="{ToSvgColor(background)}" stroke="{ToSvgColor(foreground)}" stroke-width="{SvgValue(outlineThickness)}"/>
  <rect x="{SvgValue(labelX)}" y="{SvgValue(labelY)}" width="{SvgValue(labelWidth)}" height="{SvgValue(labelHeight)}" rx="{SvgValue(labelHeight / 2.0)}" ry="{SvgValue(labelHeight / 2.0)}" fill="{ToSvgColor(background)}" stroke="{ToSvgColor(foreground)}" stroke-width="{SvgValue(outlineThickness)}"/>
  {CreateSvgTextElement(resolvedFrameText, new System.Drawing.RectangleF(labelX, labelY, labelWidth, labelHeight), outlineThickness, foreground)}
""";
                    canvasWidth = SvgValue(totalWidth);
                    canvasHeight = SvgValue(totalHeight);
                    break;
                }
            case QrFramePreset.CornerCallout:
                {
                    int outerPadding = ScaleMetric(qrRenderSize, 0.11, 64);
                    int bracketInset = ScaleMetric(qrRenderSize, 0.03, 16);
                    int bracketLength = ScaleMetric(qrRenderSize, 0.14, 72);
                    int outlineThickness = ScaleMetric(qrRenderSize, 0.024, 15);
                    int labelHeight = ScaleMetric(qrRenderSize, 0.13, 72);
                    int labelSpacing = ScaleMetric(qrRenderSize, 0.04, 24);
                    int labelWidth = ScaleMetric(qrRenderSize, 0.72, 520);
                    int totalWidth = qrRenderSize + (outerPadding * 2);
                    int totalHeight = labelHeight + labelSpacing + qrRenderSize + (outerPadding * 2);
                    int labelX = (totalWidth - labelWidth) / 2;
                    int labelY = outerPadding / 3;
                    int qrX = outerPadding;
                    int qrY = labelY + labelHeight + labelSpacing;
                    int bracketLeft = qrX - bracketInset;
                    int bracketTop = qrY - bracketInset;
                    int bracketRight = qrX + qrRenderSize + bracketInset;
                    int bracketBottom = qrY + qrRenderSize + bracketInset;

                    qrGroup = CreateTranslatedSvgGroup(svgBody, qrX, qrY);
                    frameMarkup = $"""
  <rect x="{SvgValue(labelX)}" y="{SvgValue(labelY)}" width="{SvgValue(labelWidth)}" height="{SvgValue(labelHeight)}" rx="{SvgValue(labelHeight / 2.0)}" ry="{SvgValue(labelHeight / 2.0)}" fill="{ToSvgColor(background)}" stroke="{ToSvgColor(foreground)}" stroke-width="{SvgValue(outlineThickness)}"/>
  {CreateSvgTextElement(resolvedFrameText, new System.Drawing.RectangleF(labelX, labelY, labelWidth, labelHeight), outlineThickness, foreground)}
  <path d="M {SvgValue(bracketLeft + bracketLength)} {SvgValue(bracketTop)} L {SvgValue(bracketLeft)} {SvgValue(bracketTop)} L {SvgValue(bracketLeft)} {SvgValue(bracketTop + bracketLength)}
           M {SvgValue(bracketRight - bracketLength)} {SvgValue(bracketTop)} L {SvgValue(bracketRight)} {SvgValue(bracketTop)} L {SvgValue(bracketRight)} {SvgValue(bracketTop + bracketLength)}
           M {SvgValue(bracketLeft + bracketLength)} {SvgValue(bracketBottom)} L {SvgValue(bracketLeft)} {SvgValue(bracketBottom)} L {SvgValue(bracketLeft)} {SvgValue(bracketBottom - bracketLength)}
           M {SvgValue(bracketRight - bracketLength)} {SvgValue(bracketBottom)} L {SvgValue(bracketRight)} {SvgValue(bracketBottom)} L {SvgValue(bracketRight)} {SvgValue(bracketBottom - bracketLength)}" fill="none" stroke="{ToSvgColor(foreground)}" stroke-width="{SvgValue(outlineThickness)}" stroke-linecap="round" stroke-linejoin="round"/>
  <line x1="{SvgValue(labelX + (labelWidth / 2.0))}" y1="{SvgValue(labelY + labelHeight)}" x2="{SvgValue(labelX + (labelWidth / 2.0))}" y2="{SvgValue(bracketTop - (outlineThickness * 1.2))}" stroke="{ToSvgColor(foreground)}" stroke-width="{SvgValue(outlineThickness)}" stroke-linecap="round"/>
""";
                    canvasWidth = SvgValue(totalWidth);
                    canvasHeight = SvgValue(totalHeight);
                    break;
                }
            default:
                return svg;
        }

        string content = $"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {canvasWidth} {canvasHeight}" width="{canvasWidth}" height="{canvasHeight}">
{frameMarkup}
{qrGroup}
</svg>
""";
        return new SvgImage(content);
    }

    private static string CreateTranslatedSvgGroup(string svgBody, double offsetX, double offsetY)
    {
        return $"  <g transform=\"translate({SvgValue(offsetX)} {SvgValue(offsetY)})\">{Environment.NewLine}{svgBody}{Environment.NewLine}  </g>";
    }

    private static string CreateSvgTextElement(string value, System.Drawing.RectangleF rect, float outlineThickness, System.Drawing.Color foreground)
    {
        System.Drawing.RectangleF textRect = GetFrameTextRect(rect, outlineThickness);
        float fontSize = GetFittedFrameFontSize(value, textRect.Size);
        string escapedText = System.Security.SecurityElement.Escape(value) ?? string.Empty;
        float centerX = textRect.X + (textRect.Width / 2f);
        float centerY = textRect.Y + (textRect.Height / 2f);
        return $"<text x=\"{SvgValue(centerX)}\" y=\"{SvgValue(centerY)}\" fill=\"{ToSvgColor(foreground)}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"{SvgValue(fontSize)}\" font-weight=\"700\" text-anchor=\"middle\" dominant-baseline=\"middle\">{escapedText}</text>";
    }

    private static string ToSvgColor(System.Drawing.Color color)
    {
        return color.A < 255
            ? $"rgba({color.R},{color.G},{color.B},{(color.A / 255.0).ToString("0.###", CultureInfo.InvariantCulture)})"
            : $"rgb({color.R},{color.G},{color.B})";
    }

    private static string SvgValue(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static SvgImage EmbedLogoInSvg(SvgImage svg, Bitmap? logo, double sizePercentage, int moduleCount, int margin, int svgSize, double logoPaddingPixels, System.Drawing.Color backgroundColor, string? logoSvgContent = null)
    {
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

        // Build an SVG mask that subtracts the logo area from the QR code content.
        // A plain <rect fill="transparent"> painted on top of SVG paths does nothing —
        // the mask approach is the only reliable way to clear QR modules in the logo zone.
        string maskId = "logo-mask";
        string maskDefs = $"""
<defs>
  <mask id="{maskId}">
    <rect width="{svgSize}" height="{svgSize}" fill="white"/>
    <rect x="{punchoutX}" y="{punchoutY}" width="{punchoutSize}" height="{punchoutSize}" fill="black"/>
  </mask>
</defs>
""";

        // Find the end of the opening <svg> tag so we can inject <defs> and wrap the QR
        // content in a masked group.  Start search at the <svg element, not at position 0,
        // to skip any leading <?xml?> or <!DOCTYPE> declarations.
        // Use a regex that skips quoted attribute values so a stray '>' inside an attribute
        // (e.g. style="fill:>") does not split the tag prematurely.
        string svgContent = svg.Content;
        Match openTagMatch = SvgTagRegex().Match(svgContent);
        int openTagEnd = openTagMatch.Index + openTagMatch.Length - 1;

        // Wrap only the outermost </svg> — use LastIndexOf so any nested <svg> elements
        // (e.g. from an inlined SVG logo) are not accidentally closed early.
        string svgBody = svgContent[(openTagEnd + 1)..];
        int lastClose = svgBody.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);

        string modifiedContent = svgContent[..(openTagEnd + 1)]
            + "\n" + maskDefs
            + $"\n<g mask=\"url(#{maskId})\">"
            + (lastClose >= 0
                ? svgBody[..lastClose] + $"</g>\n{logoSvgElement}\n</svg>"
                : svgBody + $"</g>\n{logoSvgElement}\n</svg>");  // malformed but degrade gracefully

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

        foreach (int decodePadding in GetDecodePaddings(bitmap))
        {
            Bitmap? paddedBitmap = decodePadding > 0
                ? AddOuterDecodePadding(bitmap, decodePadding)
                : null;
            Bitmap decodeBitmap = paddedBitmap ?? bitmap;

            Result[] results = barcodeReader.DecodeMultiple(decodeBitmap);
            List<(string, Result)> strings = ConvertResultsToStrings(results, decodePadding);
            paddedBitmap?.Dispose();

            if (strings.Count > 0)
                return strings;
        }

        return [];
    }

    private static List<(string, Result)> ConvertResultsToStrings(Result[]? results, int decodePadding)
    {
        List<(string, Result)> strings = [];

        if (results == null || results.Length == 0)
            return strings;

        foreach (Result result in results)
        {
            if (string.IsNullOrWhiteSpace(result.Text))
                continue;

            if (decodePadding > 0)
                ShiftResultPointsToOriginalImage(result, decodePadding);

            strings.Add((result.Text, result));
        }

        return strings;
    }

    private static void ShiftResultPointsToOriginalImage(Result result, int decodePadding)
    {
        ResultPoint[]? points = result.ResultPoints;
        if (points == null)
            return;

        for (int i = 0; i < points.Length; i++)
        {
            ResultPoint p = points[i];
            if (p != null)
                points[i] = new ResultPoint(p.X - decodePadding, p.Y - decodePadding);
        }
    }

    private static IEnumerable<int> GetDecodePaddings(Bitmap bitmap)
    {
        int minDimension = Math.Min(bitmap.Width, bitmap.Height);
        int mediumPadding = Math.Clamp((int)Math.Round(minDimension * 0.03, MidpointRounding.AwayFromZero), 8, 24);
        int largePadding = Math.Clamp((int)Math.Round(minDimension * 0.08, MidpointRounding.AwayFromZero), 16, 64);

        int[] paddings = [0, mediumPadding, largePadding];
        return paddings.Distinct().Order();
    }

    private static Bitmap AddOuterDecodePadding(Bitmap source, int padding)
    {
        Bitmap padded = new(source.Width + (padding * 2), source.Height + (padding * 2), PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(padded);
        g.Clear(System.Drawing.Color.White);
        g.DrawImageUnscaled(source, padding, padding);
        return padded;
    }

    /// <summary>
    /// Extracts the viewBox value from an SVG string (e.g. "0 0 600 530").
    /// Falls back to constructing one from width/height attributes if no viewBox is present.
    /// </summary>
    private static string ExtractSvgViewBox(string svgContent)
    {
        // Prefer an explicit viewBox attribute
        Match viewBoxMatch =
            SvgViewboxRegex().Match(svgContent);
        if (viewBoxMatch.Success)
            return viewBoxMatch.Groups[1].Value;

        // Fall back to synthesising viewBox from width/height on the <svg> element
        Match widthMatch =
            SvgTagWidthRegex().Match(svgContent);
        Match heightMatch =
            SvgTagHeightRegex().Match(svgContent);

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

    public static int NormalizeQrPaddingModules(double qrPaddingModules)
    {
        return Math.Clamp((int)Math.Round(qrPaddingModules, MidpointRounding.AwayFromZero), 0, 10);
    }

    private static int GetQrRenderSize(int moduleCount, int margin)
    {
        int totalModules = moduleCount + (margin * 2);
        int pixelsPerModule = Math.Max(1, MaxQrRenderSize / totalModules);
        return totalModules * pixelsPerModule;
    }

    [GeneratedRegex(@"<svg\b(?:[^>""']|""[^""]*""|'[^']*')*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-US")]
    private static partial Regex SvgTagRegex();

    [GeneratedRegex(@"viewBox\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex SvgViewboxRegex();

    [GeneratedRegex(@"<svg[^>]*\swidth\s*=\s*[""']([0-9.]+)[""']", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex SvgTagWidthRegex();

    [GeneratedRegex(@"<svg[^>]*\sheight\s*=\s*[""']([0-9.]+)[""']", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex SvgTagHeightRegex();
}
