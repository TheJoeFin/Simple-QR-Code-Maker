using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Drawing;
using System.Globalization;
using System.Xml.Linq;
using ZXing;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Windows.Tests.Rendering;

[TestClass]
public sealed class QrCodeRendererTests
{
    [TestMethod]
    [DataRow("Hello, QR!")]
    [DataRow("café déjà vu ÿ")]
    [DataRow("漢字かな中文")]
    [DataRow("Привет, мир")]
    [DataRow("QR 😀🚀🌈")]
    public void PngAndSvgPreservePayload(string payload)
    {
        using MemoryStream stream = new();
        QrCodeRenderer.SaveQrCodePngToStream(stream, payload, ErrorCorrectionLevel.M, Color.Black, Color.White);
        Assert.IsTrue(stream.CanWrite, "The renderer must not close the caller's stream.");
        stream.Position = 0;
        using Bitmap png = new(stream);
        AssertPayload(png, payload);

        string svg = QrCodeRenderer.GetSvgQrCodeForText(payload, ErrorCorrectionLevel.M, Color.Black, Color.White).Content;
        XElement root = XElement.Parse(svg);
        Assert.AreEqual("svg", root.Name.LocalName);
        Assert.Contains(e => e.Name.LocalName is "rect" or "path" or "polygon", root.Descendants());
        using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
        AssertPayload(rasterized, payload);
    }

    [TestMethod]
    [DataRow("L", 20)]
    [DataRow("M", 23)]
    [DataRow("Q", 35)]
    [DataRow("H", 40)]
    public void CorrectionLevelsAreUsedByEncoderAndRenderer(string levelName, int maxLogo)
    {
        ErrorCorrectionLevel level = Level(levelName);
        const string payload = "correction-level payload 0123456789";
        QRCode qr = QrCodeRenderer.EncodeQrCode(payload, level);
        Assert.AreEqual(level, qr.ECLevel);
        Assert.AreEqual(maxLogo, QrCodeRenderer.GetMaxLogoSizePercentage(level));
        using Bitmap image = QrCodeRenderer.GetQrCodeBitmapFromText(payload, level, Color.Black, Color.White);
        Result result = QrCodeRenderer.GetStringsFromBitmap(image).Single().Item2;
        Assert.AreEqual(payload, result.Text);
        Assert.AreEqual(levelName, result.ResultMetadata[ResultMetadataType.ERROR_CORRECTION_LEVEL]);
    }

    [TestMethod]
    public void VersionOneCapacityMatchesKnownNumericAlphanumericAndLatin1Limits()
    {
        // Version 1-L capacities from the QR specification, independent of decoder behavior.
        AssertVersion(new string('1', 41), 1);
        AssertVersion(new string('1', 42), 2);
        AssertVersion(new string('A', 25), 1);
        AssertVersion(new string('A', 26), 2);
        AssertVersion(new string('é', 17), 1);
        AssertVersion(new string('é', 18), 2);
        Assert.AreEqual(Mode.NUMERIC, QrCodeRenderer.EncodeQrCode("01234567", ErrorCorrectionLevel.L).Mode);
        Assert.AreEqual(Mode.BYTE, QrCodeRenderer.EncodeQrCode("é", ErrorCorrectionLevel.L).Mode);
        Assert.ThrowsExactly<WriterException>(() => QrCodeRenderer.EncodeQrCode(new string('é', 4000), ErrorCorrectionLevel.H));
    }

    [TestMethod]
    [DataRow(0, 1008)]
    [DataRow(2, 1000)]
    [DataRow(4, 1015)]
    [DataRow(10, 984)]
    public void PixelsUseWholeModulesAndExactQuietZones(int padding, int expectedSide)
    {
        const string payload = "01234567";
        QRCode qr = QrCodeRenderer.EncodeQrCode(payload, ErrorCorrectionLevel.L);
        Assert.AreEqual(21, qr.Version.DimensionForVersion);
        using Bitmap image = QrCodeRenderer.GetQrCodeBitmapFromText(payload, ErrorCorrectionLevel.L, Color.Black, Color.White, qrPaddingModules: padding);
        Assert.AreEqual(expectedSide, image.Width);
        Assert.AreEqual(expectedSide, image.Height);
        int scale = expectedSide / (21 + (padding * 2));
        for (int y = 0; y < 21 + (padding * 2); y++)
        {
            for (int x = 0; x < 21 + (padding * 2); x++)
            {
                bool isDark = x >= padding && x < 21 + padding && y >= padding && y < 21 + padding
                    && qr.Matrix[x - padding, y - padding] == 1;
                Color expected = isDark ? Color.Black : Color.White;
                Assert.AreEqual(expected.ToArgb(), image.GetPixel((x * scale) + (scale / 2), (y * scale) + (scale / 2)).ToArgb(), $"module ({x},{y})");
            }
        }

        // Fixed structural expectations, not a renderer-vs-decoder round trip.
        string[] finder = ["1111111", "1000001", "1011101", "1011101", "1011101", "1000001", "1111111"];
        for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
                Assert.AreEqual(finder[y][x] - '0', qr.Matrix[x, y]);
        for (int x = 8; x <= 12; x++)
            Assert.AreEqual(x % 2 == 0 ? 1 : 0, qr.Matrix[x, 6]);
        AssertPayload(image, payload);
    }

    [TestMethod]
    [DataRow(-100.0, 0)]
    [DataRow(0.49, 0)]
    [DataRow(0.5, 1)]
    [DataRow(2.49, 2)]
    [DataRow(2.5, 3)]
    [DataRow(9.5, 10)]
    [DataRow(100.0, 10)]
    public void PaddingIsRoundedAwayFromZeroAndClamped(double value, int expected)
        => Assert.AreEqual(expected, QrCodeRenderer.NormalizeQrPaddingModules(value));

    [TestMethod]
    [DataRow("\r")]
    [DataRow("\n")]
    [DataRow("\r\n")]
    public void VCardSizingPngAndSvgUseIdenticalCrLfPayload(string newline)
    {
        string[] lines = ["BEGIN:VCARD", "VERSION:3.0", "FN:Zoë 😀", "N:Example;Zoë;;;", "NOTE:First\\nSecond", "END:VCARD"];
        string input = string.Join(newline, lines);
        string expected = string.Join("\r\n", lines);
        QRCode normalized = QrCodeRenderer.EncodeQrCode(expected, ErrorCorrectionLevel.Q);
        Assert.AreEqual(normalized.Version.VersionNumber, QrCodeRenderer.EncodeQrCode(input, ErrorCorrectionLevel.Q).Version.VersionNumber);
        QrImageLayoutMetrics metrics = QrCodeRenderer.GetQrImageLayoutMetrics(input, ErrorCorrectionLevel.Q);
        using Bitmap png = QrCodeRenderer.GetQrCodeBitmapFromText(input, ErrorCorrectionLevel.Q, Color.Black, Color.White);
        Assert.AreEqual(metrics.CanvasWidth, png.Width);
        AssertPayload(png, expected);
        string svg = QrCodeRenderer.GetSvgQrCodeForText(input, ErrorCorrectionLevel.Q, Color.Black, Color.White).Content;
        using Bitmap rasterized = QrCodeRenderer.RasterizeSvgToBitmap(svg, png.Width, png.Height);
        AssertPayload(rasterized, expected);
        using Bitmap ordinary = QrCodeRenderer.GetQrCodeBitmapFromText("ordinary\rtext", ErrorCorrectionLevel.M, Color.Black, Color.White);
        AssertPayload(ordinary, "ordinary\rtext");
    }

    [TestMethod]
    public void PngRetainsOpaqueColorsAndTransparentBackground()
    {
        Color foreground = Color.FromArgb(255, 24, 80, 120);
        Color background = Color.FromArgb(0, 230, 240, 250);
        using Bitmap image = QrCodeRenderer.GetQrCodeBitmapFromText("alpha", ErrorCorrectionLevel.L, foreground, background);
        int scale = image.Width / 25;
        Assert.AreEqual(0, image.GetPixel(0, 0).A);
        Assert.AreEqual(foreground.ToArgb(), image.GetPixel((2 * scale) + (scale / 2), (2 * scale) + (scale / 2)).ToArgb());
        using Bitmap translucent = QrCodeRenderer.GetQrCodeBitmapFromText("alpha", ErrorCorrectionLevel.L, Color.FromArgb(128, 0, 0, 0), Color.White);
        Assert.AreEqual(128, translucent.GetPixel((2 * scale) + (scale / 2), (2 * scale) + (scale / 2)).A);
    }

    [TestMethod]
    [DataRow(QrFramePreset.None)]
    [DataRow(QrFramePreset.BottomLabel)]
    [DataRow(QrFramePreset.RoundedFrame)]
    [DataRow(QrFramePreset.CornerCallout)]
    public void FrameGeometryMatchesPngSvgAndSizingWithoutFontSnapshots(QrFramePreset frame)
    {
        const string label = "Scan <this> & \"that\"";
        QrImageLayoutMetrics expected = QrCodeRenderer.GetQrImageLayoutMetrics("frame", ErrorCorrectionLevel.M, 2, frame);
        using MemoryStream stream = new();
        QrCodeRenderer.SaveQrCodePngToStream(stream, "frame", ErrorCorrectionLevel.M, Color.Black, Color.White, null, 20, 8, 2, frame, label, out QrImageLayoutMetrics rendered);
        Assert.AreEqual(expected, rendered);
        stream.Position = 0;
        using Bitmap bitmap = new(stream);
        Assert.AreEqual(expected.CanvasWidth, bitmap.Width);
        Assert.AreEqual(expected.CanvasHeight, bitmap.Height);
        AssertPayload(bitmap, "frame");
        XElement svg = XElement.Parse(QrCodeRenderer.GetSvgQrCodeForText("frame", ErrorCorrectionLevel.M, Color.Black, Color.White, framePreset: frame, frameText: label).Content);
        string[] viewBox = svg.Attribute("viewBox")!.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(expected.CanvasWidth, double.Parse(viewBox[2], CultureInfo.InvariantCulture));
        Assert.AreEqual(expected.CanvasHeight, double.Parse(viewBox[3], CultureInfo.InvariantCulture));
        if (frame != QrFramePreset.None)
        {
            Assert.IsGreaterThan(rendered.QrHeight, bitmap.Height);
            Assert.AreEqual(label, svg.Descendants().Single(e => e.Name.LocalName == "text").Value);
        }
    }

    [TestMethod]
    public void SyntheticRasterLogoPreservesAspectRatioAndPayload()
    {
        using Bitmap logo = new(12, 6);
        using (Graphics graphics = Graphics.FromImage(logo))
            graphics.Clear(Color.Red);
        using Bitmap qr = QrCodeRenderer.GetQrCodeBitmapFromText("https://example.test/logo", ErrorCorrectionLevel.H, Color.Black, Color.White, logo, 15, 4);
        Assert.AreEqual(Color.Red.ToArgb(), qr.GetPixel(qr.Width / 2, qr.Height / 2).ToArgb());
        AssertPayload(qr, "https://example.test/logo");
        XElement svg = XElement.Parse(QrCodeRenderer.GetSvgQrCodeForText("logo", ErrorCorrectionLevel.H, Color.Black, Color.Transparent, logo, 15, 4).Content);
        XElement embedded = svg.Descendants().Single(e => e.Name.LocalName == "image");
        double width = double.Parse(embedded.Attribute("width")!.Value, CultureInfo.InvariantCulture);
        double height = double.Parse(embedded.Attribute("height")!.Value, CultureInfo.InvariantCulture);
        Assert.AreEqual(2, width / height, 0.05);
        string href = embedded.Attribute("href")!.Value;
        Assert.IsTrue(href.StartsWith("data:image/png;base64,", StringComparison.Ordinal));
        using MemoryStream bytes = new(Convert.FromBase64String(href.Split(',')[1]));
        using Bitmap embeddedImage = new(bytes);
        Assert.AreEqual((int)width, embeddedImage.Width);
        Assert.AreEqual((int)height, embeddedImage.Height);
        Assert.Contains(e => e.Name.LocalName == "mask", svg.Descendants());
        Assert.Contains(e => e.Attribute("mask")?.Value == "url(#logo-mask)", svg.Descendants());
    }

    [TestMethod]
    public void InlineSvgLogoIsNestedAndOpacityIsCultureIndependent()
    {
        const string logo = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 12 6"><rect width="12" height="6" fill="red"/></svg>""";
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            XElement root = XElement.Parse(QrCodeRenderer.GetSvgQrCodeForText("inline", ErrorCorrectionLevel.H, Color.Black, Color.FromArgb(128, 20, 30, 40), logoSvgContent: logo).Content);
            XElement nested = root.Descendants().Single(e => e.Name.LocalName == "svg");
            Assert.AreEqual("0 0 12 6", nested.Attribute("viewBox")!.Value);
            Assert.AreEqual("xMidYMid meet", nested.Attribute("preserveAspectRatio")!.Value);
            Assert.DoesNotContain(a => a.Value.Contains("0,502", StringComparison.Ordinal), root.Descendants().Attributes("fill"), "SVG decimal alpha must not use the current culture's comma.");
            Assert.Contains(a => a.Value == "rgba(20,30,40,0.502)", root.Descendants().Attributes("fill"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
        Assert.ThrowsExactly<InvalidDataException>(() => QrCodeRenderer.GetSvgQrCodeForText("bad", ErrorCorrectionLevel.H, Color.Black, Color.White, logoSvgContent: "<rect/>"));
    }

    [TestMethod]
    public void DecoderReadsIndependentlyAuthoredNumericFixtureAndRejectsBlank()
    {
        // Synthetic version 1-L / mask 0 fixture for "01234567", authored independently
        // of ZXing: data 10 20 0C 56 61 80 + EC/11 pad, RS 53 55 97 67 10 05 84.
        string[] modules =
        [
            "111111100010101111111", "100000100000101000001", "101110101010001011101",
            "101110100000101011101", "101110100101101011101", "100000100111001000001",
            "111111101010101111111", "000000001010000000000", "111011111010111000100",
            "001101001001010100010", "100000111111011101110", "110101011101110110010",
            "101010111101011100001", "000000001000001000010", "111111101110100010001",
            "100000101000001001011", "101110101110101011101", "101110100111010101110",
            "101110101111011100101", "100000101001110111000", "111111101101011100101",
        ];
        using Bitmap fixture = new(232, 232);
        using (Graphics graphics = Graphics.FromImage(fixture))
        {
            graphics.Clear(Color.White);
            for (int y = 0; y < 21; y++)
                for (int x = 0; x < 21; x++)
                    if (modules[y][x] == '1')
                        graphics.FillRectangle(Brushes.Black, (x + 4) * 8, (y + 4) * 8, 8, 8);
        }
        AssertPayload(fixture, "01234567");
        using Bitmap blank = new(64, 64);
        using (Graphics graphics = Graphics.FromImage(blank))
            graphics.Clear(Color.White);
        Assert.IsEmpty(QrCodeRenderer.GetStringsFromBitmap(blank));
    }

    [TestMethod]
    [DataRow(235, 195, 210)]
    [DataRow(255, 234, 225)]
    public void DecoderRecoversFadedDottedCodeAndPreservesFinderPositions(int background, int ring, int dot)
    {
        const string payload = "synthetic faint finder QR";
        const int moduleSize = 5;
        const int left = 80;
        const int top = 40;
        ByteMatrix modules = QrCodeRenderer.EncodeQrCode(payload, ErrorCorrectionLevel.H).Matrix;
        using Bitmap fixture = new(340, 290);
        using (Graphics graphics = Graphics.FromImage(fixture))
        using (SolidBrush finderRing = new(Color.FromArgb(ring, ring, ring)))
        using (SolidBrush fadedDot = new(Color.FromArgb(dot, dot, dot)))
        {
            graphics.Clear(Color.FromArgb(background, background, background));
            graphics.FillRectangle(Brushes.DimGray, 30, 230, 270, 25);
            for (int y = 0; y < modules.Height; y++)
            {
                for (int x = 0; x < modules.Width; x++)
                {
                    if (modules[x, y] != 1)
                        continue;

                    int finderX = x >= modules.Width - 7 ? x - (modules.Width - 7) : x;
                    int finderY = y >= modules.Height - 7 ? y - (modules.Height - 7) : y;
                    bool isFinder = (x < 7 && finderY < 7) || (finderX < 7 && y < 7);
                    int pixelX = left + (x * moduleSize);
                    int pixelY = top + (y * moduleSize);
                    if (isFinder)
                    {
                        bool isRing = (finderX is 0 or 6) || (finderY is 0 or 6);
                        graphics.FillRectangle(isRing ? finderRing : Brushes.Black, pixelX, pixelY, moduleSize, moduleSize);
                    }
                    else
                    {
                        graphics.FillRectangle(fadedDot, pixelX + 1, pixelY + 1, moduleSize - 2, moduleSize - 2);
                    }
                }
            }
        }

        (string, Result)[] results = [.. QrCodeRenderer.GetStringsFromBitmap(fixture)];
        Assert.HasCount(1, results);
        (string text, Result result) = results[0];
        Assert.AreEqual(payload, text);
        Assert.AreEqual(BarcodeFormat.QR_CODE, result.BarcodeFormat);

        float center = 3.5f * moduleSize;
        float spacing = (modules.Width - 7) * moduleSize;
        foreach ((float x, float y) in new (float, float)[]
        {
            (left + center, top + center),
            (left + center + spacing, top + center),
            (left + center, top + center + spacing),
        })
        {
            Assert.Contains(p => Math.Abs(p.X - x) <= 1 && Math.Abs(p.Y - y) <= 1, result.ResultPoints);
        }
    }

    private static void AssertVersion(string payload, int expected)
        => Assert.AreEqual(expected, QrCodeRenderer.EncodeQrCode(payload, ErrorCorrectionLevel.L).Version.VersionNumber);

    private static void AssertPayload(Bitmap image, string expected)
    {
        (string, Result)[] results = [.. QrCodeRenderer.GetStringsFromBitmap(image)];
        Assert.HasCount(1, results);
        Assert.AreEqual(expected, results[0].Item1);
        Assert.AreEqual(BarcodeFormat.QR_CODE, results[0].Item2.BarcodeFormat);
    }

    private static ErrorCorrectionLevel Level(string name) => name switch
    {
        "L" => ErrorCorrectionLevel.L,
        "Q" => ErrorCorrectionLevel.Q,
        "H" => ErrorCorrectionLevel.H,
        _ => ErrorCorrectionLevel.M,
    };
}
