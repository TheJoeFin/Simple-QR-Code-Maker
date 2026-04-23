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
using System.Globalization;
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

            List<byte[]> pngBytesList = codes.Select(code =>
            {
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
                    renderSettings.QrPaddingModules);
                return ms.ToArray();
            }).ToList();

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using PdfSharp.Pdf.PdfDocument document = new();
                document.Info.Title = "QR Codes";
                document.Info.Creator = "Simple QR Code Maker";

                PageSize pageSize = PrintLayoutHelper.GetPageSize(normalizedSettings.PageType);
                PrintLayoutMetrics pageLayout = PrintLayoutHelper.CreateMetrics(normalizedSettings);
                int pageCount = (int)Math.Ceiling((double)pngBytesList.Count / pageLayout.CodesPerPage);

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PdfPage page = document.AddPage();
                    page.Size = pageSize;
                    page.Orientation = normalizedSettings.PageLayout == PrintPageLayout.Landscape
                        ? PageOrientation.Landscape
                        : PageOrientation.Portrait;
                    DrawPage(page, pageIndex, pngBytesList, codes, normalizedSettings);
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
        IList<byte[]> qrPngBytes,
        IReadOnlyList<RequestedQrCodeItem> codes,
        PrintJobSettings settings)
    {
        PrintLayoutMetrics pageLayout = PrintLayoutHelper.CreateMetrics(page.Width.Point, page.Height.Point, settings);

        using XGraphics graphics = XGraphics.FromPdfPage(page);
        graphics.DrawRectangle(XBrushes.White, 0, 0, page.Width.Point, page.Height.Point);

        int start = pageIndex * pageLayout.CodesPerPage;
        int end = Math.Min(start + pageLayout.CodesPerPage, qrPngBytes.Count);

        for (int i = start; i < end; i++)
        {
            int local = i - start;
            XRect cellRect = new(
                pageLayout.MarginPoints + ((local % pageLayout.Columns) * (pageLayout.CellWidth + pageLayout.SpacingPoints)),
                pageLayout.MarginPoints + ((local / pageLayout.Columns) * (pageLayout.CellHeight + pageLayout.SpacingPoints)),
                pageLayout.CellWidth,
                pageLayout.CellHeight);

            DrawCell(graphics, cellRect, qrPngBytes[i], codes[i].CodeAsText, settings);
        }
    }

    private static void DrawCell(XGraphics graphics, XRect cellRect, byte[] qrPngBytes, string label, PrintJobSettings settings)
    {
        XRect paddedRect = new(
            cellRect.X + PrintLayoutHelper.CellPaddingPoints,
            cellRect.Y + PrintLayoutHelper.CellPaddingPoints,
            Math.Max(cellRect.Width - (PrintLayoutHelper.CellPaddingPoints * 2), 1),
            Math.Max(cellRect.Height - (PrintLayoutHelper.CellPaddingPoints * 2), 1));

        double reservedLabelHeight = settings.ShowLabels ? PrintLayoutHelper.LabelHeightPoints + PrintLayoutHelper.LabelSpacingPoints : 0;
        double imageAreaHeight = Math.Max(paddedRect.Height - reservedLabelHeight, 1);
        double imageSize = PrintLayoutHelper.CalculateActualCodeSizePoints(cellRect.Width, cellRect.Height, settings);
        double imageX = paddedRect.X + ((paddedRect.Width - imageSize) / 2);
        double imageY = paddedRect.Y + ((imageAreaHeight - imageSize) / 2);

        using MemoryStream imageStream = new(qrPngBytes, writable: false);
        using XImage image = XImage.FromStream(imageStream);
        graphics.DrawImage(image, imageX, imageY, imageSize, imageSize);

        if (!settings.ShowLabels)
            return;

        XFont labelFont = new("Arial", 10);
        XRect labelRect = new(
            paddedRect.X,
            paddedRect.Bottom - PrintLayoutHelper.LabelHeightPoints,
            paddedRect.Width,
            PrintLayoutHelper.LabelHeightPoints);

        string trimmedLabel = TrimTextToWidth(graphics, label, labelFont, labelRect.Width);
        graphics.DrawString(trimmedLabel, labelFont, XBrushes.Black, labelRect, XStringFormats.Center);
    }

    private static string TrimTextToWidth(XGraphics graphics, string value, XFont font, double availableWidth)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (graphics.MeasureString(value, font).Width <= availableWidth)
            return value;

        const string ellipsis = "...";
        for (int length = value.Length - 1; length > 0; length--)
        {
            string candidate = $"{value[..length].TrimEnd()}{ellipsis}";
            if (graphics.MeasureString(candidate, font).Width <= availableWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
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
