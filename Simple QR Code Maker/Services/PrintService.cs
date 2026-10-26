using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using Simple_QR_Code_Maker.Contracts.Services;
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
    private readonly SemaphoreSlim printStateGate = new(1, 1);
    private PrintDocument? activePrintDocument;
    private IPrintDocumentSource? activePrintDocumentSource;
    private PrintManager? activePrintManager;
    private List<BitmapImage> activePrintBitmaps = [];
    private List<UIElement> activePrintPages = [];
    private string activePrintTitle = "QR Codes";

    public async Task<string> GenerateQrPdfAsync(
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot renderSettings,
        PrintJobSettings printSettings,
        CancellationToken cancellationToken = default)
    {
        string pdfPath = CreatePreviewPdfPath();
        try
        {
            byte[] bytes = await QrPdfRenderer.GenerateQrPdfBytesAsync(codes, renderSettings, printSettings, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllBytesAsync(pdfPath, bytes, cancellationToken).ConfigureAwait(false);
            return pdfPath;
        }
        catch
        {
            TryDeleteFile(pdfPath);
            throw;
        }
    }

    public async Task PrintPdfAsync(string pdfPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException("The generated PDF could not be found for printing.", pdfPath);
        if (!PrintManager.IsSupported())
            throw new InvalidOperationException("Printing isn't supported on this device.");

        IntPtr windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
        await printStateGate.WaitAsync(cancellationToken);
        try
        {
            if (activePrintDocument is not null)
                throw new InvalidOperationException("Another print job is already in progress.");

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
            throw new InvalidOperationException("The print document source is unavailable.");
        args.SetSource(activePrintDocumentSource);
    }

    private void OnPrintTaskCompleted(PrintTask sender, PrintTaskCompletedEventArgs args)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try { UnregisterForPrinting(); }
            catch (Exception ex) { Debug.WriteLine($"Failed to clean up the print job: {ex.Message}"); }
        });
    }

    private void OnPrintDocumentPaginate(object sender, PaginateEventArgs e)
    {
        if (activePrintDocument is null)
            return;
        PrintPageDescription description = e.PrintTaskOptions.GetPageDescription(0);
        activePrintPages = CreatePrintPages(activePrintBitmaps, description);
        activePrintDocument.SetPreviewPageCount(activePrintPages.Count, PreviewPageCountType.Final);
    }

    private void OnPrintDocumentGetPreviewPage(object sender, GetPreviewPageEventArgs e)
    {
        if (activePrintDocument is null)
            return;
        int index = e.PageNumber - 1;
        if (index >= 0 && index < activePrintPages.Count)
            activePrintDocument.SetPreviewPage(e.PageNumber, activePrintPages[index]);
    }

    private void OnPrintDocumentAddPages(object sender, AddPagesEventArgs e)
    {
        if (activePrintDocument is null)
            return;
        foreach (UIElement page in activePrintPages)
            activePrintDocument.AddPage(page);
        activePrintDocument.AddPagesComplete();
    }

    private static string CreatePreviewPdfPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Simple_QR_Code_Maker", "PrintPreview");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"qr-print-{DateTimeOffset.UtcNow.Ticks}.pdf");
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
            (uint width, uint height) = GetPrintRenderSize(page);
            Windows.Data.Pdf.PdfPageRenderOptions options = new()
            {
                DestinationWidth = width,
                DestinationHeight = height,
                BackgroundColor = new Color { A = 255, R = 255, G = 255, B = 255 },
            };
            await page.RenderToStreamAsync(stream, options).AsTask(cancellationToken);
            stream.Seek(0);
            BitmapImage bitmap = new();
            await bitmap.SetSourceAsync(stream);
            bitmaps.Add(bitmap);
        }
        return bitmaps;
    }

    private static List<UIElement> CreatePrintPages(IReadOnlyList<BitmapImage> pageBitmaps, PrintPageDescription description)
    {
        List<UIElement> pages = new(pageBitmaps.Count);
        foreach (BitmapImage bitmap in pageBitmaps)
        {
            Grid root = new()
            {
                Width = description.PageSize.Width,
                Height = description.PageSize.Height,
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            };
            Image image = new()
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            root.Children.Add(image);
            pages.Add(root);
        }
        return pages;
    }

    private static (uint Width, uint Height) GetPrintRenderSize(WinPdfPage page)
    {
        double width = Math.Max(page.Size.Width, 1);
        double height = Math.Max(page.Size.Height, 1);
        if (width >= height)
            return (PrintRenderLongEdgePixels, (uint)Math.Max(1, Math.Round(PrintRenderLongEdgePixels * height / width)));
        return ((uint)Math.Max(1, Math.Round(PrintRenderLongEdgePixels * width / height)), PrintRenderLongEdgePixels);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete print preview file '{path}': {ex.Message}");
        }
    }
}