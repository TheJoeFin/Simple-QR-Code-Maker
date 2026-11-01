using Simple_QR_Code_Maker.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using SvgImage = ZXing.Rendering.SvgRenderer.SvgImage;

namespace Simple_QR_Code_Maker.Helpers;

public static partial class QrCodeRenderer
{
    private readonly record struct FrameLayout(int Width, int Height, Rectangle Qr, RectangleF Caption, float Stroke);

    private enum FramePaint { None, Accent, Panel, Outline, Shadow, Paper }

    private sealed record FrameLayer(GraphicsPath Path, FramePaint Fill, FramePaint Stroke = FramePaint.None, float StrokeWidth = 0);

    private sealed class FrameScene(FrameLayout layout, GraphicsPath panel) : IDisposable
    {
        public FrameLayout Layout { get; } = layout;
        public GraphicsPath Panel { get; } = panel;
        public List<FrameLayer> Layers { get; } = [];

        public void Dispose()
        {
            Panel.Dispose();
            foreach (FrameLayer layer in Layers)
                layer.Path.Dispose();
        }
    }

    private static bool IsDecorativeFrame(QrFramePreset preset)
        => preset is QrFramePreset.SpeechBubble or QrFramePreset.Ticket or QrFramePreset.Sticker
            or QrFramePreset.ScallopedBadge or QrFramePreset.Polaroid or QrFramePreset.Burst;

    private static bool UsesAppearanceFrame(QrFramePreset preset, QrCodeAppearance? appearance)
        => IsDecorativeFrame(preset) || (preset != QrFramePreset.None && appearance is not null
            && (appearance.FrameAccent.HasValue || appearance.FrameGradientEnd.HasValue || appearance.CaptionIcon != QrCaptionIcon.None));

    private static FrameLayout GetDecorativeFrameLayout(int size, QrFramePreset preset)
    {
        double paddingRatio = preset switch
        {
            QrFramePreset.ScallopedBadge => 0.23,
            QrFramePreset.Burst => 0.24,
            QrFramePreset.Polaroid => 0.065,
            QrFramePreset.Sticker => 0.09,
            QrFramePreset.Ticket => 0.11,
            _ => 0.10,
        };
        int padding = ScaleMetric(size, paddingRatio, 16);
        int captionHeight = ScaleMetric(size, preset == QrFramePreset.Polaroid ? 0.23 : 0.16, 32);
        int gap = ScaleMetric(size, 0.03, 8);
        int tail = preset == QrFramePreset.SpeechBubble ? ScaleMetric(size, 0.08, 20) : 0;
        int width = size + 2 * padding;
        int height = size + 2 * padding + gap + captionHeight + tail;
        Rectangle qr = new(padding, padding, size, size);
        RectangleF caption = new(padding, qr.Bottom + gap, size, captionHeight);
        return new FrameLayout(width, height, qr, caption, Math.Max(3, size * 0.007f));
    }

    private static FrameLayout GetAppearanceFrameLayout(int size, QrFramePreset preset)
    {
        if (IsDecorativeFrame(preset))
            return GetDecorativeFrameLayout(size, preset);

        QrImageLayoutMetrics metrics = GetQrImageLayoutMetrics(size, preset);
        int width = (int)metrics.CanvasWidth;
        int height = (int)metrics.CanvasHeight;
        if (preset == QrFramePreset.BottomLabel)
        {
            int padding = ScaleMetric(size, 0.05, 36);
            int labelWidth = size + ScaleMetric(size, 0.08, 40);
            int labelHeight = ScaleMetric(size, 0.16, 84);
            int gap = ScaleMetric(size, 0.035, 20);
            return new FrameLayout(width, height, new Rectangle(padding, padding, size, size),
                new RectangleF((width - labelWidth) / 2, padding + size + gap, labelWidth, labelHeight), ScaleMetric(size, 0.014, 8));
        }
        if (preset == QrFramePreset.RoundedFrame)
        {
            int padding = ScaleMetric(size, 0.1, 56);
            int inset = ScaleMetric(size, 0.032, 18);
            int labelWidth = (int)Math.Round((size + 2 * inset) * 0.72, MidpointRounding.AwayFromZero);
            return new FrameLayout(width, height, new Rectangle(padding, padding, size, size),
                new RectangleF((width - labelWidth) / 2, padding + size + inset + ScaleMetric(size, 0.03, 18),
                    labelWidth, ScaleMetric(size, 0.14, 80)), ScaleMetric(size, 0.022, 14));
        }
        if (preset == QrFramePreset.CornerCallout)
        {
            int padding = ScaleMetric(size, 0.11, 64);
            int labelHeight = ScaleMetric(size, 0.13, 72);
            int labelWidth = ScaleMetric(size, 0.72, 520);
            int labelY = padding / 3;
            return new FrameLayout(width, height,
                new Rectangle(padding, labelY + labelHeight + ScaleMetric(size, 0.04, 24), size, size),
                new RectangleF((width - labelWidth) / 2, labelY, labelWidth, labelHeight), ScaleMetric(size, 0.024, 15));
        }
        throw new ArgumentOutOfRangeException(nameof(preset));
    }

    private static FrameScene CreateFrameScene(int size, QrFramePreset preset)
    {
        FrameLayout layout = GetAppearanceFrameLayout(size, preset);
        float panelPadding = size * 0.012f;
        RectangleF panelBounds = layout.Qr;
        panelBounds.Inflate(panelPadding, panelPadding);
        // Rounding stays outside the complete square QR and its original quiet zone.
        FrameScene scene = new(layout, CreateRoundedRectanglePath(panelBounds, panelPadding * 3));
        try
        {
            float inset = size * 0.022f;
            RectangleF body = new(inset, inset, layout.Width - 2 * inset, layout.Height - 2 * inset);
            switch (preset)
            {
                case QrFramePreset.SpeechBubble:
                    body.Height -= ScaleMetric(size, 0.08, 20);
                    scene.Layers.Add(new FrameLayer(CreateBubblePath(body, size * 0.08f, size * 0.08f),
                        FramePaint.Accent, FramePaint.Outline, layout.Stroke));
                    break;
                case QrFramePreset.Ticket:
                    scene.Layers.Add(new FrameLayer(CreateTicketPath(body, layout.Qr.Bottom + size * 0.015f, size * 0.045f),
                        FramePaint.Accent, FramePaint.Outline, layout.Stroke));
                    GraphicsPath perforations = new();
                    float perforationY = layout.Qr.Bottom + size * 0.015f;
                    for (float x = body.Left + size * 0.10f; x < body.Right - size * 0.10f; x += size * 0.035f)
                    {
                        perforations.StartFigure();
                        perforations.AddLine(x, perforationY, x + size * 0.014f, perforationY);
                    }
                    scene.Layers.Add(new FrameLayer(perforations, FramePaint.None, FramePaint.Panel, size * 0.004f));
                    break;
                case QrFramePreset.Sticker:
                    body.Inflate(-size * 0.02f, -size * 0.02f);
                    RectangleF shadow = body;
                    shadow.Offset(size * 0.025f, size * 0.025f);
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(shadow, size * 0.075f), FramePaint.Shadow));
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(body, size * 0.075f),
                        FramePaint.Accent, FramePaint.Paper, size * 0.025f));
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(body, size * 0.075f),
                        FramePaint.None, FramePaint.Outline, layout.Stroke));
                    break;
                case QrFramePreset.ScallopedBadge:
                    scene.Layers.Add(new FrameLayer(CreateBadgePath(body, false, size),
                        FramePaint.Accent, FramePaint.Outline, layout.Stroke));
                    break;
                case QrFramePreset.Polaroid:
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(body, size * 0.008f),
                        FramePaint.Paper, FramePaint.Accent, layout.Stroke));
                    RectangleF band = new(body.X + layout.Stroke, layout.Qr.Bottom + size * 0.03f,
                        body.Width - 2 * layout.Stroke, body.Bottom - layout.Qr.Bottom - size * 0.03f - layout.Stroke);
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(band, size * 0.008f), FramePaint.Accent));
                    break;
                case QrFramePreset.Burst:
                    scene.Layers.Add(new FrameLayer(CreateBadgePath(body, true, size),
                        FramePaint.Accent, FramePaint.Outline, layout.Stroke));
                    break;
                case QrFramePreset.BottomLabel:
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(layout.Caption, layout.Caption.Height / 2),
                        FramePaint.Accent, FramePaint.Accent, layout.Stroke));
                    float accentWidth = Math.Max(layout.Caption.Width / 4, ScaleMetric(size, 0.18, 120));
                    float accentHeight = ScaleMetric(size, 0.014, 8);
                    float accentY = layout.Caption.Y - ScaleMetric(size, 0.035, 20) / 2;
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(
                        new RectangleF((layout.Width - accentWidth) / 2, accentY, accentWidth, accentHeight), accentHeight / 2), FramePaint.Accent));
                    break;
                case QrFramePreset.RoundedFrame:
                    RectangleF border = layout.Qr;
                    border.Inflate(ScaleMetric(size, 0.032, 18), ScaleMetric(size, 0.032, 18));
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(border, ScaleMetric(size, 0.085, 42)),
                        FramePaint.Accent, FramePaint.Accent, layout.Stroke));
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(layout.Caption, layout.Caption.Height / 2),
                        FramePaint.Accent, FramePaint.Accent, layout.Stroke));
                    break;
                case QrFramePreset.CornerCallout:
                    scene.Layers.Add(new FrameLayer(CreateRoundedRectanglePath(layout.Caption, layout.Caption.Height / 2),
                        FramePaint.Accent, FramePaint.Accent, layout.Stroke));
                    scene.Layers.Add(new FrameLayer(CreateCalloutPath(layout, size), FramePaint.None, FramePaint.Accent, layout.Stroke));
                    break;
            }

            return scene;
        }
        catch
        {
            scene.Dispose();
            throw;
        }
    }

    private static GraphicsPath CreateBubblePath(RectangleF rect, float radius, float tail)
    {
        GraphicsPath path = new();
        float diameter = 2 * radius;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddLine(rect.Left + rect.Width * 0.72f, rect.Bottom, rect.Left + rect.Width * 0.68f, rect.Bottom + tail);
        path.AddLine(rect.Left + rect.Width * 0.68f, rect.Bottom + tail, rect.Left + rect.Width * 0.54f, rect.Bottom);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath CreateTicketPath(RectangleF rect, float notchY, float radius)
    {
        GraphicsPath path = new();
        path.AddLine(rect.Left, rect.Top, rect.Right, rect.Top);
        path.AddArc(rect.Right - radius, notchY - radius, radius * 2, radius * 2, 270, -180);
        path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
        path.AddArc(rect.Left - radius, notchY - radius, radius * 2, radius * 2, 90, -180);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath CreateBadgePath(RectangleF rect, bool burst, int size)
    {
        int count = burst ? 40 : 240;
        PointF[] points = new PointF[count];
        float amplitude = size * (burst ? 0.055f : 0.015f);
        double radiusX = rect.Width / 2 - amplitude;
        double radiusY = rect.Height / 2 - amplitude;
        for (int i = 0; i < count; i++)
        {
            double angle = -Math.PI / 2 + i * Math.Tau / count;
            double cosine = Math.Cos(angle);
            double sine = Math.Sin(angle);
            double squareness = Math.Pow(Math.Pow(Math.Abs(cosine), 4) + Math.Pow(Math.Abs(sine), 4), -0.25);
            double wave = burst ? (i % 2 == 0 ? amplitude : -amplitude) : amplitude * Math.Cos(angle * 24);
            points[i] = new PointF(
                (float)(rect.Left + rect.Width / 2 + (radiusX * squareness + wave) * cosine),
                (float)(rect.Top + rect.Height / 2 + (radiusY * squareness + wave) * sine));
        }
        GraphicsPath path = new();
        if (burst)
            path.AddPolygon(points);
        else
            path.AddClosedCurve(points, 0.5f);
        return path;
    }

    private static GraphicsPath CreateCalloutPath(FrameLayout layout, int size)
    {
        RectangleF bounds = layout.Qr;
        bounds.Inflate(ScaleMetric(size, 0.03, 16), ScaleMetric(size, 0.03, 16));
        float length = ScaleMetric(size, 0.14, 72);
        GraphicsPath path = new();
        foreach ((float x, float y, float dx, float dy) in new (float, float, float, float)[]
        {
            (bounds.Left, bounds.Top, length, length), (bounds.Right, bounds.Top, -length, length),
            (bounds.Left, bounds.Bottom, length, -length), (bounds.Right, bounds.Bottom, -length, -length),
        })
        {
            path.StartFigure();
            path.AddLines([new PointF(x + dx, y), new PointF(x, y), new PointF(x, y + dy)]);
        }
        path.StartFigure();
        path.AddLine(layout.Width / 2f, layout.Caption.Bottom, layout.Width / 2f, bounds.Top - layout.Stroke * 1.2f);
        return path;
    }

    private static Color ConvertFrameColor(Windows.UI.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static GraphicsPath CreatePanelExtension(FrameScene scene)
    {
        GraphicsPath path = new(FillMode.Alternate);
        path.AddPath(scene.Panel, false);
        path.AddRectangle(scene.Layout.Qr);
        return path;
    }

    private static Color GetFramePaint(FramePaint paint, Color accent, Color foreground, Color background) => paint switch
    {
        FramePaint.Accent => accent,
        FramePaint.Panel => background,
        FramePaint.Outline => foreground,
        FramePaint.Shadow => Color.FromArgb(60, 0, 0, 0),
        FramePaint.Paper => Color.White,
        _ => Color.Transparent,
    };

    private static Brush CreateFrameBrush(FramePaint paint, FrameLayout layout, Color accent, Color? gradientEnd, Color foreground, Color background)
        => paint == FramePaint.Accent && gradientEnd.HasValue
            ? new LinearGradientBrush(new PointF(0, 0), new PointF(layout.Width, layout.Height), accent, gradientEnd.Value)
            : new SolidBrush(GetFramePaint(paint, accent, foreground, background));

    private static Bitmap AddAppearanceFrameToBitmap(Bitmap qrBitmap, Color foreground, Color background, QrFramePreset preset, string? frameText, QrCodeAppearance? appearance)
    {
        using FrameScene scene = CreateFrameScene(qrBitmap.Width, preset);
        FrameLayout layout = scene.Layout;
        Color accent = appearance?.FrameAccent is { } start ? ConvertFrameColor(start) : foreground;
        Color? gradientEnd = appearance?.FrameGradientEnd is { } end ? ConvertFrameColor(end) : null;
        Bitmap canvas = new(layout.Width, layout.Height, PixelFormat.Format32bppArgb);
        try
        {
            using Graphics graphics = Graphics.FromImage(canvas);
            PrepareGraphics(graphics);
            graphics.Clear(Color.Transparent);
            GraphicsState frameState = graphics.Save();
            using (Region panelRegion = new(scene.Panel))
                graphics.ExcludeClip(panelRegion);
            foreach (FrameLayer layer in scene.Layers)
            {
                if (layer.Fill != FramePaint.None)
                {
                    using Brush brush = CreateFrameBrush(layer.Fill, layout, accent, gradientEnd, foreground, background);
                    graphics.FillPath(brush, layer.Path);
                }
                if (layer.Stroke != FramePaint.None)
                {
                    using Brush brush = CreateFrameBrush(layer.Stroke, layout, accent, gradientEnd, foreground, background);
                    using Pen pen = new(brush, layer.StrokeWidth) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                    graphics.DrawPath(pen, layer.Path);
                }
            }
            graphics.Restore(frameState);
            // The bitmap already owns its background. Paint only the added rounded padding,
            // with no frame underneath, so translucent backgrounds are not applied twice.
            graphics.CompositingMode = CompositingMode.SourceCopy;
            using (GraphicsPath panelExtension = CreatePanelExtension(scene))
            using (SolidBrush panelBrush = new(background))
                graphics.FillPath(panelBrush, panelExtension);
            // No interpolation or fractional placement of the already integer-scaled QR.
            graphics.DrawImageUnscaled(qrBitmap, layout.Qr.Location);
            graphics.CompositingMode = CompositingMode.SourceOver;
            DrawAppearanceCaption(graphics, QrFramePresetCatalog.ResolveText(preset, frameText)!, layout, accent, gradientEnd, appearance?.CaptionIcon ?? QrCaptionIcon.None);
            return canvas;
        }
        catch
        {
            canvas.Dispose();
            throw;
        }
    }

    private static SvgImage ApplyAppearanceFrameToSvg(SvgImage svg, int qrRenderSize, Color foreground, Color background, QrFramePreset preset, string? frameText, QrCodeAppearance? appearance)
    {
        using FrameScene scene = CreateFrameScene(qrRenderSize, preset);
        FrameLayout layout = scene.Layout;
        Color accent = appearance?.FrameAccent is { } start ? ConvertFrameColor(start) : foreground;
        Color? gradientEnd = appearance?.FrameGradientEnd is { } end ? ConvertFrameColor(end) : null;
        StringBuilder markup = new();
        using GraphicsPath frameClip = new(FillMode.Alternate);
        frameClip.AddRectangle(new Rectangle(0, 0, layout.Width, layout.Height));
        frameClip.AddPath(scene.Panel, false);
        markup.Append($"""
<defs><clipPath id="frame-artwork-clip"><path d="{GetSvgPathData(frameClip)}" clip-rule="evenodd"/></clipPath></defs>
""");
        if (gradientEnd.HasValue)
        {
            markup.Append($"""
<defs><linearGradient id="frame-gradient" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="{layout.Width}" y2="{layout.Height}">
<stop offset="0" stop-color="{ToSvgColor(accent)}"/><stop offset="1" stop-color="{ToSvgColor(gradientEnd.Value)}"/>
</linearGradient></defs>
""");
        }
        string Paint(FramePaint paint) => paint == FramePaint.None ? "none"
            : paint == FramePaint.Accent && gradientEnd.HasValue ? "url(#frame-gradient)"
            : ToSvgColor(GetFramePaint(paint, accent, foreground, background));

        foreach (FrameLayer layer in scene.Layers)
        {
            markup.Append("<path d=\"").Append(GetSvgPathData(layer.Path)).Append("\" fill=\"").Append(Paint(layer.Fill))
                .Append("\" stroke=\"").Append(Paint(layer.Stroke)).Append("\" stroke-width=\"").Append(SvgValue(layer.StrokeWidth))
                .Append("\" stroke-linejoin=\"round\" stroke-linecap=\"round\" clip-path=\"url(#frame-artwork-clip)\"/>");
        }
        using GraphicsPath panelExtension = CreatePanelExtension(scene);
        markup.Append($"<path data-qr-panel=\"true\" d=\"{GetSvgPathData(panelExtension)}\" fill=\"{ToSvgColor(background)}\" fill-rule=\"evenodd\"/>");
        markup.Append(CreateTranslatedSvgGroup(ExtractSvgInnerContent(svg.Content), layout.Qr.X, layout.Qr.Y));
        markup.Append(CreateAppearanceCaptionSvg(QrFramePresetCatalog.ResolveText(preset, frameText)!, layout, accent, gradientEnd, appearance?.CaptionIcon ?? QrCaptionIcon.None));
        return new SvgImage($"""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {layout.Width} {layout.Height}" width="{layout.Width}" height="{layout.Height}">
{markup}
</svg>
""");
    }
}
