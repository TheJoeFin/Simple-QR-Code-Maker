using Simple_QR_Code_Maker.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Security;
using System.Text;

namespace Simple_QR_Code_Maker.Helpers;

public static partial class QrCodeRenderer
{
    private readonly record struct CaptionLayout(string Text, float FontSize, RectangleF TextBounds, RectangleF IconBounds);

    private static Color GetCaptionInk(Color accent, Color? gradientEnd)
    {
        static double Luminance(Color color)
        {
            int Composite(byte channel) => (channel * color.A + 255 * (255 - color.A) + 127) / 255;
            Color opaque = Color.FromArgb(255, Composite(color.R), Composite(color.G), Composite(color.B));
            static double Linear(byte value) => value <= 10 ? value / 3294.6 : Math.Pow((value / 255.0 + 0.055) / 1.055, 2.4);
            return Linear(opaque.R) * 0.2126 + Linear(opaque.G) * 0.7152 + Linear(opaque.B) * 0.0722;
        }
        double start = Luminance(accent);
        double end = Luminance(gradientEnd ?? accent);
        double blackContrast = (Math.Min(start, end) + 0.05) / 0.05;
        double whiteContrast = 1.05 / (Math.Max(start, end) + 0.05);
        return blackContrast >= whiteContrast ? Color.Black : Color.White;
    }

    private static CaptionLayout GetCaptionLayout(string text, FrameLayout layout, QrCaptionIcon icon)
    {
        // Captions are intentionally single-line in both outputs; data payloads are untouched.
        text = text.ReplaceLineEndings(" ").Replace('\t', ' ');
        RectangleF available = GetFrameTextRect(layout.Caption, layout.Stroke);
        float iconSize = icon == QrCaptionIcon.None ? 0 : Math.Min(available.Height * 0.78f, available.Width * 0.12f);
        float gap = iconSize * 0.28f;
        float textWidth = Math.Max(0.01f, available.Width - iconSize - gap);
        using Bitmap measurement = new(1, 1);
        using Graphics graphics = Graphics.FromImage(measurement);
        using Font font = new("Segoe UI", 100, FontStyle.Bold, GraphicsUnit.Pixel);
        using StringFormat format = CreateCaptionStringFormat();
        SizeF measured = graphics.MeasureString(text, font, PointF.Empty, format);
        float scale = Math.Min(textWidth / Math.Max(measured.Width, 1), available.Height / Math.Max(measured.Height, 1)) * 0.94f;
        float fontSize = Math.Max(0.001f, 100 * scale);
        float fittedWidth = Math.Min(textWidth, measured.Width * fontSize / 100);
        float combinedWidth = fittedWidth + iconSize + gap;
        float left = available.Left + (available.Width - combinedWidth) / 2;
        RectangleF iconBounds = new(left, available.Top + (available.Height - iconSize) / 2, iconSize, iconSize);
        RectangleF textBounds = new(left + iconSize + gap, available.Top, fittedWidth, available.Height);
        return new CaptionLayout(text, fontSize, textBounds, iconBounds);
    }

    private static StringFormat CreateCaptionStringFormat() => new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.None,
        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces,
    };

    private static void DrawAppearanceCaption(Graphics graphics, string text, FrameLayout frame, Color accent, Color? gradientEnd, QrCaptionIcon icon)
    {
        CaptionLayout layout = GetCaptionLayout(text, frame, icon);
        Color ink = GetCaptionInk(accent, gradientEnd);
        using Font font = new("Segoe UI", layout.FontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using SolidBrush brush = new(ink);
        using StringFormat format = CreateCaptionStringFormat();
        graphics.DrawString(layout.Text, font, brush, layout.TextBounds, format);
        if (icon != QrCaptionIcon.None)
        {
            using GraphicsPath path = CreateCaptionIconPath(icon, layout.IconBounds);
            using Pen pen = new(ink, layout.IconBounds.Width * 0.085f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            graphics.DrawPath(pen, path);
        }
    }

    private static string CreateAppearanceCaptionSvg(string text, FrameLayout frame, Color accent, Color? gradientEnd, QrCaptionIcon icon)
    {
        CaptionLayout layout = GetCaptionLayout(text, frame, icon);
        string ink = ToSvgColor(GetCaptionInk(accent, gradientEnd));
        StringBuilder markup = new();
        if (icon != QrCaptionIcon.None)
        {
            using GraphicsPath path = CreateCaptionIconPath(icon, layout.IconBounds);
            markup.Append($"<path data-caption-icon=\"{icon}\" d=\"{GetSvgPathData(path)}\" fill=\"none\" stroke=\"{ink}\" stroke-width=\"{SvgValue(layout.IconBounds.Width * 0.085f)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
        }
        // textLength also bounds fallback fonts in SVG viewers with different font metrics.
        markup.Append($"<text xml:space=\"preserve\" x=\"{SvgValue(layout.TextBounds.Left + layout.TextBounds.Width / 2)}\" y=\"{SvgValue(layout.TextBounds.Top + layout.TextBounds.Height / 2)}\" fill=\"{ink}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"{SvgValue(layout.FontSize)}\" font-weight=\"700\" text-anchor=\"middle\" dominant-baseline=\"middle\" textLength=\"{SvgValue(layout.TextBounds.Width)}\" lengthAdjust=\"spacingAndGlyphs\">{SecurityElement.Escape(layout.Text)}</text>");
        return markup.ToString();
    }

    private static GraphicsPath CreateCaptionIconPath(QrCaptionIcon icon, RectangleF bounds)
    {
        GraphicsPath path = new();
        switch (icon)
        {
            case QrCaptionIcon.Arrow:
                path.AddLine(3, 12, 21, 12);
                path.StartFigure();
                path.AddLines([new PointF(13, 4), new PointF(21, 12), new PointF(13, 20)]);
                break;
            case QrCaptionIcon.WiFi:
                path.AddBezier(2, 8, 7, 3, 17, 3, 22, 8);
                path.StartFigure();
                path.AddBezier(5, 12, 9, 8, 15, 8, 19, 12);
                path.StartFigure();
                path.AddBezier(9, 16, 11, 14, 13, 14, 15, 16);
                path.StartFigure();
                path.AddEllipse(11, 19, 2, 2);
                break;
            case QrCaptionIcon.Contact:
                path.AddEllipse(8, 3, 8, 8);
                path.StartFigure();
                path.AddBezier(3, 21, 3, 11, 21, 11, 21, 21);
                path.AddLine(21, 21, 3, 21);
                break;
            case QrCaptionIcon.Link:
                using (GraphicsPath first = CreateRoundedRectanglePath(new RectangleF(2, 8, 12, 8), 4))
                using (GraphicsPath second = CreateRoundedRectanglePath(new RectangleF(10, 8, 12, 8), 4))
                {
                    path.AddPath(first, false);
                    path.AddPath(second, false);
                }
                path.StartFigure();
                path.AddLine(8, 12, 16, 12);
                using (Matrix rotation = new())
                {
                    rotation.RotateAt(-35, new PointF(12, 12));
                    path.Transform(rotation);
                }
                break;
            default:
                path.Dispose();
                throw new ArgumentOutOfRangeException(nameof(icon));
        }
        using Matrix transform = new(bounds.Width / 24, 0, 0, bounds.Height / 24, bounds.X, bounds.Y);
        path.Transform(transform);
        return path;
    }
}
