using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;
using UglyToad.PdfPig;
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
        RequestedQrCodeItem[] codes = Enumerable.Range(0, 5).Select(i => new RequestedQrCodeItem($"PDF item {i}")).ToArray();
        PrintJobSettings settings = new()
        {
            PageType = type, PageLayout = orientation, CodesPerPage = 2, FitAsManyAsPossible = false,
            ShowLabels = false, CodeSizeMm = 30, MarginMm = 10, SpacingMm = 4,
        };
        byte[] bytes = await QrPdfRenderer.GenerateQrPdfBytesAsync(codes, renderSettings, settings);
        using PdfDocument document = PdfDocument.Open(bytes);
        Assert.AreEqual(3, document.NumberOfPages);
        Assert.AreEqual("QR Codes", document.Information.Title);
        Assert.AreEqual("Simple QR Code Maker", document.Information.Creator);
        int index = 0;
        foreach (var page in document.GetPages())
        {
            Assert.AreEqual(width, (double)page.Width, 0.01);
            Assert.AreEqual(height, (double)page.Height, 0.01);
            var images = page.GetImages().ToArray();
            Assert.HasCount(page.Number == 3 ? 1 : 2, images);
            Assert.AreEqual(string.Empty, page.Text);
            foreach (var image in images)
            {
                Assert.AreEqual(30 * 72 / 25.4, image.BoundingBox.Width, 0.02);
                Assert.AreEqual(image.BoundingBox.Width, image.BoundingBox.Height, 0.02);
                Assert.IsTrue(image.BoundingBox.Left >= 10 * 72 / 25.4);
                Assert.IsTrue(image.BoundingBox.Right <= width - 10 * 72 / 25.4);
                Assert.IsTrue(image.BoundingBox.Bottom >= 10 * 72 / 25.4);
                Assert.IsTrue(image.BoundingBox.Top <= height - 10 * 72 / 25.4);
                Assert.IsTrue(image.TryGetPng(out byte[]? png));
                using MemoryStream stream = new(png!);
                using Bitmap bitmap = new(stream);
                Assert.AreEqual(codes[index++].CodeAsText, QrCodeRenderer.GetStringsFromBitmap(bitmap).Single().Item1);
            }
        }
        Assert.AreEqual(5, index, "Every requested QR must be embedded exactly once.");
    }

    [TestMethod]
    public async Task PdfImagesFollowTopLeftGridOrderAndStayInsideDistinctCellsAsync()
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings();
        PrintJobSettings settings = new()
        {
            PageType = PrintPageType.Letter, PageLayout = PrintPageLayout.Portrait,
            CodesPerPage = 4, FitAsManyAsPossible = false, ShowLabels = false,
            MarginMm = 10, SpacingMm = 4, CodeSizeMm = 35,
        };
        RequestedQrCodeItem[] codes = [new("northwest"), new("northeast"), new("southwest"), new("southeast")];
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync(codes, renderSettings, settings));
        var images = pdf.GetPage(1).GetImages().ToArray();
        Assert.HasCount(4, images);
        Assert.IsTrue(images[0].BoundingBox.Right < images[1].BoundingBox.Left);
        Assert.IsTrue(images[2].BoundingBox.Right < images[3].BoundingBox.Left);
        Assert.IsTrue(images[0].BoundingBox.Bottom > images[2].BoundingBox.Top, "Top-left layout must be converted to bottom-left PDF coordinates.");
        Assert.AreEqual(images[0].BoundingBox.Left, images[2].BoundingBox.Left, 0.01);
        Assert.AreEqual(images[1].BoundingBox.Top, images[0].BoundingBox.Top, 0.01);
        double margin = 10 * 72 / 25.4;
        double spacing = 4 * 72 / 25.4;
        double cellWidth = (612 - 2 * margin - spacing) / 2;
        double cellHeight = (792 - 2 * margin - spacing) / 2;
        double imageSide = 35 * 72 / 25.4;
        Assert.AreEqual(margin + (cellWidth - imageSide) / 2, images[0].BoundingBox.Left, 0.02);
        Assert.AreEqual(792 - margin - (cellHeight - imageSide) / 2, images[0].BoundingBox.Top, 0.02);
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
            PageType = PrintPageType.Letter, CodesPerPage = 4, FitAsManyAsPossible = false,
            ShowLabels = true, CodeSizeMm = 30,
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
            PageType = PrintPageType.A5, CodesPerPage = 16, FitAsManyAsPossible = false,
            ShowLabels = true, CodeSizeMm = 20,
        };
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync([new(label)], renderSettings, settings));
        var page = pdf.GetPage(1);
        Assert.IsTrue(page.Letters.Count > 0);
        Assert.IsTrue(page.Text.Length < label.Length);
        Assert.IsTrue(page.Letters.All(letter => letter.FontSize >= 6 && letter.FontSize <= 10));
        Assert.IsTrue(page.Letters.Select(letter => Math.Round(letter.StartBaseLine.Y, 1)).Distinct().Count() <= 2);
        Assert.IsTrue(page.Letters.All(letter => letter.BoundingBox.Left >= 0 && letter.BoundingBox.Right <= (double)page.Width));
    }

    [TestMethod]
    [DataRow(QrFramePreset.BottomLabel)]
    [DataRow(QrFramePreset.RoundedFrame)]
    [DataRow(QrFramePreset.CornerCallout)]
    public async Task PdfPreservesFramedImageAspectRatioAndQrPayloadAsync(QrFramePreset frame)
    {
        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettings(frame);
        PrintJobSettings settings = new() { PageType = PrintPageType.Letter, CodesPerPage = 1, FitAsManyAsPossible = false, ShowLabels = false, CodeSizeMm = 35 };
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync([new("framed PDF")], renderSettings, settings));
        var image = pdf.GetPage(1).GetImages().Single();
        var metrics = QrCodeRenderer.GetQrImageLayoutMetrics("framed PDF", ErrorCorrectionLevel.H, 2, frame);
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
            PageType = PrintPageType.A5, FitAsManyAsPossible = true, ShowLabels = true,
            MarginMm = 10, CodeSizeMm = 40, SpacingMm = 4,
        };
        RequestedQrCodeItem[] codes = Enumerable.Range(0, 9).Select(i => new RequestedQrCodeItem($"Auto-fit {i}")).ToArray();
        var metrics = QrCodeRenderer.GetQrImageLayoutMetrics(codes[0].CodeAsText, ErrorCorrectionLevel.H, 2, QrFramePreset.CornerCallout);
        var layout = PrintLayoutHelper.CreateMetrics(settings, metrics);
        using PdfDocument pdf = PdfDocument.Open(await QrPdfRenderer.GenerateQrPdfBytesAsync(codes, renderSettings, settings));
        Assert.AreEqual((int)Math.Ceiling(9.0 / layout.CodesPerPage), pdf.NumberOfPages);
        Assert.AreEqual(9, pdf.GetPages().Sum(page => page.GetImages().Count()));
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
        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => operation);
        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsTrue(operation.IsCanceled);
    }

    [TestMethod]
    public void RendererAssemblyDoesNotDependOnWinUi()
    {
        Assert.IsFalse(typeof(QrPdfRenderer).Assembly.GetReferencedAssemblies().Any(reference =>
            reference.Name is "Microsoft.WinUI" or "Microsoft.WindowsAppSDK" or "Microsoft.UI.Xaml"));
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
