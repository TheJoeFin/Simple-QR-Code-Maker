#if WINDOWS
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
#endif
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Models;
using SkiaSharp;
using System.Globalization;
#if WINDOWS
using System.Numerics;
#endif
using System.Security;
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
        SKBitmap previewBitmap = await RenderEmojiToBitmapAsync(emoji, style, monochromeColor, pixelSize);
        (string? svgContent, EmojiLogoSvgKind svgKind) = CreateSvgContent(emoji, style, monochromeColor);
        return new EmojiLogoAsset(previewBitmap, svgContent, svgKind);
    }

    public static async Task<SKBitmap> RenderEmojiToBitmapAsync(string emoji, EmojiLogoStyle style, System.Drawing.Color monochromeColor, int pixelSize = 512)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            throw new ArgumentException("Emoji cannot be empty.", nameof(emoji));

        return await RenderEmojiBitmapAsync(emoji, style, monochromeColor, pixelSize);
    }

#if !WINDOWS
    private static Task<SKBitmap> RenderEmojiBitmapAsync(string emoji, EmojiLogoStyle style, System.Drawing.Color monochromeColor, int pixelSize)
    {
        // Win2D isn't available outside the Windows App SDK; render the emoji glyph with SkiaSharp.
        // SkiaSharp renders color-font glyphs (COLR/CBDT) automatically when the typeface is a color font.
        SKBitmap bitmap = new(new SKImageInfo(pixelSize, pixelSize, SKColorType.Bgra8888, SKAlphaType.Premul));
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.Transparent);

        using SKTypeface typeface = ResolveEmojiTypeface(style, emoji);
        float fontSize = pixelSize * 0.7f;
        using SKFont font = new(typeface, fontSize);
        using SKPaint paint = new()
        {
            IsAntialias = true,
            Color = style == EmojiLogoStyle.Monochrome ? monochromeColor.ToSkColor() : SKColors.Black,
        };

        font.MeasureText(emoji, out SKRect bounds);
        float x = (pixelSize / 2f) - bounds.MidX;
        float y = (pixelSize / 2f) - bounds.MidY;
        canvas.DrawText(emoji, x, y, SKTextAlign.Left, font, paint);
        canvas.Flush();

        return Task.FromResult(bitmap);
    }

    private static SKTypeface ResolveEmojiTypeface(EmojiLogoStyle style, string emoji)
    {
        int codepoint = emoji.Length > 0 ? char.ConvertToUtf32(emoji, 0) : 'a';
        return SKFontManager.Default.MatchCharacter(GetFontFamilyName(style), codepoint)
            ?? SKTypeface.FromFamilyName(GetFontFamilyName(style))
            ?? SKTypeface.Default;
    }
#else
    private static async Task<SKBitmap> RenderEmojiBitmapAsync(string emoji, EmojiLogoStyle style, System.Drawing.Color monochromeColor, int pixelSize)
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
        return SKBitmap.Decode(memoryStream);
    }
#endif

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
        using SKTypeface typeface = SKTypeface.FromFamilyName(GetFontFamilyName(EmojiLogoStyle.Monochrome)) ?? SKTypeface.Default;
        using SKFont font = new(typeface, SvgViewportSize);

        ushort[] glyphs = font.GetGlyphs(emoji);
        if (glyphs.Length == 0)
            return null;

        float[] widths = font.GetGlyphWidths(glyphs);
        using SKPath glyphPath = new();
        float xpos = 0;
        for (int i = 0; i < glyphs.Length; i++)
        {
            using SKPath? gp = font.GetGlyphPath(glyphs[i]);
            if (gp is not null && !gp.IsEmpty)
            {
                gp.Transform(SKMatrix.CreateTranslation(xpos, 0));
                glyphPath.AddPath(gp);
            }

            if (i < widths.Length)
                xpos += widths[i];
        }

        if (glyphPath.IsEmpty)
            return null;

        SKRect bounds = glyphPath.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        float scale = Math.Min(SvgGlyphSize / bounds.Width, SvgGlyphSize / bounds.Height);
        float centerX = bounds.Left + (bounds.Width / 2f);
        float centerY = bounds.Top + (bounds.Height / 2f);
        string transform = string.Create(
            CultureInfo.InvariantCulture,
            $"translate({FormatSvgFloat(SvgViewportSize / 2f)} {FormatSvgFloat(SvgViewportSize / 2f)}) scale({FormatSvgFloat(scale)}) translate({FormatSvgFloat(-centerX)} {FormatSvgFloat(-centerY)})");

        return $"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {FormatSvgFloat(SvgViewportSize)} {FormatSvgFloat(SvgViewportSize)}" role="img" aria-label="{EscapeSvgText(emoji)}">
  <path d="{glyphPath.ToSvgPathData()}" fill="{ToSvgColor(monochromeColor)}" fill-rule="nonzero" transform="{transform}" />
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

    private static string EscapeSvgText(string value)
    {
        return SecurityElement.Escape(value) ?? value;
    }

    private static string FormatSvgFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

#if WINDOWS
    private static Windows.UI.Color ToWindowsColor(System.Drawing.Color color)
    {
        return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
#endif

    private static string ToSvgColor(System.Drawing.Color color)
    {
        return color.A < 255
            ? $"rgba({color.R},{color.G},{color.B},{color.A / 255.0:F3})"
            : $"rgb({color.R},{color.G},{color.B})";
    }
}
