using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Diagnostics;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using WinRT.Interop;
using WinPdfDocument = Windows.Data.Pdf.PdfDocument;
using WinPdfPage = Windows.Data.Pdf.PdfPage;

namespace Simple_QR_Code_Maker.Services;

public class PrintService : IPrintService
{
    private const uint PrintRenderLongEdgePixels = 2400;
    private readonly record struct PrintableQrCodeAsset(string Label, byte[] PngBytes, QrImageLayoutMetrics ImageLayout);
    private readonly SemaphoreSlim printStateGate = new(1, 1);
    private PrintDocument? activePrintDocument;
    private IPrintDocumentSource? activePrintDocumentSource;
    private PrintManager? activePrintManager;
    private List<BitmapImage> activePrintBitmaps = [];
    private List<UIElement> activePrintPages = [];
    private string activePrintTitle = "QR Codes";

    public Task<string> GenerateQrPdfAsync(
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot renderSettings,
        PrintJobSettings printSettings,
        CancellationToken cancellationToken = default)
    {
        PrintJobSettings normalizedSettings = printSettings.Normalize();
        string pdfPath = CreatePreviewPdfPath();

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<PrintableQrCodeAsset> printableCodes = codes.Select(code =>
            {
                string? resolvedFrameText = QrFrameTextResolver.Resolve(
                    renderSettings.FramePreset,
                    renderSettings.FrameTextSource,
                    renderSettings.FrameText,
                    code.CodeAsText,
                    code.ContentKind,
                    code.MultiLineCodeModeOverride);
                using MemoryStream ms = new();
                BarcodeHelpers.SaveQrCodePngToStream(
                    ms,
                    code.CodeAsText,
                    renderSettings.ErrorCorrectionLevel,
                    renderSettings.ForegroundColor,
                    renderSettings.BackgroundColor,
                    renderSettings.LogoImage,
                    renderSettings.LogoSizePercentage,
                    renderSettings.LogoPaddingPixels,
                    renderSettings.QrPaddingModules,
                    renderSettings.FramePreset,
                    resolvedFrameText,
                    out QrImageLayoutMetrics imageLayout);
                return new PrintableQrCodeAsset(code.CodeAsText, ms.ToArray(), imageLayout);
            }).ToList();

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using PdfDocumentBuilder document = new();
                document.DocumentInformation.Title = "QR Codes";
                document.DocumentInformation.Creator = "Simple QR Code Maker";
                PdfLabelFont? labelFont = normalizedSettings.ShowLabels ? new PdfLabelFont(document) : null;

                QrImageLayoutMetrics maximumImageLayout = GetMaximumImageLayout(printableCodes);
                PrintLayoutMetrics pageLayout = PrintLayoutHelper.CreateMetrics(normalizedSettings, maximumImageLayout);
                int pageCount = (int)Math.Ceiling((double)printableCodes.Count / pageLayout.CodesPerPage);

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PdfPageBuilder page = document.AddPage(pageLayout.PageWidthPoints, pageLayout.PageHeightPoints);
                    DrawPage(page, pageIndex, printableCodes, normalizedSettings, maximumImageLayout, labelFont);
                }

                File.WriteAllBytes(pdfPath, document.Build());
                return pdfPath;
            }
            catch
            {
                TryDeleteFile(pdfPath);
                throw;
            }
        }, cancellationToken);
    }

    public async Task PrintPdfAsync(
        string pdfPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("The generated PDF could not be found for printing.", pdfPath);
        }

        if (!PrintManager.IsSupported())
        {
            throw new InvalidOperationException("Printing isn't supported on this device.");
        }

        IntPtr windowHandle = WindowNative.GetWindowHandle(App.MainWindow);

        await printStateGate.WaitAsync(cancellationToken);
        try
        {
            if (activePrintDocument is not null)
            {
                throw new InvalidOperationException("Another print job is already in progress.");
            }

            activePrintTitle = Path.GetFileNameWithoutExtension(pdfPath);
            activePrintBitmaps = await LoadPrintableBitmapsAsync(pdfPath, cancellationToken);
            RegisterForPrinting(windowHandle);

            try
            {
                await PrintManagerInterop.ShowPrintUIForWindowAsync(windowHandle);
            }
            catch
            {
                UnregisterForPrinting();
                throw;
            }
        }
        finally
        {
            printStateGate.Release();
        }
    }

    private static void DrawPage(
        PdfPageBuilder page,
        int pageIndex,
        IReadOnlyList<PrintableQrCodeAsset> printableCodes,
        PrintJobSettings settings,
        QrImageLayoutMetrics maximumImageLayout,
        PdfLabelFont? labelFont)
    {
        PrintLayoutMetrics pageLayout = PrintLayoutHelper.CreateMetrics(page.PageSize.Width, page.PageSize.Height, settings, maximumImageLayout);
        page.SetStrokeColor(255, 255, 255);
        page.SetTextAndFillColor(255, 255, 255);
        page.DrawRectangle(new PdfPoint(0, 0), page.PageSize.Width, page.PageSize.Height, 0, fill: true);
        page.ResetColor();

        int start = pageIndex * pageLayout.CodesPerPage;
        int end = Math.Min(start + pageLayout.CodesPerPage, printableCodes.Count);

        for (int i = start; i < end; i++)
        {
            int local = i - start;
            Rect cellRect = new(
                pageLayout.MarginPoints + ((local % pageLayout.Columns) * (pageLayout.CellWidth + pageLayout.SpacingPoints)),
                pageLayout.MarginPoints + ((local / pageLayout.Columns) * (pageLayout.CellHeight + pageLayout.SpacingPoints)),
                pageLayout.CellWidth,
                pageLayout.CellHeight);

            DrawCell(page, cellRect, printableCodes[i], settings, labelFont);
        }
    }

    private static void DrawCell(PdfPageBuilder page, Rect cellRect, PrintableQrCodeAsset printableCode, PrintJobSettings settings, PdfLabelFont? labelFont)
    {
        Rect paddedRect = new(
            cellRect.X + PrintLayoutHelper.CellPaddingPoints,
            cellRect.Y + PrintLayoutHelper.CellPaddingPoints,
            Math.Max(cellRect.Width - (PrintLayoutHelper.CellPaddingPoints * 2), 1),
            Math.Max(cellRect.Height - (PrintLayoutHelper.CellPaddingPoints * 2), 1));

        double reservedLabelHeight = settings.ShowLabels ? PrintLayoutHelper.LabelHeightPoints + PrintLayoutHelper.LabelSpacingPoints : 0;
        double imageAreaHeight = Math.Max(paddedRect.Height - reservedLabelHeight, 1);
        PrintCodePlacement placement = PrintLayoutHelper.CalculateCodePlacement(
            cellRect.Width,
            cellRect.Height,
            settings,
            printableCode.ImageLayout);
        double imageX = paddedRect.X + ((paddedRect.Width - placement.ImageWidthPoints) / 2);
        double imageY = paddedRect.Y + ((imageAreaHeight - placement.ImageHeightPoints) / 2);

        // Layout uses top-left coordinates; PDF coordinates start at the bottom-left.
        double imageBottom = page.PageSize.Height - imageY - placement.ImageHeightPoints;
        page.AddPng(printableCode.PngBytes, new PdfRectangle(
            imageX,
            imageBottom,
            imageX + placement.ImageWidthPoints,
            imageBottom + placement.ImageHeightPoints));

        if (labelFont is null)
            return;

        Rect labelRect = new(
            paddedRect.X,
            paddedRect.Bottom - PrintLayoutHelper.LabelHeightPoints,
            paddedRect.Width,
            PrintLayoutHelper.LabelHeightPoints);

        DrawWrappedLabel(page, printableCode.Label, labelRect, labelFont);
    }

    private static void DrawWrappedLabel(PdfPageBuilder page, string value, Rect labelRect, PdfLabelFont labelFont)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        double fontSize = GetLabelFontSize(page, value, labelFont, labelRect.Width);
        List<string> lines = WrapLabelLines(
            page,
            value,
            labelFont,
            fontSize,
            labelRect.Width,
            PrintLayoutHelper.LabelMaxLineCount,
            out _);
        double lineHeight = fontSize * PrintLayoutHelper.LabelLineHeightMultiplier;
        double totalTextHeight = lines.Count * lineHeight;
        double top = labelRect.Y + Math.Max((labelRect.Height - totalTextHeight) / 2, 0);

        foreach (string line in lines)
        {
            IReadOnlyList<Letter> letters = labelFont.MeasureText(page, line, fontSize);
            double width = GetTextWidth(letters);
            double glyphCenter = (letters.Max(letter => letter.BoundingBox.Top) + letters.Min(letter => letter.BoundingBox.Bottom)) / 2;
            PdfPoint baseline = new(
                labelRect.X + ((labelRect.Width - width) / 2),
                page.PageSize.Height - top - (lineHeight / 2) - glyphCenter);
            labelFont.DrawText(page, line, fontSize, baseline);
            top += lineHeight;
        }
    }

    private static double GetLabelFontSize(PdfPageBuilder page, string value, PdfLabelFont font, double availableWidth)
    {
        for (double fontSize = PrintLayoutHelper.LabelFontSizePoints;
             fontSize >= PrintLayoutHelper.MinimumLabelFontSizePoints;
             fontSize -= 0.5)
        {
            _ = WrapLabelLines(
                page,
                value,
                font,
                fontSize,
                availableWidth,
                PrintLayoutHelper.LabelMaxLineCount,
                out bool fitsCompletely);

            if (fitsCompletely)
                return fontSize;
        }

        return PrintLayoutHelper.MinimumLabelFontSizePoints;
    }

    private static List<string> WrapLabelLines(
        PdfPageBuilder page,
        string value,
        PdfLabelFont font,
        double fontSize,
        double availableWidth,
        int maxLineCount,
        out bool fitsCompletely)
    {
        string normalized = value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        List<string> lines = [];

        if (string.IsNullOrWhiteSpace(normalized))
        {
            fitsCompletely = true;
            return lines;
        }

        int start = 0;
        while (start < normalized.Length && lines.Count < maxLineCount)
        {
            while (start < normalized.Length && char.IsWhiteSpace(normalized[start]))
                start++;

            if (start >= normalized.Length)
                break;

            int nextBreak = FindWrapBreakIndex(page, normalized, start, font, fontSize, availableWidth);
            string line = normalized[start..nextBreak].Trim();
            if (line.Length == 0)
            {
                nextBreak = Math.Min(start + 1, normalized.Length);
                line = normalized[start..nextBreak];
            }

            lines.Add(line);
            start = nextBreak;
        }

        while (start < normalized.Length && char.IsWhiteSpace(normalized[start]))
            start++;

        fitsCompletely = start >= normalized.Length;
        return lines;
    }

    private static int FindWrapBreakIndex(
        PdfPageBuilder page,
        string value,
        int start,
        PdfLabelFont font,
        double fontSize,
        double availableWidth)
    {
        int lastPreferredBreak = -1;

        for (int index = start + 1; index <= value.Length; index++)
        {
            string candidate = value[start..index].TrimEnd();
            if (candidate.Length == 0)
                continue;

            if (GetTextWidth(font.MeasureText(page, candidate, fontSize)) <= availableWidth)
            {
                if (index < value.Length && IsPreferredWrapBoundary(value[index - 1]))
                    lastPreferredBreak = index;

                continue;
            }

            if (lastPreferredBreak > start)
                return lastPreferredBreak;

            return Math.Max(start + 1, index - 1);
        }

        return value.Length;
    }

    private static bool IsPreferredWrapBoundary(char character)
    {
        return char.IsWhiteSpace(character)
            || character is '-' or '_' or '/' or '\\' or '.' or '?' or '&' or '=';
    }

    private void RegisterForPrinting(IntPtr windowHandle)
    {
        activePrintManager = PrintManagerInterop.GetForWindow(windowHandle);
        activePrintManager.PrintTaskRequested += OnPrintTaskRequested;

        activePrintDocument = new PrintDocument();
        activePrintDocumentSource = activePrintDocument.DocumentSource;
        activePrintDocument.Paginate += OnPrintDocumentPaginate;
        activePrintDocument.GetPreviewPage += OnPrintDocumentGetPreviewPage;
        activePrintDocument.AddPages += OnPrintDocumentAddPages;
    }

    private void UnregisterForPrinting()
    {
        if (activePrintDocument is not null)
        {
            activePrintDocument.Paginate -= OnPrintDocumentPaginate;
            activePrintDocument.GetPreviewPage -= OnPrintDocumentGetPreviewPage;
            activePrintDocument.AddPages -= OnPrintDocumentAddPages;
            activePrintDocument = null;
        }

        if (activePrintManager is not null)
        {
            activePrintManager.PrintTaskRequested -= OnPrintTaskRequested;
            activePrintManager = null;
        }

        activePrintDocumentSource = null;
        activePrintBitmaps.Clear();
        activePrintPages.Clear();
        activePrintTitle = "QR Codes";
    }

    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        PrintTask printTask = args.Request.CreatePrintTask(activePrintTitle, OnPrintTaskSourceRequested);
        printTask.Completed += OnPrintTaskCompleted;
    }

    private void OnPrintTaskSourceRequested(PrintTaskSourceRequestedArgs args)
    {
        if (activePrintDocumentSource is null)
        {
            throw new InvalidOperationException("The print document source is unavailable.");
        }

        args.SetSource(activePrintDocumentSource);
    }

    private void OnPrintTaskCompleted(PrintTask sender, PrintTaskCompletedEventArgs args)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                UnregisterForPrinting();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clean up the print job: {ex.Message}");
            }
        });
    }

    private void OnPrintDocumentPaginate(object sender, PaginateEventArgs e)
    {
        if (activePrintDocument is null)
        {
            return;
        }

        PrintPageDescription pageDescription = e.PrintTaskOptions.GetPageDescription(0);
        activePrintPages = CreatePrintPages(activePrintBitmaps, pageDescription);
        activePrintDocument.SetPreviewPageCount(activePrintPages.Count, PreviewPageCountType.Final);
    }

    private void OnPrintDocumentGetPreviewPage(object sender, GetPreviewPageEventArgs e)
    {
        if (activePrintDocument is null)
        {
            return;
        }

        int pageIndex = e.PageNumber - 1;
        if (pageIndex < 0 || pageIndex >= activePrintPages.Count)
        {
            return;
        }

        activePrintDocument.SetPreviewPage(e.PageNumber, activePrintPages[pageIndex]);
    }

    private void OnPrintDocumentAddPages(object sender, AddPagesEventArgs e)
    {
        if (activePrintDocument is null)
        {
            return;
        }

        foreach (UIElement page in activePrintPages)
        {
            activePrintDocument.AddPage(page);
        }

        activePrintDocument.AddPagesComplete();
    }

    private static string CreatePreviewPdfPath()
    {
        string previewDirectory = Path.Combine(Path.GetTempPath(), "Simple_QR_Code_Maker", "PrintPreview");
        Directory.CreateDirectory(previewDirectory);
        return Path.Combine(previewDirectory, $"qr-print-{DateTimeOffset.UtcNow.Ticks}.pdf");
    }

    private static double GetTextWidth(IReadOnlyList<Letter> letters)
    {
        return letters.Count == 0 ? 0 : letters[^1].EndBaseLine.X;
    }

    private sealed class PdfLabelFont
    {
        private readonly PdfDocumentBuilder document;
        private readonly byte[] fontBytes;
        private readonly HashSet<char> characters = [];
        private PdfDocumentBuilder.AddedFont font;

        internal PdfLabelFont(PdfDocumentBuilder document)
        {
            this.document = document;
            fontBytes = File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"));
            font = document.AddTrueTypeFont(fontBytes);
        }

        internal IReadOnlyList<Letter> MeasureText(PdfPageBuilder page, string text, double fontSize)
        {
            return page.MeasureText(text, fontSize, new PdfPoint(0, 0), font);
        }

        internal void DrawText(PdfPageBuilder page, string text, double fontSize, PdfPoint baseline)
        {
            int start = 0;
            for (int index = 0; index < text.Length; index++)
            {
                if (characters.Contains(text[index]))
                    continue;

                // PdfPig's simple TrueType fonts can encode only 255 distinct characters.
                if (characters.Count == byte.MaxValue)
                {
                    string run = text[start..index];
                    page.AddText(run, fontSize, baseline, font);
                    baseline = new PdfPoint(baseline.X + GetTextWidth(MeasureText(page, run, fontSize)), baseline.Y);
                    font = document.AddTrueTypeFont(fontBytes);
                    characters.Clear();
                    start = index;
                }

                characters.Add(text[index]);
            }

            page.AddText(text[start..], fontSize, baseline, font);
        }
    }

    private static async Task<List<BitmapImage>> LoadPrintableBitmapsAsync(string pdfPath, CancellationToken cancellationToken)
    {
        StorageFile pdfFile = await StorageFile.GetFileFromPathAsync(pdfPath);
        WinPdfDocument pdfDocument = await WinPdfDocument.LoadFromFileAsync(pdfFile);
        List<BitmapImage> bitmaps = [];

        for (uint index = 0; index < pdfDocument.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using WinPdfPage page = pdfDocument.GetPage(index);
            using InMemoryRandomAccessStream stream = new();

            (uint destinationWidth, uint destinationHeight) = GetPrintRenderSize(page);
            Windows.Data.Pdf.PdfPageRenderOptions renderOptions = new()
            {
                DestinationWidth = destinationWidth,
                DestinationHeight = destinationHeight,
                BackgroundColor = new Color { A = 255, R = 255, G = 255, B = 255 },
            };

            await page.RenderToStreamAsync(stream, renderOptions).AsTask(cancellationToken);
            stream.Seek(0);

            BitmapImage bitmap = new();
            await bitmap.SetSourceAsync(stream);
            bitmaps.Add(bitmap);
        }

        return bitmaps;
    }

    private static List<UIElement> CreatePrintPages(IReadOnlyList<BitmapImage> pageBitmaps, PrintPageDescription pageDescription)
    {
        List<UIElement> pages = new(pageBitmaps.Count);

        foreach (BitmapImage bitmap in pageBitmaps)
        {
            Grid pageRoot = new()
            {
                Width = pageDescription.PageSize.Width,
                Height = pageDescription.PageSize.Height,
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            };

            Image pageImage = new()
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            pageRoot.Children.Add(pageImage);
            pages.Add(pageRoot);
        }

        return pages;
    }

    private static (uint Width, uint Height) GetPrintRenderSize(WinPdfPage page)
    {
        double width = Math.Max(page.Size.Width, 1);
        double height = Math.Max(page.Size.Height, 1);

        if (width >= height)
        {
            uint renderedHeight = (uint)Math.Max(1, Math.Round(PrintRenderLongEdgePixels * (height / width)));
            return (PrintRenderLongEdgePixels, renderedHeight);
        }

        uint renderedWidth = (uint)Math.Max(1, Math.Round(PrintRenderLongEdgePixels * (width / height)));
        return (renderedWidth, PrintRenderLongEdgePixels);
    }

    private static QrImageLayoutMetrics GetMaximumImageLayout(IReadOnlyList<PrintableQrCodeAsset> printableCodes)
    {
        if (printableCodes.Count == 0)
        {
            return QrImageLayoutMetrics.Square;
        }

        double maxWidthPerQrSize = printableCodes.Max(static code => code.ImageLayout.WidthPerQrSize);
        double maxHeightPerQrSize = printableCodes.Max(static code => code.ImageLayout.HeightPerQrSize);
        return QrImageLayoutMetrics.FromScaleProfile(maxWidthPerQrSize, maxHeightPerQrSize);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete print preview file '{path}': {ex.Message}");
        }
    }
}
