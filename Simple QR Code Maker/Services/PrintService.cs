using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Printing;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Simple_QR_Code_Maker.Services;

public class PrintService : IPrintService
{
    // A Canvas that lives permanently in ShellPage's root Grid.
    // Must be in the live visual tree (not moved off-screen) so the compositor
    // allocates real resources for pages added to it — exactly the pattern used
    // by the official Windows printing SDK sample (PrintHelper.PrintCanvas).
    private Canvas? _printCanvas;

    private Canvas EnsurePrintCanvas()
    {
        if (_printCanvas is not null)
            return _printCanvas;

        // Opacity near-zero: compositor keeps the visual alive, user sees nothing.
        _printCanvas = new Canvas { Opacity = 0.001, IsHitTestVisible = false };

        if (App.MainWindow.Content is Page { Content: Panel rootPanel })
            rootPanel.Children.Add(_printCanvas);

        return _printCanvas;
    }

    public async Task PrintQrCodesAsync(
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot renderSettings,
        PrintJobSettings printSettings)
    {
        // ── 1. Render PNG bytes on a background thread ────────────────────────
        List<byte[]> pngBytesList = await Task.Run(() =>
            codes.Select(code =>
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
            }).ToList());

        // ── 2. Decode to BitmapImage on UI thread ─────────────────────────────
        List<BitmapImage> bitmapImages = [];
        foreach (byte[] bytes in pngBytesList)
        {
            BitmapImage bmi = new();
            using InMemoryRandomAccessStream stream = new();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            await bmi.SetSourceAsync(stream);
            bitmapImages.Add(bmi);
        }

        // ── 3. Ensure live-tree print canvas exists ───────────────────────────
        Canvas printCanvas = EnsurePrintCanvas();

        // ── 4. Wire PrintDocument ─────────────────────────────────────────────
        PrintDocument printDocument = new();
        IPrintDocumentSource printDocumentSource = printDocument.DocumentSource;

        // Pages are built in Paginate (same as the official SDK sample) and
        // cached here so GetPreviewPage and AddPages can reuse them without rebuild.
        List<Canvas> previewPages = [];

        void OnPaginate(object sender, PaginateEventArgs e)
        {
            // Mirrors PrintHelper.CreatePrintPreviewPages from the SDK sample.
            lock (previewPages)
            {
                previewPages.Clear();
                printCanvas.Children.Clear();

                PrintPageDescription desc = e.PrintTaskOptions.GetPageDescription(0);
                double pw = desc.PageSize.Width;
                double ph = desc.PageSize.Height;
                double il = desc.ImageableRect.X;
                double it = desc.ImageableRect.Y;
                double iw = desc.ImageableRect.Width;
                double ih = desc.ImageableRect.Height;

                int count = (int)Math.Ceiling((double)bitmapImages.Count / printSettings.CodesPerPage);

                for (int i = 0; i < count; i++)
                {
                    Canvas pg = BuildPrintPage(i, pw, ph, il, it, iw, ih, printSettings, bitmapImages, codes);

                    // Add to live visual tree, then force layout — exact SDK sample pattern.
                    printCanvas.Children.Add(pg);
                    printCanvas.InvalidateMeasure();
                    printCanvas.UpdateLayout();

                    previewPages.Add(pg);
                }

                printDocument.SetPreviewPageCount(previewPages.Count, PreviewPageCountType.Intermediate);
            }
        }

        void OnGetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            lock (previewPages)
            {
                printDocument.SetPreviewPage(e.PageNumber, previewPages[e.PageNumber - 1]);
            }
        }

        void OnAddPages(object sender, AddPagesEventArgs e)
        {
            // Reuse the same pages built in Paginate — exactly as the SDK sample does.
            lock (previewPages)
            {
                foreach (Canvas pg in previewPages)
                    printDocument.AddPage(pg);
            }
            printDocument.AddPagesComplete();
        }

        printDocument.Paginate += OnPaginate;
        printDocument.GetPreviewPage += OnGetPreviewPage;
        printDocument.AddPages += OnAddPages;

        nint hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        PrintManager printManager = PrintManagerInterop.GetForWindow(hwnd);

        void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            args.Request.CreatePrintTask("QR Codes", src => src.SetSource(printDocumentSource));
        }

        printManager.PrintTaskRequested += OnPrintTaskRequested;

        try
        {
            await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
        }
        finally
        {
            printManager.PrintTaskRequested -= OnPrintTaskRequested;
            printDocument.Paginate -= OnPaginate;
            printDocument.GetPreviewPage -= OnGetPreviewPage;
            printDocument.AddPages -= OnAddPages;
            printCanvas.Children.Clear();
        }
    }

    private static Canvas BuildPrintPage(
        int pageIndex,
        double pageWidth, double pageHeight,
        double imgLeft, double imgTop,
        double imgWidth, double imgHeight,
        PrintJobSettings settings,
        IList<BitmapImage> bitmapImages,
        IReadOnlyList<RequestedQrCodeItem> codes)
    {
        double userMargin = settings.MarginMm * 96.0 / 25.4;
        double usableLeft = imgLeft + userMargin;
        double usableTop = imgTop + userMargin;
        double usableW = Math.Max(imgWidth - 2 * userMargin, 1);
        double usableH = Math.Max(imgHeight - 2 * userMargin, 1);

        (int rows, int cols) = GetGridDimensions(settings.CodesPerPage);

        Canvas page = new()
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = new SolidColorBrush(Colors.White),
        };

        Grid codesGrid = new() { Width = usableW, Height = usableH };
        for (int r = 0; r < rows; r++)
            codesGrid.RowDefinitions.Add(new RowDefinition());
        for (int c = 0; c < cols; c++)
            codesGrid.ColumnDefinitions.Add(new ColumnDefinition());

        Canvas.SetLeft(codesGrid, usableLeft);
        Canvas.SetTop(codesGrid, usableTop);
        page.Children.Add(codesGrid);

        int start = pageIndex * settings.CodesPerPage;
        int end = Math.Min(start + settings.CodesPerPage, bitmapImages.Count);

        for (int i = start; i < end; i++)
        {
            int local = i - start;
            FrameworkElement cell = BuildCell(bitmapImages[i], codes[i].CodeAsText, settings.ShowLabels);
            Grid.SetRow(cell, local / cols);
            Grid.SetColumn(cell, local % cols);
            codesGrid.Children.Add(cell);
        }

        return page;
    }

    private static FrameworkElement BuildCell(BitmapImage source, string label, bool showLabel)
    {
        Grid cell = new() { Margin = new Thickness(4) };
        cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        if (showLabel)
            cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24, GridUnitType.Pixel) });

        Image img = new()
        {
            Source = source,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetRow(img, 0);
        cell.Children.Add(img);

        if (showLabel)
        {
            TextBlock tb = new()
            {
                Text = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(tb, 1);
            cell.Children.Add(tb);
        }

        return cell;
    }

    private static (int rows, int cols) GetGridDimensions(int codesPerPage) => codesPerPage switch
    {
        1 => (1, 1),
        2 => (2, 1),
        6 => (3, 2),
        9 => (3, 3),
        12 => (4, 3),
        _ => (2, 2),
    };
}
