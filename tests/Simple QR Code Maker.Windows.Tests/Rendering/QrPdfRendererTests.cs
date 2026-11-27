using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;
using System.Collections;
using System.Drawing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Windows.Tests.Rendering;

[TestClass]
public sealed class QrPdfRendererTests
{
    [TestMethod]
    [DataRow(PrintPageType.Letter, PrintPageLayout.Portrait, 612.0, 792.0)]
    [DataRow(PrintPageType.Letter, PrintPageLayout.Landscape, 792.0, 612.0)]
    [DataRow(PrintPageType.A4, PrintPageLayout.Portrait, 595.0, 842.0)]
    [DataRow(PrintPageType.A4, PrintPageLayout.Landscape, 842.0, 595.0)]
    [DataRow(PrintPageType.Legal, PrintPageLayout.Portrait, 612.0, 1008.0)]
    [DataRow(PrintPageType.A5, PrintPageLayout.Portrait, 420.0, 595.0)]
    public async Task PdfPageCountsDimensionsImagesAndMetadataMatchPrintSettingsAsync(PrintPageType type, PrintPageLayout orientation, double width, double height)
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings();
        RequestedQrCodeItem[] codes = [.. Enumerable.Range(0, 5).Select(i => new RequestedQrCodeItem($"PDF item {i}"))];
        PrintJobSettings settings = new()
        {
            PageType = type,
            PageLayout = orientation,
            CodesPerPage = 2,
            FitAsManyAsPossible = false,
            ShowLabels = false,
            CodeSizeMm = 30,
            MarginMm = 10,
            SpacingMm = 4,
        };
        byte[] bytes = await QrPdfRenderer.GenerateQrPdfBytesAsync(codes, renderSettings, settings);
        using PdfDocument document = PdfDocument.Open(bytes);
        Assert.AreEqual(3, document.NumberOfPages);
        Assert.AreEqual("QR Codes", document.Information.Title);
        Assert.AreEqual("Simple QR Code Maker", document.Information.Creator);
        int index = 0;
        foreach (Page page in document.GetPages())
        {
            Assert.AreEqual(width, (double)page.Width, 0.01);
            Assert.AreEqual(height, (double)page.Height, 0.01);
            IPdfImage[] images = page.GetImages().ToArray();
            Assert.HasCount(page.Number == 3 ? 1 : 2, images);
            Assert.AreEqual(string.Empty, page.Text);
            foreach (IPdfImage? image in images)
            {
                Assert.AreEqual(30 * 72 / 25.4, image.BoundingBox.Width, 0.02);
                Assert.AreEqual(image.BoundingBox.Width, image.BoundingBox.Height, 0.02);
                Assert.IsGreaterThanOrEqualTo(10 * 72 / 25.4, image.BoundingBox.Left);
                Assert.IsLessThanOrEqualTo(width - (10 * 72 / 25.4), image.BoundingBox.Right);
                Assert.IsGreaterThanOrEqualTo(10 * 72 / 25.4, image.BoundingBox.Bottom);
                Assert.IsLessThanOrEqualTo(height - (10 * 72 / 25.4), image.BoundingBox.Top);
                Assert.IsTrue(image.TryGetPng(out byte[]? png));
                using MemoryStream stream = new(png!);
                using Bitmap bitmap = new(stream);
                Assert.AreEqual(codes[index++].CodeAsText, QrCodeRenderer.GetStringsFromBitmap(bitmap).Single().Item1);
            }
        }
        Assert.AreEqual(5, index, "Every requested QR must be embedded exactly once.");
    }

    [TestMethod]
    [DataRow("Codes à imprimer", "Légende", true)]
    [DataRow("QR コード", "印刷", false)]
    public async Task LocalizedPdfTitleAndLabelDoNotChangeEncodedTextAsync(string title, string label, bool selectable)
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings();
        RequestedQrCodeItem code = new("example payload");
        byte[] bytes = await QrPdfRenderer.GenerateQrPdfBytesAsync([code], renderSettings, new() { ShowLabels = true },
            documentTitle: title, formatLabel: _ => label);
        using PdfDocument document = PdfDocument.Open(bytes);
        Page page = document.GetPage(1);

        Assert.AreEqual(title, document.Information.Title);
        IPdfImage[] images = [.. page.GetImages()];
        Assert.HasCount(selectable ? 1 : 2, images);
        if (selectable)
        {
            StringAssert.Contains(page.Text, label);
        }
        else
        {
            Assert.IsTrue(images[1].TryGetPng(out byte[]? labelPng));
            using MemoryStream labelStream = new(labelPng!);
            using Bitmap labelImage = new(labelStream);
            Assert.IsTrue(Enumerable.Range(0, labelImage.Width).Any(x =>
                Enumerable.Range(0, labelImage.Height).Any(y => labelImage.GetPixel(x, y).R < 128)));
            Assert.IsTrue(images[1].BoundingBox.Top < images[0].BoundingBox.Bottom);
        }
        Assert.IsTrue(images[0].TryGetPng(out byte[]? png));
        using MemoryStream stream = new(png!);
        using Bitmap bitmap = new(stream);
        Assert.AreEqual(code.CodeAsText, QrCodeRenderer.GetStringsFromBitmap(bitmap).Single().Item1);
    }

    [TestMethod]
    public async Task PdfImagesFollowTopLeftGridOrderAndStayInsideDistinctCellsAsync()
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings();
        PrintJobSettings settings = new()
        {
            PageType = PrintPageType.Letter,
            PageLayout = PrintPageLayout.Portrait,
            CodesPerPage = 4,
            FitAsManyAsPossible = false,
            ShowLabels = false,
            MarginMm = 10,
            SpacingMm = 4,
            CodeSizeMm = 35,
        };
        RequestedQrCodeItem[] codes = [new("northwest"), new("northeast"), new("southwest"), new("southeast")];
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync(codes, renderSettings, settings));
        IPdfImage[] images = pdf.GetPage(1).GetImages().ToArray();
        Assert.HasCount(4, images);
        Assert.IsLessThan(images[1].BoundingBox.Left, images[0].BoundingBox.Right);
        Assert.IsLessThan(images[3].BoundingBox.Left, images[2].BoundingBox.Right);
        Assert.IsGreaterThan(images[2].BoundingBox.Top, images[0].BoundingBox.Bottom, "Top-left layout must be converted to bottom-left PDF coordinates.");
        Assert.AreEqual(images[0].BoundingBox.Left, images[2].BoundingBox.Left, 0.01);
        Assert.AreEqual(images[1].BoundingBox.Top, images[0].BoundingBox.Top, 0.01);
        double margin = 10 * 72 / 25.4;
        double spacing = 4 * 72 / 25.4;
        double cellWidth = (612 - (2 * margin) - spacing) / 2;
        double cellHeight = (792 - (2 * margin) - spacing) / 2;
        double imageSide = 35 * 72 / 25.4;
        Assert.AreEqual(margin + ((cellWidth - imageSide) / 2), images[0].BoundingBox.Left, 0.02);
        Assert.AreEqual(792 - margin - ((cellHeight - imageSide) / 2), images[0].BoundingBox.Top, 0.02);
    }

    [TestMethod]
    public async Task PdfLabelsUseStructuredContentDisplayNamesAndExcludeRawPayloadAsync()
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings();
        RequestedQrCodeItem[] codes =
        [
            new("BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Zoë Example\r\nN:Example;Zoë;;;\r\nEND:VCARD", QrContentKind.VCard),
            new("WIFI:T:WPA;S:Synthetic Network;P:secret-pass;;", QrContentKind.WiFi),
            new("mailto:person@example.test?subject=Hello&body=Private", QrContentKind.Email),
            new("Plain QR caption"),
        ];
        PrintJobSettings settings = new()
        {
            PageType = PrintPageType.Letter,
            CodesPerPage = 4,
            FitAsManyAsPossible = false,
            ShowLabels = true,
            CodeSizeMm = 30,
        };
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync(codes, renderSettings, settings));
        string text = pdf.GetPage(1).Text;
        Assert.Contains("Zoë Example", text);
        Assert.Contains("Synthetic Network", text);
        Assert.Contains("Hello", text); // Email labels prefer the subject when one is present.
        Assert.Contains("Plain QR caption", text);
        Assert.IsFalse(text.Contains("BEGIN:VCARD", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("secret-pass", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("Private", StringComparison.Ordinal));
        Assert.IsTrue(pdf.GetPage(1).Letters.All(letter => letter.FontSize >= PrintLayoutHelper.MinimumLabelFontSizePoints - 0.01));
    }

    [TestMethod]
    public async Task PdfWrapsAndShrinksLongLabelsWithoutDependingOnExactFontPixelsAsync()
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings();
        string label = string.Join(" / ", Enumerable.Repeat("long synthetic label segment", 8));
        PrintJobSettings settings = new()
        {
            PageType = PrintPageType.A5,
            CodesPerPage = 16,
            FitAsManyAsPossible = false,
            ShowLabels = true,
            CodeSizeMm = 20,
        };
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync([new(label)], renderSettings, settings));
        Page page = pdf.GetPage(1);
        Assert.IsNotEmpty(page.Letters);
        Assert.IsLessThan(label.Length, page.Text.Length);
        Assert.IsTrue(page.Letters.All(letter => letter.FontSize is >= 6 and <= 10));
        Assert.IsLessThanOrEqualTo(2, page.Letters.Select(letter => Math.Round(letter.StartBaseLine.Y, 1)).Distinct().Count());
        Assert.IsTrue(page.Letters.All(letter => letter.BoundingBox.Left >= 0 && letter.BoundingBox.Right <= (double)page.Width));
    }

    [TestMethod]
    [DataRow(QrFramePreset.BottomLabel)]
    [DataRow(QrFramePreset.RoundedFrame)]
    [DataRow(QrFramePreset.CornerCallout)]
    [DataRow(QrFramePreset.SpeechBubble)]
    [DataRow(QrFramePreset.Ticket)]
    [DataRow(QrFramePreset.Sticker)]
    [DataRow(QrFramePreset.ScallopedBadge)]
    [DataRow(QrFramePreset.Polaroid)]
    [DataRow(QrFramePreset.Burst)]
    public async Task PdfPreservesFramedImageAspectRatioAndQrPayloadAsync(QrFramePreset frame)
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings(frame);
        PrintJobSettings settings = new() { PageType = PrintPageType.Letter, CodesPerPage = 1, FitAsManyAsPossible = false, ShowLabels = false, CodeSizeMm = 35 };
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync([new("framed PDF")], renderSettings, settings));
        IPdfImage image = pdf.GetPage(1).GetImages().Single();
        QrImageLayoutMetrics metrics = QrCodeRenderer.GetQrImageLayoutMetrics("framed PDF", ErrorCorrectionLevel.H, 2, frame);
        Assert.AreEqual(metrics.CanvasWidth / metrics.CanvasHeight, image.BoundingBox.Width / image.BoundingBox.Height, 0.001);
        Assert.AreEqual(35 * 72 / 25.4 * metrics.WidthPerQrSize, image.BoundingBox.Width, 0.02);
        Assert.IsTrue(image.TryGetPng(out byte[]? bytes));
        using MemoryStream stream = new(bytes!);
        using Bitmap bitmap = new(stream);
        Assert.AreEqual("framed PDF", QrCodeRenderer.GetStringsFromBitmap(bitmap).Single().Item1);
    }

    [TestMethod]
    public async Task PdfAutoFitUsesActualFrameProfileRatherThanSquareAssumptionAsync()
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings(QrFramePreset.CornerCallout);
        PrintJobSettings settings = new()
        {
            PageType = PrintPageType.A5,
            FitAsManyAsPossible = true,
            ShowLabels = true,
            MarginMm = 10,
            CodeSizeMm = 40,
            SpacingMm = 4,
        };
        RequestedQrCodeItem[] codes = [.. Enumerable.Range(0, 9).Select(i => new RequestedQrCodeItem($"Auto-fit {i}"))];
        QrImageLayoutMetrics metrics = QrCodeRenderer.GetQrImageLayoutMetrics(codes[0].CodeAsText, ErrorCorrectionLevel.H, 2, QrFramePreset.CornerCallout);
        PrintLayoutMetrics layout = PrintLayoutHelper.CreateMetrics(settings, metrics);
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync(codes, renderSettings, settings));
        Assert.AreEqual((int)Math.Ceiling(9.0 / layout.CodesPerPage), pdf.NumberOfPages);
        Assert.AreEqual(9, pdf.GetPages().Sum(page => page.GetImages().Count()));
    }

    [TestMethod]
    public async Task PdfCarriesTheCompleteStyledDesignAsync()
    {
        QrDesignPreset preset = QrDesignPresetCatalog.All[4];
        using QrRenderSettingsSnapshot renderSettings = QrRenderSettingsSnapshot.Create(
            ErrorCorrectionLevel.H, Color.FromArgb(preset.Foreground.A, preset.Foreground.R, preset.Foreground.G, preset.Foreground.B),
            Color.White, null, 15, 4, null, 4, preset.Frame, QrFrameTextSource.Manual, preset.Caption, preset.Appearance);
        PrintJobSettings settings = new() { CodesPerPage = 1, FitAsManyAsPossible = false, ShowLabels = false, CodeSizeMm = 35 };
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync([new("styled PDF")], renderSettings, settings));
        IPdfImage image = pdf.GetPage(1).GetImages().Single();
        Assert.IsTrue(image.TryGetPng(out byte[]? bytes));
        using MemoryStream stream = new(bytes!);
        using Bitmap actual = new(stream);
        using Bitmap expected = QrCodeRenderer.GetQrCodeBitmapFromText("styled PDF", ErrorCorrectionLevel.H,
            renderSettings.ForegroundColor, Color.White, qrPaddingModules: 4, framePreset: preset.Frame,
            frameText: preset.Caption, appearance: preset.Appearance);
        Assert.AreEqual(expected.Size, actual.Size);
        for (int y = 0; y < expected.Height; y += 13)
            for (int x = 0; x < expected.Width; x += 13)
                Assert.AreEqual(expected.GetPixel(x, y).ToArgb(), actual.GetPixel(x, y).ToArgb());
        Assert.AreEqual("styled PDF", QrCodeRenderer.GetStringsFromBitmap(actual).Single().Item1);
    }

    [TestMethod]
    public async Task PreCancelledPdfDoesNotEnumerateOrRenderCodesAsync()
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => QrPdfRenderer.GenerateQrPdfBytesAsync(
            new CancellingCodes(cancellation, throwOnEnumeration: true), renderSettings, new(), cancellation.Token));
    }

    [TestMethod]
    public async Task PdfHonorsCancellationBetweenRenderedItemsAsync()
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings();
        using CancellationTokenSource cancellation = new();
        Task<byte[]> operation = QrPdfRenderer.GenerateQrPdfBytesAsync(new CancellingCodes(cancellation), renderSettings, new() { ShowLabels = false }, cancellation.Token);
        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => operation);
        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsTrue(operation.IsCanceled);
    }

    [TestMethod]
    public void RendererAssemblyDoesNotDependOnWinUi()
    {
        Assert.DoesNotContain(reference =>
            reference.Name is "Microsoft.WinUI" or "Microsoft.WindowsAppSDK" or "Microsoft.UI.Xaml", typeof(QrPdfRenderer).Assembly.GetReferencedAssemblies());
    }

    private static QrRenderSettingsSnapshot CreateRenderSettings(QrFramePreset frame = QrFramePreset.None)
        => QrRenderSettingsSnapshot.Create(ErrorCorrectionLevel.H, Color.Black, Color.White, null, 15, 4, null, 2, frame, QrFrameTextSource.Manual, "Scan me");

    private sealed class CancellingCodes(CancellationTokenSource cancellation, bool throwOnEnumeration = false) : IReadOnlyList<RequestedQrCodeItem>
    {
        public int Count => 2;
        public RequestedQrCodeItem this[int index] => new($"cancel-{index}");

        public IEnumerator<RequestedQrCodeItem> GetEnumerator()
        {
            if (throwOnEnumeration)
                throw new AssertFailedException("A pre-cancelled operation must not enumerate input.");
            yield return this[0];
            cancellation.Cancel();
            yield return this[1];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
