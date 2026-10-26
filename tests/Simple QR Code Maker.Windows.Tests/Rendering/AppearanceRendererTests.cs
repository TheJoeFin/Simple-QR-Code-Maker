using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Xml.Linq;
using ZXing.QrCode.Internal;
using WindowsColor = global::Windows.UI.Color;

namespace Simple_QR_Code_Maker.Windows.Tests.Rendering;

[TestClass]
public sealed class AppearanceRendererTests
{
    private const string DensePayload = "https://example.test/設計?name=Zoë&note=Привет-😀-漢字-" +
        "0123456789abcdefghijklmnopqrstuvwxyz-0123456789abcdefghijklmnopqrstuvwxyz";

    [TestMethod]
    [DataRow(QrFramePreset.SpeechBubble)]
    [DataRow(QrFramePreset.Ticket)]
    [DataRow(QrFramePreset.Sticker)]
    [DataRow(QrFramePreset.ScallopedBadge)]
    [DataRow(QrFramePreset.Polaroid)]
    [DataRow(QrFramePreset.Burst)]
    public void DecorativeFrameBoundsQuietZoneAndPayloadAgreeInBothOutputs(QrFramePreset preset)
    {
        const string payload = "https://example.test/設計😀";
        const string caption = "Scan <this> & \"that\" — café 漢字 😀";
        QrCodeAppearance appearance = new()
        {
            FrameAccent = WindowsColor.FromArgb(255, 30, 75, 155),
            FrameGradientEnd = WindowsColor.FromArgb(255, 75, 25, 110),
            CaptionIcon = QrCaptionIcon.Link,
        };
        QrImageLayoutMetrics metrics = QrCodeRenderer.GetQrImageLayoutMetrics(payload, ErrorCorrectionLevel.H, 4, preset);
        using MemoryStream stream = new();
        QrCodeRenderer.SaveQrCodePngToStream(stream, payload, ErrorCorrectionLevel.H, Color.Black, Color.White,
            null, 20, 8, 4, preset, caption, out QrImageLayoutMetrics actualMetrics, appearance);
        Assert.AreEqual(metrics, actualMetrics);
        Assert.IsTrue(stream.CanWrite);
        stream.Position = 0;
        using Bitmap png = new(stream);
        Assert.AreEqual(metrics.CanvasWidth, (double)png.Width);
        Assert.AreEqual(metrics.CanvasHeight, (double)png.Height);
        AssertPayload(png, payload);

        string svg = QrCodeRenderer.GetSvgQrCodeForText(payload, ErrorCorrectionLevel.H, Color.Black, Color.White,
            qrPaddingModules: 4, framePreset: preset, frameText: caption, appearance: appearance).Content;
        XElement root = XElement.Parse(svg);
        Assert.AreEqual($"0 0 {png.Width} {png.Height}", root.Attribute("viewBox")!.Value);
        Assert.AreEqual(caption, root.Descendants().Single(e => e.Name.LocalName == "text").Value);
        Assert.IsFalse(root.Descendants().Any(e => e.Name.LocalName == "image"), "A QR without a logo must be entirely vector.");
        Assert.HasCount(1, root.Descendants().Where(e => e.Name.LocalName == "linearGradient"));
        Assert.HasCount(1, root.Descendants().Where(e => e.Attribute("data-caption-icon")?.Value == "Link"));
        using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
        AssertPayload(rasterized, payload);

        (int qrX, int qrY) = GetQrOrigin(root);
        int qrSide = (int)metrics.QrWidth;
        int scale = qrSide / (QrCodeRenderer.EncodeQrCode(payload, ErrorCorrectionLevel.H).Matrix.Width + 8);
        // Rounding preserves the entire original square quiet zone.
        for (int offset = scale / 2; offset < qrSide; offset += scale)
        {
            AssertPixel(png, qrX + offset, qrY + scale / 2, Color.White);
            AssertPixel(png, qrX + scale / 2, qrY + offset, Color.White);
            AssertPixel(rasterized, qrX + offset, qrY + scale / 2, Color.White);
            AssertPixel(rasterized, qrX + scale / 2, qrY + offset, Color.White);
            AssertPixel(png, qrX + offset, qrY + qrSide - scale / 2, Color.White);
            AssertPixel(rasterized, qrX + offset, qrY + qrSide - scale / 2, Color.White);
        }
        AssertTransparentEdges(png);
        AssertTransparentEdges(rasterized);
    }

    [TestMethod]
    [DataRow(0, QrModuleStyle.Square)]
    [DataRow(128, QrModuleStyle.Square)]
    [DataRow(255, QrModuleStyle.Square)]
    [DataRow(0, QrModuleStyle.Dots)]
    [DataRow(128, QrModuleStyle.Dots)]
    [DataRow(255, QrModuleStyle.Dots)]
    public void RoundedPanelsHonorBackgroundOpacityWithoutLayeringItTwice(int alpha, QrModuleStyle modules)
    {
        const string payload = "rounded panel";
        Color background = Color.FromArgb(alpha, 180, 210, 230);
        QrCodeAppearance appearance = new()
        {
            ModuleStyle = modules,
            EyeStyle = modules == QrModuleStyle.Square ? QrEyeStyle.Square : QrEyeStyle.Rounded,
            FrameAccent = WindowsColor.FromArgb(255, 30, 75, 155),
            FrameGradientEnd = WindowsColor.FromArgb(255, 75, 25, 110),
        };
        foreach (QrFramePreset preset in Enum.GetValues<QrFramePreset>().Where(frame => frame != QrFramePreset.None))
        {
            using Bitmap png = QrCodeRenderer.GetQrCodeBitmapFromText(payload, ErrorCorrectionLevel.H,
                Color.Black, background, qrPaddingModules: 4, framePreset: preset, appearance: appearance);
            string svg = QrCodeRenderer.GetSvgQrCodeForText(payload, ErrorCorrectionLevel.H,
                Color.Black, background, qrPaddingModules: 4, framePreset: preset, appearance: appearance).Content;
            XElement root = XElement.Parse(svg);
            XElement panel = root.Elements().Single(element => element.Attribute("data-qr-panel") is not null);
            Assert.IsTrue(panel.Attribute("d")!.Value.Contains('C'), "The panel must have curved corners.");
            using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
            (int x, int y) = GetQrOrigin(root);
            int side = (int)QrCodeRenderer.GetQrImageLayoutMetrics(payload, ErrorCorrectionLevel.H, 4, preset).QrWidth;
            int padding = (int)(side * 0.012f);
            foreach (Bitmap image in new[] { png, rasterized })
            {
                foreach (Point point in new[]
                {
                    new Point(x, y), new Point(x + side - 1, y), new Point(x, y + side - 1),
                    new Point(x + side - 1, y + side - 1), new Point(x + side / 2, y + 1),
                    new Point(x - padding / 2, y + side / 2),
                })
                {
                    Color actual = image.GetPixel(point.X, point.Y);
                    Assert.AreEqual(alpha, (int)actual.A, $"{preset} {(ReferenceEquals(image, png) ? "PNG" : "SVG")} at {point}");
                    if (alpha > 0)
                    {
                        Assert.AreEqual((double)background.R, actual.R, 1, $"{preset} red at {point}");
                        Assert.AreEqual((double)background.G, actual.G, 1, $"{preset} green at {point}");
                        Assert.AreEqual((double)background.B, actual.B, 1, $"{preset} blue at {point}");
                    }
                }
            }
            if (preset == QrFramePreset.ScallopedBadge)
            {
                Color outsideCorner = png.GetPixel(x - padding + 1, y - padding + 1);
                Assert.AreEqual(255, (int)outsideCorner.A);
                Assert.AreNotEqual(background.ToArgb(), outsideCorner.ToArgb(), "The colored frame must show outside the rounded panel.");
            }
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(128)]
    [DataRow(255)]
    public void FramedTranslucentModulesDoNotBlendWithTheirBackground(int backgroundAlpha)
    {
        const string payload = "alpha";
        Color foreground = Color.FromArgb(128, 24, 80, 120);
        Color background = Color.FromArgb(backgroundAlpha, 240, 245, 250);
        foreach (QrModuleStyle modules in new[] { QrModuleStyle.Square, QrModuleStyle.Dots })
        {
            QrCodeAppearance appearance = new() { ModuleStyle = modules };
            using Bitmap png = QrCodeRenderer.GetQrCodeBitmapFromText(payload, ErrorCorrectionLevel.L,
                foreground, background, qrPaddingModules: 4, framePreset: QrFramePreset.ScallopedBadge, appearance: appearance);
            string svg = QrCodeRenderer.GetSvgQrCodeForText(payload, ErrorCorrectionLevel.L,
                foreground, background, qrPaddingModules: 4, framePreset: QrFramePreset.ScallopedBadge, appearance: appearance).Content;
            using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
            (int x, int y) = GetQrOrigin(XElement.Parse(svg));
            int side = (int)QrCodeRenderer.GetQrImageLayoutMetrics(payload, ErrorCorrectionLevel.L, 4, QrFramePreset.ScallopedBadge).QrWidth;
            int scale = side / 29;
            foreach (Bitmap image in new[] { png, rasterized })
            {
                Color actual = image.GetPixel(x + 7 * scale + scale / 2, y + 7 * scale + scale / 2);
                Assert.AreEqual((int)foreground.A, (int)actual.A);
                Assert.AreEqual((double)foreground.R, actual.R, 1);
                Assert.AreEqual((double)foreground.G, actual.G, 1);
                Assert.AreEqual((double)foreground.B, actual.B, 1);
            }
        }
    }

    [TestMethod]
    [DataRow(QrModuleStyle.Square, QrEyeStyle.Square)]
    [DataRow(QrModuleStyle.Square, QrEyeStyle.Rounded)]
    [DataRow(QrModuleStyle.Square, QrEyeStyle.Circle)]
    [DataRow(QrModuleStyle.Rounded, QrEyeStyle.Square)]
    [DataRow(QrModuleStyle.Rounded, QrEyeStyle.Rounded)]
    [DataRow(QrModuleStyle.Rounded, QrEyeStyle.Circle)]
    [DataRow(QrModuleStyle.Dots, QrEyeStyle.Square)]
    [DataRow(QrModuleStyle.Dots, QrEyeStyle.Rounded)]
    [DataRow(QrModuleStyle.Dots, QrEyeStyle.Circle)]
    public void EveryModuleAndEyeCombinationPreservesShortAndDenseUnicodePayloads(QrModuleStyle modules, QrEyeStyle eyes)
    {
        QrCodeAppearance appearance = new() { ModuleStyle = modules, EyeStyle = eyes };
        foreach (string payload in new[] { "QR 😀", DensePayload })
        {
            using Bitmap png = QrCodeRenderer.GetQrCodeBitmapFromText(payload, ErrorCorrectionLevel.H,
                Color.Black, Color.White, qrPaddingModules: 4, appearance: appearance);
            AssertPayload(png, payload);
            string svg = QrCodeRenderer.GetSvgQrCodeForText(payload, ErrorCorrectionLevel.H,
                Color.Black, Color.White, qrPaddingModules: 4, appearance: appearance).Content;
            using Bitmap rasterizedSvg = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
            using Bitmap rasterized = CompositeOnWhite(rasterizedSvg);
            AssertPayload(rasterized, payload);
            AssertProtectedPatterns(png, payload, eyes, 4);
            AssertProtectedPatterns(rasterized, payload, eyes, 4);
            if (modules != QrModuleStyle.Square || eyes != QrEyeStyle.Square)
                Assert.IsTrue(XElement.Parse(svg).Descendants().Any(e => e.Name.LocalName == "path" && e.Attribute("d")!.Value.Contains('C')));
        }
    }

    [TestMethod]
    public void VersionInformationAndAllAlignmentPatternsRemainSquare()
    {
        string payload = string.Concat(Enumerable.Repeat(DensePayload, 3));
        QRCode code = QrCodeRenderer.EncodeQrCode(payload, ErrorCorrectionLevel.H);
        Assert.IsTrue(code.Version.VersionNumber >= 7);
        QrCodeAppearance appearance = new() { ModuleStyle = QrModuleStyle.Dots, EyeStyle = QrEyeStyle.Rounded };
        using Bitmap png = QrCodeRenderer.GetQrCodeBitmapFromText(payload, ErrorCorrectionLevel.H,
            Color.Black, Color.White, qrPaddingModules: 4, appearance: appearance);
        AssertProtectedPatterns(png, payload, appearance.EyeStyle, 4);
        string svg = QrCodeRenderer.GetSvgQrCodeForText(payload, ErrorCorrectionLevel.H,
            Color.Black, Color.White, qrPaddingModules: 4, appearance: appearance).Content;
        using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
        AssertProtectedPatterns(rasterized, payload, appearance.EyeStyle, 4);
        AssertPayload(png, payload);
        AssertPayload(rasterized, payload);
    }

    [TestMethod]
    [DataRow(QrFramePreset.None)]
    [DataRow(QrFramePreset.BottomLabel)]
    [DataRow(QrFramePreset.RoundedFrame)]
    [DataRow(QrFramePreset.CornerCallout)]
    public void LegacyNullAndDefaultAppearanceAreExactlyEquivalent(QrFramePreset preset)
    {
        const string payload = "legacy café 😀";
        using Bitmap original = QrCodeRenderer.GetQrCodeBitmapFromText(payload, ErrorCorrectionLevel.M,
            Color.Navy, Color.FromArgb(128, 240, 245, 250), framePreset: preset, frameText: "Scan <me>");
        using Bitmap defaults = QrCodeRenderer.GetQrCodeBitmapFromText(payload, ErrorCorrectionLevel.M,
            Color.Navy, Color.FromArgb(128, 240, 245, 250), framePreset: preset, frameText: "Scan <me>", appearance: new QrCodeAppearance());
        using MemoryStream first = new();
        using MemoryStream second = new();
        original.Save(first, ImageFormat.Png);
        defaults.Save(second, ImageFormat.Png);
        CollectionAssert.AreEqual(first.ToArray(), second.ToArray());
        string originalSvg = QrCodeRenderer.GetSvgQrCodeForText(payload, ErrorCorrectionLevel.M,
            Color.Navy, Color.White, framePreset: preset, frameText: "Scan <me>").Content;
        string defaultSvg = QrCodeRenderer.GetSvgQrCodeForText(payload, ErrorCorrectionLevel.M,
            Color.Navy, Color.White, framePreset: preset, frameText: "Scan <me>", appearance: new QrCodeAppearance()).Content;
        Assert.AreEqual(originalSvg, defaultSvg);
    }

    [TestMethod]
    [DataRow(QrCaptionIcon.Arrow)]
    [DataRow(QrCaptionIcon.WiFi)]
    [DataRow(QrCaptionIcon.Contact)]
    [DataRow(QrCaptionIcon.Link)]
    public void LegacyFramesSupportIndependentGradientAndVectorCaptionIcons(QrCaptionIcon icon)
    {
        const string caption = "A < B & C > D \"café\" 😀 漢字 — " +
            "a rather long caption that must fit without clipping or replacing its text";
        QrCodeAppearance appearance = new()
        {
            FrameAccent = WindowsColor.FromArgb(255, 230, 200, 40),
            FrameGradientEnd = WindowsColor.FromArgb(255, 245, 160, 50),
            CaptionIcon = icon,
        };
        foreach (QrFramePreset preset in new[] { QrFramePreset.BottomLabel, QrFramePreset.RoundedFrame, QrFramePreset.CornerCallout })
        {
            string svg = QrCodeRenderer.GetSvgQrCodeForText("icons", ErrorCorrectionLevel.H, Color.Black,
                Color.White, framePreset: preset, frameText: caption, appearance: appearance).Content;
            XElement root = XElement.Parse(svg);
            XElement text = root.Descendants().Single(e => e.Name.LocalName == "text");
            Assert.AreEqual(caption, text.Value);
            Assert.AreEqual("rgb(0,0,0)", text.Attribute("fill")!.Value, "Bright filled captions use dark ink.");
            Assert.IsTrue(double.Parse(text.Attribute("textLength")!.Value, CultureInfo.InvariantCulture) > 0);
            Assert.HasCount(1, root.Descendants().Where(e => e.Attribute("data-caption-icon")?.Value == icon.ToString()));
            QrImageLayoutMetrics metrics = QrCodeRenderer.GetQrImageLayoutMetrics("icons", ErrorCorrectionLevel.H, framePreset: preset);
            using Bitmap png = QrCodeRenderer.GetQrCodeBitmapFromText("icons", ErrorCorrectionLevel.H,
                Color.Black, Color.White, framePreset: preset, frameText: caption, appearance: appearance);
            Assert.AreEqual(metrics.CanvasWidth, (double)png.Width);
            Assert.AreEqual(metrics.CanvasHeight, (double)png.Height);
            AssertPayload(png, "icons");
            using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
            AssertPayload(rasterized, "icons");
        }
    }

    [TestMethod]
    public void StyledModulesRetainForegroundAndUnframedTransparency()
    {
        Color foreground = Color.FromArgb(128, 24, 80, 120);
        QrCodeAppearance appearance = new() { ModuleStyle = QrModuleStyle.Dots, EyeStyle = QrEyeStyle.Rounded };
        using Bitmap image = QrCodeRenderer.GetQrCodeBitmapFromText("alpha", ErrorCorrectionLevel.L,
            foreground, Color.Transparent, qrPaddingModules: 4, appearance: appearance);
        int scale = image.Width / 29;
        Assert.AreEqual(0, (int)image.GetPixel(0, 0).A);
        Assert.AreEqual(foreground.ToArgb(), image.GetPixel(7 * scale + scale / 2, 7 * scale + scale / 2).ToArgb());
        XElement svg = XElement.Parse(QrCodeRenderer.GetSvgQrCodeForText("alpha", ErrorCorrectionLevel.L,
            foreground, Color.Transparent, appearance: appearance).Content);
        Assert.IsTrue(svg.Descendants().Any(e => e.Attribute("fill")?.Value == "rgba(24,80,120,0.502)"));
    }

    [TestMethod]
    public void AppearanceLogosAndVCardNormalizationRemainCompatible()
    {
        const string input = "BEGIN:VCARD\rVERSION:3.0\rFN:Zoë 😀\rN:Example;Zoë;;;\rEND:VCARD";
        string expected = input.ReplaceLineEndings("\r\n");
        using Bitmap logo = new(24, 12);
        using (Graphics graphics = Graphics.FromImage(logo))
            graphics.Clear(Color.Red);
        QrCodeAppearance appearance = new()
        {
            ModuleStyle = QrModuleStyle.Dots,
            EyeStyle = QrEyeStyle.Rounded,
            FrameAccent = WindowsColor.FromArgb(255, 30, 80, 140),
            CaptionIcon = QrCaptionIcon.Contact,
        };
        using Bitmap png = QrCodeRenderer.GetQrCodeBitmapFromText(input, ErrorCorrectionLevel.H, Color.Black,
            Color.White, logo, 12, 4, 4, QrFramePreset.Sticker, "Contact 😀", appearance);
        AssertPayload(png, expected);
        string svg = QrCodeRenderer.GetSvgQrCodeForText(input, ErrorCorrectionLevel.H, Color.Black, Color.White,
            logo, 12, 4, qrPaddingModules: 4, framePreset: QrFramePreset.Sticker, frameText: "Contact 😀", appearance: appearance).Content;
        XElement root = XElement.Parse(svg);
        Assert.HasCount(1, root.Descendants().Where(e => e.Name.LocalName == "image"));
        Assert.HasCount(1, root.Descendants().Where(e => e.Name.LocalName == "mask"));
        using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
        AssertPayload(rasterized, expected);

        const string vectorLogo = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 12 6"><rect width="12" height="6" fill="red"/></svg>""";
        string withVectorLogo = QrCodeRenderer.GetSvgQrCodeForText(input, ErrorCorrectionLevel.H, Color.Black, Color.White,
            logoSizePercentage: 12, logoSvgContent: vectorLogo, qrPaddingModules: 4,
            framePreset: QrFramePreset.Sticker, appearance: appearance).Content;
        using Bitmap vectorRasterized = QrCodeRenderer.RasterizeSvgToBitmap(withVectorLogo, png.Width, png.Height);
        AssertPayload(vectorRasterized, expected);
    }

    [TestMethod]
    public void NewFrameSilhouettesAreDistinctAndPresentWithoutAppearance()
    {
        HashSet<string> silhouettes = [];
        foreach (QrFramePreset preset in new[]
        {
            QrFramePreset.SpeechBubble, QrFramePreset.Ticket, QrFramePreset.Sticker,
            QrFramePreset.ScallopedBadge, QrFramePreset.Polaroid, QrFramePreset.Burst,
        })
        {
            string svg = QrCodeRenderer.GetSvgQrCodeForText("silhouette", ErrorCorrectionLevel.M,
                Color.Black, Color.White, framePreset: preset).Content;
            XElement root = XElement.Parse(svg);
            string firstPath = root.Elements().First(e => e.Name.LocalName == "path").Attribute("d")!.Value;
            Assert.IsTrue(silhouettes.Add(firstPath));
            using Bitmap png = QrCodeRenderer.GetQrCodeBitmapFromText("silhouette", ErrorCorrectionLevel.M,
                Color.Black, Color.White, framePreset: preset);
            AssertPayload(png, "silhouette");
            using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
            AssertPayload(rasterized, "silhouette");
        }
        Assert.HasCount(6, silhouettes);
    }

    [TestMethod]
    public void EveryCuratedPresetScansWithUnicodeAndOptionalLogo()
    {
        Assert.HasCount(8, QrDesignPresetCatalog.All);
        using Bitmap logo = new(24, 12);
        using (Graphics graphics = Graphics.FromImage(logo))
            graphics.Clear(Color.DarkOrange);
        string? samplesDirectory = Environment.GetEnvironmentVariable("QR_RENDER_SAMPLES_DIR");
        if (!string.IsNullOrEmpty(samplesDirectory))
            Directory.CreateDirectory(samplesDirectory);

        List<(string Name, Bitmap Image)> samples = [];
        try
        {
            foreach (QrDesignPreset preset in QrDesignPresetCatalog.All)
            {
                Color foreground = ToDrawingColor(preset.Foreground);
                Color background = ToDrawingColor(preset.Background);
                foreach ((string payload, Bitmap? overlay) in new (string, Bitmap?)[]
                {
                    ("https://example.test/hello", null), (DensePayload, null), (DensePayload, logo),
                })
                {
                    using Bitmap png = QrCodeRenderer.GetQrCodeBitmapFromText(payload, ErrorCorrectionLevel.H,
                        foreground, background, overlay, 12, 4, 4, preset.Frame, preset.Caption, preset.Appearance);
                    AssertPayload(png, payload);
                    string svg = QrCodeRenderer.GetSvgQrCodeForText(payload, ErrorCorrectionLevel.H,
                        foreground, background, overlay, 12, 4, qrPaddingModules: 4,
                        framePreset: preset.Frame, frameText: preset.Caption, appearance: preset.Appearance).Content;
                    using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
                    AssertPayload(rasterized, payload);
                    if (!string.IsNullOrEmpty(samplesDirectory) && payload == DensePayload && overlay is null)
                    {
                        png.Save(Path.Combine(samplesDirectory, $"{preset.Name}.png"), ImageFormat.Png);
                        File.WriteAllText(Path.Combine(samplesDirectory, $"{preset.Name}.svg"), svg);
                        samples.Add((preset.Name, new Bitmap(png)));
                    }
                }
            }
            if (!string.IsNullOrEmpty(samplesDirectory))
                SaveContactSheet(samplesDirectory, samples);
        }
        finally
        {
            foreach ((string _, Bitmap image) in samples)
                image.Dispose();
        }
    }

    private static void SaveContactSheet(string directory, List<(string Name, Bitmap Image)> samples)
    {
        const int tileWidth = 390;
        const int tileHeight = 470;
        using Bitmap sheet = new(tileWidth * 4, tileHeight * 2);
        using Graphics graphics = Graphics.FromImage(sheet);
        graphics.Clear(Color.FromArgb(245, 245, 247));
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using Font font = new("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel);
        using StringFormat format = new() { Alignment = StringAlignment.Center };
        for (int i = 0; i < samples.Count; i++)
        {
            (string name, Bitmap image) = samples[i];
            float scale = Math.Min((tileWidth - 40f) / image.Width, (tileHeight - 80f) / image.Height);
            float width = image.Width * scale;
            float height = image.Height * scale;
            float x = i % 4 * tileWidth + (tileWidth - width) / 2;
            float y = i / 4 * tileHeight + 48 + (tileHeight - 80 - height) / 2;
            graphics.DrawString(name, font, Brushes.Black,
                new RectangleF(i % 4 * tileWidth, i / 4 * tileHeight + 12, tileWidth, 32), format);
            graphics.DrawImage(image, new RectangleF(x, y, width, height));
        }
        sheet.Save(Path.Combine(directory, "contact-sheet.png"), ImageFormat.Png);
    }

    private static Color ToDrawingColor(WindowsColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static Bitmap CompositeOnWhite(Bitmap source)
    {
        Bitmap result = new(source.Width, source.Height);
        using Graphics graphics = Graphics.FromImage(result);
        graphics.Clear(Color.White);
        graphics.DrawImageUnscaled(source, 0, 0);
        return result;
    }

    private static void AssertProtectedPatterns(Bitmap bitmap, string payload, QrEyeStyle eyes, int padding)
    {
        QRCode code = QrCodeRenderer.EncodeQrCode(payload, ErrorCorrectionLevel.H);
        bool[,] protectedModules = QrCodeRenderer.GetProtectedQrModules(code);
        int size = code.Matrix.Width;
        int scale = bitmap.Width / (size + 2 * padding);
        Assert.AreEqual(0, bitmap.Width % (size + 2 * padding));
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool finder = (x < 7 && y < 7) || (x >= size - 7 && y < 7) || (x < 7 && y >= size - 7);
                if (!protectedModules[x, y] || (finder && eyes != QrEyeStyle.Square))
                    continue;
                Color expected = code.Matrix[x, y] == 1 ? Color.Black : Color.White;
                // Off-center samples detect dots/rounded corners accidentally applied to function modules.
                foreach ((int dx, int dy) in new[] { (2, 2), (scale - 3, 2), (2, scale - 3), (scale / 2, scale / 2) })
                    AssertPixel(bitmap, (x + padding) * scale + dx, (y + padding) * scale + dy, expected);
            }
        }
        // Independent checks of the protected mask catch accidental omissions in the renderer mask itself.
        Assert.IsTrue(protectedModules[8, size - 8], "Fixed dark module");
        for (int x = 8; x < size - 8; x++)
            Assert.IsTrue(protectedModules[x, 6], "Timing row");
        foreach (int centerY in code.Version.AlignmentPatternCenters)
            foreach (int centerX in code.Version.AlignmentPatternCenters)
                if (!(centerX < 9 && centerY < 9) && !(centerX < 9 && centerY > size - 9) && !(centerY < 9 && centerX > size - 9))
                    Assert.IsTrue(protectedModules[centerX - 2, centerY - 2], "Alignment pattern");
    }

    private static (int X, int Y) GetQrOrigin(XElement root)
    {
        string transform = root.Elements().Single(e => e.Name.LocalName == "g").Attribute("transform")!.Value;
        string[] offsets = transform["translate(".Length..^1].Split(' ');
        return (int.Parse(offsets[0], CultureInfo.InvariantCulture), int.Parse(offsets[1], CultureInfo.InvariantCulture));
    }

    private static void AssertTransparentEdges(Bitmap bitmap)
    {
        for (int x = 0; x < bitmap.Width; x++)
        {
            Assert.AreEqual(0, (int)bitmap.GetPixel(x, 0).A, $"Top edge at {x}");
            Assert.AreEqual(0, (int)bitmap.GetPixel(x, bitmap.Height - 1).A, $"Bottom edge at {x}");
        }
        for (int y = 0; y < bitmap.Height; y++)
        {
            Assert.AreEqual(0, (int)bitmap.GetPixel(0, y).A, $"Left edge at {y}");
            Assert.AreEqual(0, (int)bitmap.GetPixel(bitmap.Width - 1, y).A, $"Right edge at {y}");
        }
    }

    private static void AssertPixel(Bitmap bitmap, int x, int y, Color expected)
        => Assert.AreEqual(expected.ToArgb(), bitmap.GetPixel(x, y).ToArgb(), $"Pixel ({x},{y})");

    private static void AssertPayload(Bitmap image, string expected)
    {
        (string, ZXing.Result)[] results = QrCodeRenderer.GetStringsFromBitmap(image).ToArray();
        Assert.HasCount(1, results);
        Assert.AreEqual(expected, results[0].Item1);
    }
}
