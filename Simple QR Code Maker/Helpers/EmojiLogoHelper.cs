using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Simple_QR_Code_Maker.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Windows.Storage.Streams;
namespace Simple_QR_Code_Maker.Helpers;

public static class EmojiLogoHelper
{
    private const float SvgViewportSize = 100f;
    private const float SvgGlyphSize = 84f;

    public static string GetFontFamilyName(EmojiLogoStyle style)
    {
        return style switch
        {
            EmojiLogoStyle.ThreeDimensional => "Segoe Fluent Emoji",
            _ => "Segoe UI Emoji",
        };
    }

    public static bool IsColorFontEnabled(EmojiLogoStyle style)
    {
        return style == EmojiLogoStyle.ThreeDimensional;
    }

    public static async Task<EmojiLogoAsset> CreateEmojiLogoAssetAsync(string emoji, EmojiLogoStyle style, System.Drawing.Color monochromeColor, int pixelSize = 512)
    {
        Bitmap previewBitmap = await RenderEmojiToBitmapAsync(emoji, style, monochromeColor, pixelSize);
        (string? svgContent, EmojiLogoSvgKind svgKind) = CreateSvgContent(emoji, style, monochromeColor);
        return new EmojiLogoAsset(previewBitmap, svgContent, svgKind);
    }

    public static async Task<Bitmap> RenderEmojiToBitmapAsync(string emoji, EmojiLogoStyle style, System.Drawing.Color monochromeColor, int pixelSize = 512)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            throw new ArgumentException("Emoji cannot be empty.", nameof(emoji));

        return await RenderEmojiWithWin2DAsync(emoji, style, monochromeColor, pixelSize);
    }

    private static async Task<Bitmap> RenderEmojiWithWin2DAsync(string emoji, EmojiLogoStyle style, System.Drawing.Color monochromeColor, int pixelSize)
    {
        CanvasDevice device = CanvasDevice.GetSharedDevice();
        float renderSize = pixelSize;
        using CanvasTextFormat format = new()
        {
            FontFamily = GetFontFamilyName(style),
            FontSize = renderSize,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
        };
        using CanvasTextLayout layout = new(device, emoji, format, renderSize, renderSize);
        if (IsColorFontEnabled(style))
        {
            layout.Options = CanvasDrawTextOptions.EnableColorFont;
        }
        Windows.Foundation.Rect drawBounds = layout.DrawBounds;
        using CanvasRenderTarget renderTarget = new(device, renderSize, renderSize, 96);
        using (CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession())
        {
            drawingSession.Clear(Windows.UI.Color.FromArgb(0, 0, 0, 0));

            if (drawBounds.Width > 0 && drawBounds.Height > 0)
            {
                double scale = Math.Min(1d, Math.Min(renderSize / drawBounds.Width, renderSize / drawBounds.Height));
                float translateX = (float)(-drawBounds.Left + ((renderSize - (drawBounds.Width * scale)) / 2d));
                float translateY = (float)(-drawBounds.Top + ((renderSize - (drawBounds.Height * scale)) / 2d));

                drawingSession.Transform =
                    Matrix3x2.CreateTranslation(new Vector2(translateX, translateY))
                    * Matrix3x2.CreateScale((float)scale);
            }

            drawingSession.DrawTextLayout(layout, Vector2.Zero, ToWindowsColor(monochromeColor));
        }

        using InMemoryRandomAccessStream randomAccessStream = new();
        await renderTarget.SaveAsync(randomAccessStream, CanvasBitmapFileFormat.Png);
        randomAccessStream.Seek(0);
        using MemoryStream memoryStream = new();
        await randomAccessStream.AsStreamForRead().CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        using Bitmap bitmap = new(memoryStream);
        return new Bitmap(bitmap);
    }

    private static Bitmap NormalizeCapturedEmojiBitmap(Bitmap source, EmojiLogoStyle style, int targetSize, Rectangle contentBounds)
    {
        Bitmap normalizedSource = new(source.Width, source.Height, PixelFormat.Format32bppPArgb);

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                System.Drawing.Color sourcePixel = source.GetPixel(x, y);
                int alpha = sourcePixel.A;

                if (alpha == 0)
                {
                    normalizedSource.SetPixel(x, y, System.Drawing.Color.Transparent);
                    continue;
                }

                System.Drawing.Color destinationPixel = style == EmojiLogoStyle.Monochrome
                    ? System.Drawing.Color.FromArgb(alpha, 0, 0, 0)
                    : System.Drawing.Color.FromArgb(alpha, sourcePixel.R, sourcePixel.G, sourcePixel.B);

                normalizedSource.SetPixel(x, y, destinationPixel);
            }
        }

        if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            return normalizedSource;

        Bitmap finalBitmap = new(targetSize, targetSize, PixelFormat.Format32bppPArgb);

        using Graphics graphics = Graphics.FromImage(finalBitmap);
        graphics.Clear(System.Drawing.Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

        float maxSize = targetSize * 0.84f;
        float scale = Math.Min(maxSize / contentBounds.Width, maxSize / contentBounds.Height);
        int destinationWidth = Math.Max(1, (int)Math.Round(contentBounds.Width * scale));
        int destinationHeight = Math.Max(1, (int)Math.Round(contentBounds.Height * scale));
        int destinationX = (targetSize - destinationWidth) / 2;
        int destinationY = (targetSize - destinationHeight) / 2;

        graphics.DrawImage(
            normalizedSource,
            new Rectangle(destinationX, destinationY, destinationWidth, destinationHeight),
            contentBounds,
            GraphicsUnit.Pixel);

        normalizedSource.Dispose();
        return finalBitmap;
    }

    private static bool IsLikelyIncompleteCapture(Rectangle contentBounds, int captureSize)
    {
        if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            return true;

        return contentBounds.Width < captureSize * 0.2
            || contentBounds.Height < captureSize * 0.2;
    }

    private static Rectangle FindVisibleBounds(Bitmap bitmap)
    {
        int minX = bitmap.Width;
        int minY = bitmap.Height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 8)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < minX || maxY < minY
            ? Rectangle.Empty
            : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    private static Bitmap CreateBitmapFromBgraPixels(byte[] pixels, int width, int height)
    {
        Bitmap bitmap = new(width, height, PixelFormat.Format32bppPArgb);
        BitmapData bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppPArgb);

        try
        {
            int sourceStride = width * 4;

            for (int row = 0; row < height; row++)
            {
                Marshal.Copy(
                    pixels,
                    row * sourceStride,
                    IntPtr.Add(bitmapData.Scan0, row * bitmapData.Stride),
                    sourceStride);
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
    }

    private static (string? SvgContent, EmojiLogoSvgKind SvgKind) CreateSvgContent(string emoji, EmojiLogoStyle style, System.Drawing.Color monochromeColor)
    {
        if (!IsColorFontEnabled(style))
        {
            string? monochromeSvg = TryCreateMonochromeSvgContent(emoji, monochromeColor);
            return monochromeSvg is not null
                ? (monochromeSvg, EmojiLogoSvgKind.Outline)
                : (CreateFontBackedSvgContent(emoji, EmojiLogoStyle.Monochrome, monochromeColor), EmojiLogoSvgKind.FontReference);
        }

        return style switch
        {
            EmojiLogoStyle.ThreeDimensional => (CreateFontBackedSvgContent(emoji, style, monochromeColor), EmojiLogoSvgKind.FontReference),
            _ => (null, EmojiLogoSvgKind.None),
        };
    }

    private static string? TryCreateMonochromeSvgContent(string emoji, System.Drawing.Color monochromeColor)
    {
        using GraphicsPath glyphPath = new(FillMode.Winding);
        using FontFamily fontFamily = new(GetFontFamilyName(EmojiLogoStyle.Monochrome));
        using StringFormat stringFormat = StringFormat.GenericTypographic;

        glyphPath.AddString(
            emoji,
            fontFamily,
            (int)FontStyle.Regular,
            SvgViewportSize,
            new PointF(0, 0),
            stringFormat);

        RectangleF bounds = glyphPath.GetBounds();

        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        float scale = Math.Min(SvgGlyphSize / bounds.Width, SvgGlyphSize / bounds.Height);
        float centerX = bounds.Left + (bounds.Width / 2f);
        float centerY = bounds.Top + (bounds.Height / 2f);
        string fillRule = glyphPath.FillMode == FillMode.Alternate ? "evenodd" : "nonzero";
        string transform = string.Create(
            CultureInfo.InvariantCulture,
            $"translate({FormatSvgFloat(SvgViewportSize / 2f)} {FormatSvgFloat(SvgViewportSize / 2f)}) scale({FormatSvgFloat(scale)}) translate({FormatSvgFloat(-centerX)} {FormatSvgFloat(-centerY)})");

        return $"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {FormatSvgFloat(SvgViewportSize)} {FormatSvgFloat(SvgViewportSize)}" role="img" aria-label="{EscapeSvgText(emoji)}">
  <path d="{ConvertGraphicsPathToSvgPath(glyphPath)}" fill="{ToSvgColor(monochromeColor)}" fill-rule="{fillRule}" transform="{transform}" />
</svg>
""";
    }

    private static string CreateFontBackedSvgContent(string emoji, EmojiLogoStyle style, System.Drawing.Color monochromeColor)
    {
        string fontFamily = style switch
        {
            EmojiLogoStyle.ThreeDimensional => "'Segoe Fluent Emoji', 'Segoe UI Emoji'",
            _ => "'Segoe UI Emoji'",
        };
        string fillAttribute = !IsColorFontEnabled(style)
            ? $@" fill=""{ToSvgColor(monochromeColor)}"""
            : string.Empty;

        return $"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {FormatSvgFloat(SvgViewportSize)} {FormatSvgFloat(SvgViewportSize)}" role="img" aria-label="{EscapeSvgText(emoji)}">
  <!-- Font-backed emoji export keeps the emoji as text so Windows color-font rendering can participate in SVG output. -->
  <text x="{FormatSvgFloat(SvgViewportSize / 2f)}" y="{FormatSvgFloat(SvgViewportSize / 2f)}" text-anchor="middle" dominant-baseline="central" font-size="{FormatSvgFloat(SvgGlyphSize)}" font-family="{fontFamily}" xml:space="preserve" style="font-variant-emoji: emoji;"{fillAttribute}>{EscapeSvgText(emoji)}</text>
</svg>
""";
    }

    private static string ConvertGraphicsPathToSvgPath(GraphicsPath path)
    {
        PointF[] points = path.PathPoints;
        byte[] types = path.PathTypes;
        StringBuilder pathBuilder = new();

        int index = 0;
        while (index < points.Length)
        {
            byte pointType = (byte)(types[index] & (byte)PathPointType.PathTypeMask);

            switch (pointType)
            {
                case (byte)PathPointType.Start:
                    AppendSvgCommand(pathBuilder, "M", points[index]);
                    if ((types[index] & (byte)PathPointType.CloseSubpath) != 0)
                        pathBuilder.Append(" Z");
                    index++;
                    break;
                case (byte)PathPointType.Line:
                    AppendSvgCommand(pathBuilder, "L", points[index]);
                    if ((types[index] & (byte)PathPointType.CloseSubpath) != 0)
                        pathBuilder.Append(" Z");
                    index++;
                    break;
                case (byte)PathPointType.Bezier3:
                    if (index + 2 >= points.Length)
                    {
                        index = points.Length;
                        break;
                    }

                    pathBuilder.Append(CultureInfo.InvariantCulture, $" C {FormatSvgFloat(points[index].X)} {FormatSvgFloat(points[index].Y)} {FormatSvgFloat(points[index + 1].X)} {FormatSvgFloat(points[index + 1].Y)} {FormatSvgFloat(points[index + 2].X)} {FormatSvgFloat(points[index + 2].Y)}");

                    if ((types[index + 2] & (byte)PathPointType.CloseSubpath) != 0)
                        pathBuilder.Append(" Z");

                    index += 3;
                    break;
                default:
                    index++;
                    break;
            }
        }

        return pathBuilder.ToString().Trim();
    }

    private static void AppendSvgCommand(StringBuilder builder, string command, PointF point)
    {
        builder.Append(CultureInfo.InvariantCulture, $" {command} {FormatSvgFloat(point.X)} {FormatSvgFloat(point.Y)}");
    }

    private static string EscapeSvgText(string value)
    {
        return SecurityElement.Escape(value) ?? value;
    }

    private static string FormatSvgFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static Windows.UI.Color ToWindowsColor(System.Drawing.Color color)
    {
        return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    private static string ToSvgColor(System.Drawing.Color color)
    {
        return color.A < 255
            ? $"rgba({color.R},{color.G},{color.B},{color.A / 255.0:F3})"
            : $"rgb({color.R},{color.G},{color.B})";
    }
}
