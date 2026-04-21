using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Printing;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Simple_QR_Code_Maker.Services;

public class PrintService : IPrintService
{
    public async Task PrintQrCodesAsync(
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot renderSettings,
        PrintJobSettings printSettings)
    {
        // Render QR codes to PNG bytes on a background thread
        List<byte[]> pngBytes = await Task.Run(() =>
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

        // Load PNG bytes as BitmapImage on the UI thread
        List<BitmapImage> bitmapImages = [];
        foreach (byte[] bytes in pngBytes)
        {
            BitmapImage bitmapImage = new();
            using InMemoryRandomAccessStream stream = new();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            await bitmapImage.SetSourceAsync(stream);
            bitmapImages.Add(bitmapImage);
        }

        // A Popup connected to the XamlRoot gives off-screen elements
        // a real compositor-tree slot so the print system can render them.
        Canvas stagingCanvas = new() { Opacity = 0, IsHitTestVisible = false };
        Popup stagingPopup = new()
        {
            Child = stagingCanvas,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            IsOpen = true,
        };

        PrintDocument printDocument = new();
        IPrintDocumentSource printDocumentSource = printDocument.DocumentSource;
        PrintPageDescription pageDescription = default;
        int pageCount = 0;
        Dictionary<int, Canvas> pageCache = [];

        void ClearCache()
        {
            stagingCanvas.Children.Clear();
            pageCache.Clear();
        }

        Canvas GetOrBuildPage(int pageIndex)
        {
            if (!pageCache.TryGetValue(pageIndex, out Canvas? page))
            {
                page = BuildPrintPage(pageIndex, pageDescription, printSettings, bitmapImages, codes);
                stagingCanvas.Children.Add(page);
                stagingCanvas.UpdateLayout();
                pageCache[pageIndex] = page;
            }
            return page;
        }

        void OnPaginate(object sender, PaginateEventArgs e)
        {
            pageDescription = e.PrintTaskOptions.GetPageDescription(0);
            pageCount = (int)Math.Ceiling((double)bitmapImages.Count / printSettings.CodesPerPage);
            ClearCache();
            printDocument.SetPreviewPageCount(pageCount, PreviewPageCountType.Final);
        }

        void OnGetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            Canvas page = GetOrBuildPage(e.PageNumber - 1);
            printDocument.SetPreviewPage(e.PageNumber, page);
        }

        void OnAddPages(object sender, AddPagesEventArgs e)
        {
            ClearCache();
            for (int i = 0; i < pageCount; i++)
            {
                Canvas page = GetOrBuildPage(i);
                printDocument.AddPage(page);
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
            args.Request.CreatePrintTask("QR Codes", sourceRequestedArgs =>
                sourceRequestedArgs.SetSource(printDocumentSource));
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
            stagingPopup.IsOpen = false;
            stagingCanvas.Children.Clear();
        }
    }

    private static Canvas BuildPrintPage(
        int pageIndex,
        PrintPageDescription pageDescription,
        PrintJobSettings printSettings,
        IList<BitmapImage> bitmapImages,
        IReadOnlyList<RequestedQrCodeItem> codes)
    {
        double pageWidth = pageDescription.PageSize.Width;
        double pageHeight = pageDescription.PageSize.Height;
        double marginX = pageDescription.ImageableRect.X;
        double marginY = pageDescription.ImageableRect.Y;
        double printableWidth = pageDescription.ImageableRect.Width;
        double printableHeight = pageDescription.ImageableRect.Height;

        double userMarginPx = printSettings.MarginMm * 96.0 / 25.4;

        double usableLeft = marginX + userMarginPx;
        double usableTop = marginY + userMarginPx;
        double usableWidth = Math.Max(printableWidth - 2 * userMarginPx, 1);
        double usableHeight = Math.Max(printableHeight - 2 * userMarginPx, 1);

        (int rows, int cols) = GetGridDimensions(printSettings.CodesPerPage);

        Canvas page = new()
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = new SolidColorBrush(Colors.White),
        };

        Grid codesGrid = new()
        {
            Width = usableWidth,
            Height = usableHeight,
        };
        for (int r = 0; r < rows; r++)
            codesGrid.RowDefinitions.Add(new RowDefinition());
        for (int c = 0; c < cols; c++)
            codesGrid.ColumnDefinitions.Add(new ColumnDefinition());

        Canvas.SetLeft(codesGrid, usableLeft);
        Canvas.SetTop(codesGrid, usableTop);
        page.Children.Add(codesGrid);

        int startIndex = pageIndex * printSettings.CodesPerPage;
        int endIndex = Math.Min(startIndex + printSettings.CodesPerPage, bitmapImages.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            int localIndex = i - startIndex;
            int row = localIndex / cols;
            int col = localIndex % cols;

            FrameworkElement cell = BuildCell(bitmapImages[i], codes[i].CodeAsText, printSettings.ShowLabels);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
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
            TextBlock labelBlock = new()
            {
                Text = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(labelBlock, 1);
            cell.Children.Add(labelBlock);
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
