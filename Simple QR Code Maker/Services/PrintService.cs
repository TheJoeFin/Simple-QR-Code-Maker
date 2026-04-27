using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Diagnostics;
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
    private static readonly object FontSettingsGate = new();
    private static bool fontSettingsInitialized;
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
            EnsurePdfSharpFontSettings();

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
                using PdfSharp.Pdf.PdfDocument document = new();
                document.Info.Title = "QR Codes";
                document.Info.Creator = "Simple QR Code Maker";

                PageSize pageSize = PrintLayoutHelper.GetPageSize(normalizedSettings.PageType);
                QrImageLayoutMetrics maximumImageLayout = GetMaximumImageLayout(printableCodes);
                PrintLayoutMetrics pageLayout = PrintLayoutHelper.CreateMetrics(normalizedSettings, maximumImageLayout);
                int pageCount = (int)Math.Ceiling((double)printableCodes.Count / pageLayout.CodesPerPage);

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PdfPage page = document.AddPage();
                    page.Size = pageSize;
                    page.Orientation = normalizedSettings.PageLayout == PrintPageLayout.Landscape
                        ? PageOrientation.Landscape
                        : PageOrientation.Portrait;
                    DrawPage(page, pageIndex, printableCodes, normalizedSettings, maximumImageLayout);
                }

                document.Save(pdfPath);
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
        PdfPage page,
        int pageIndex,
        IReadOnlyList<PrintableQrCodeAsset> printableCodes,
        PrintJobSettings settings,
        QrImageLayoutMetrics maximumImageLayout)
    {
        PrintLayoutMetrics pageLayout = PrintLayoutHelper.CreateMetrics(page.Width.Point, page.Height.Point, settings, maximumImageLayout);

        using XGraphics graphics = XGraphics.FromPdfPage(page);
        graphics.DrawRectangle(XBrushes.White, 0, 0, page.Width.Point, page.Height.Point);

        int start = pageIndex * pageLayout.CodesPerPage;
        int end = Math.Min(start + pageLayout.CodesPerPage, printableCodes.Count);

        for (int i = start; i < end; i++)
        {
            int local = i - start;
            XRect cellRect = new(
                pageLayout.MarginPoints + ((local % pageLayout.Columns) * (pageLayout.CellWidth + pageLayout.SpacingPoints)),
                pageLayout.MarginPoints + ((local / pageLayout.Columns) * (pageLayout.CellHeight + pageLayout.SpacingPoints)),
                pageLayout.CellWidth,
                pageLayout.CellHeight);

            DrawCell(graphics, cellRect, printableCodes[i], settings);
        }
    }

    private static void DrawCell(XGraphics graphics, XRect cellRect, PrintableQrCodeAsset printableCode, PrintJobSettings settings)
    {
        XRect paddedRect = new(
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

        using MemoryStream imageStream = new(printableCode.PngBytes, writable: false);
        using XImage image = XImage.FromStream(imageStream);
        graphics.DrawImage(image, imageX, imageY, placement.ImageWidthPoints, placement.ImageHeightPoints);

        if (!settings.ShowLabels)
            return;

        XRect labelRect = new(
            paddedRect.X,
            paddedRect.Bottom - PrintLayoutHelper.LabelHeightPoints,
            paddedRect.Width,
            PrintLayoutHelper.LabelHeightPoints);

        DrawWrappedLabel(graphics, printableCode.Label, labelRect);
    }

    private static void DrawWrappedLabel(XGraphics graphics, string value, XRect labelRect)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        double fontSize = GetLabelFontSize(graphics, value, labelRect.Width);
        XFont labelFont = new("Arial", fontSize);
        List<string> lines = WrapLabelLines(
            graphics,
            value,
            labelFont,
            labelRect.Width,
            PrintLayoutHelper.LabelMaxLineCount,
            out _);
        double lineHeight = fontSize * PrintLayoutHelper.LabelLineHeightMultiplier;
        double totalTextHeight = lines.Count * lineHeight;
        double top = labelRect.Y + Math.Max((labelRect.Height - totalTextHeight) / 2, 0);

        foreach (string line in lines)
        {
            XRect lineRect = new(labelRect.X, top, labelRect.Width, lineHeight);
            graphics.DrawString(line, labelFont, XBrushes.Black, lineRect, XStringFormats.Center);
            top += lineHeight;
        }
    }

    private static double GetLabelFontSize(XGraphics graphics, string value, double availableWidth)
    {
        for (double fontSize = PrintLayoutHelper.LabelFontSizePoints;
             fontSize >= PrintLayoutHelper.MinimumLabelFontSizePoints;
             fontSize -= 0.5)
        {
            XFont font = new("Arial", fontSize);
            _ = WrapLabelLines(
                graphics,
                value,
                font,
                availableWidth,
                PrintLayoutHelper.LabelMaxLineCount,
                out bool fitsCompletely);

            if (fitsCompletely)
                return fontSize;
        }

        return PrintLayoutHelper.MinimumLabelFontSizePoints;
    }

    private static List<string> WrapLabelLines(
        XGraphics graphics,
        string value,
        XFont font,
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

            int nextBreak = FindWrapBreakIndex(graphics, normalized, start, font, availableWidth);
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
        XGraphics graphics,
        string value,
        int start,
        XFont font,
        double availableWidth)
    {
        int lastPreferredBreak = -1;

        for (int index = start + 1; index <= value.Length; index++)
        {
            string candidate = value[start..index].TrimEnd();
            if (candidate.Length == 0)
                continue;

            if (graphics.MeasureString(candidate, font).Width <= availableWidth)
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

    private static void EnsurePdfSharpFontSettings()
    {
        if (fontSettingsInitialized)
        {
            return;
        }

        lock (FontSettingsGate)
        {
            if (fontSettingsInitialized)
            {
                return;
            }

            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            fontSettingsInitialized = true;
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
