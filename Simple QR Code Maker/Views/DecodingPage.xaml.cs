using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using System.Drawing;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class DecodingPage : Page
{
    public DecodingViewModel ViewModel
    {
        get;
    }

    private bool isPointerOverImage;

    public DecodingPage()
    {
        ViewModel = App.GetService<DecodingViewModel>();
        InitializeComponent();
        AdvancedToolsPanel.ViewModel = ViewModel.AdvancedTools;

        ViewModel.AdvancedTools.ImageProcessed += AdvancedToolsViewModel_ImageProcessed;
        ViewModel.AdvancedTools.PropertyChanged += AdvancedToolsViewModel_PropertyChanged;
        ViewModel.AdvancedTools.PerspectiveCornersClearedRequested += AdvancedToolsViewModel_PerspectiveCornersClearedRequested;
        ViewModel.PreviewStateChanged += ViewModel_PreviewStateChanged;
        Loaded += DecodingPage_Loaded;
    }

    private void DecodingPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateBaseGridLayout();
    }

    private void AdvancedToolsViewModel_PerspectiveCornersClearedRequested(object? sender, EventArgs e)
    {
        if (ViewModel.CurrentDecodingItem != null)
        {
            ViewModel.CurrentDecodingItem.PerspectiveCornerMarkers.Clear();
            ViewModel.CurrentDecodingItem.CurrentCornerIndex = 0;
        }
    }

    private void AdvancedToolsViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdvancedToolsViewModel.IsAnyToolActive))
            UpdateCursorForModes();
    }

    private void UpdateCursorForModes()
    {
        bool isToolActive = ViewModel.AdvancedTools.IsAnyToolActive;

        if (isToolActive && isPointerOverImage)
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Cross);
        else
            this.ProtectedCursor = null;
    }

    private void ImageContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        isPointerOverImage = true;
        UpdateCursorForModes();
    }

    private void ImageContainer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        isPointerOverImage = false;
        UpdateCursorForModes();
    }

    private async void OpenNewFileButton_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenNewFileCommand.ExecuteAsync(null);

    private async void PasteImageButton_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenFileFromClipboardCommand.ExecuteAsync(null);

    private async void AdvancedToolsViewModel_ImageProcessed(object? sender, ImageMagick.MagickImage e)
    {
        if (ViewModel.CurrentDecodingItem != null)
        {
            if (ViewModel.AdvancedTools.IsPerspectiveCorrectionMode &&
                ViewModel.AdvancedTools.IsCornerSelectionComplete)
                ViewModel.AdvancedTools.IsPerspectiveCorrectionMode = false;

            ViewModel.CurrentDecodingItem.ProcessedMagickImage = e;
            ViewModel.CurrentDecodingItem.OriginalMagickImage = (ImageMagick.MagickImage)e.Clone();

            await ViewModel.ApplyAdvancedToolsAndRedecodeCommand.ExecuteAsync(ViewModel.CurrentDecodingItem);
        }
    }

    private void DropContainer_DragOver(object sender, DragEventArgs e)
    {
        DataPackageView dataView = e.DataView;

        if (dataView.Contains(StandardDataFormats.Bitmap))
            e.AcceptedOperation = DataPackageOperation.Copy;
        else if (dataView.Contains(StandardDataFormats.Uri))
            e.AcceptedOperation = DataPackageOperation.Copy;
        else if (dataView.Contains(StandardDataFormats.StorageItems))
            e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void DropContainer_Drop(object sender, DragEventArgs e)
    {
        DragOperationDeferral def = e.GetDeferral();
        e.Handled = true;
        def.Complete();

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();
            ViewModel.OpenAndDecodeStorageFiles(storageItems);
        }
    }

    private void ImageContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid imageContainer || ViewModel.CurrentDecodingItem is null)
            return;

        double displayedImageWidth = ImageWithBarcodes.ActualWidth;
        double displayedImageHeight = ImageWithBarcodes.ActualHeight;

        if (displayedImageWidth <= 0 || displayedImageHeight <= 0)
            return;

        Windows.Foundation.Point clickPoint = e.GetCurrentPoint(imageContainer).Position;

        int imageWidth = ViewModel.CurrentDecodingItem.ImagePixelWidth;
        int imageHeight = ViewModel.CurrentDecodingItem.ImagePixelHeight;

        double scaleX = imageWidth / displayedImageWidth;
        double scaleY = imageHeight / displayedImageHeight;

        double imageX = clickPoint.X * scaleX;
        double imageY = clickPoint.Y * scaleY;

        int pixelX = Math.Clamp((int)Math.Round(imageX), 0, imageWidth - 1);
        int pixelY = Math.Clamp((int)Math.Round(imageY), 0, imageHeight - 1);

        Point imagePoint = new(pixelX, pixelY);

        if (ViewModel.AdvancedTools.IsPerspectiveCorrectionMode)
        {
            HandlePerspectiveCorrectionClick(imagePoint, clickPoint);
            return;
        }

        if (ViewModel.AdvancedTools.IsEyedropperBlackMode ||
            ViewModel.AdvancedTools.IsEyedropperWhiteMode)
            ViewModel.AdvancedTools.SetColorFromPoint(imagePoint);
    }

    private void HandlePerspectiveCorrectionClick(
        Point imagePoint,
        Windows.Foundation.Point displayPoint)
    {
        if (ViewModel.CurrentDecodingItem == null)
            return;

        DecodingImageItem item = ViewModel.CurrentDecodingItem;
        int cornerIndex = item.CurrentCornerIndex;

        if (cornerIndex >= 4)
            return;

        ViewModel.AdvancedTools.SetCornerPoint(imagePoint, cornerIndex);

        Grid marker = CreateCornerMarkerWithLabel(displayPoint, cornerIndex);
        item.PerspectiveCornerMarkers.Add(marker);

        if (cornerIndex > 0)
        {
            UIElement? previousMarker = null;
            for (int i = item.PerspectiveCornerMarkers.Count - 2; i >= 0; i--)
            {
                if (item.PerspectiveCornerMarkers[i] is Grid)
                {
                    previousMarker = item.PerspectiveCornerMarkers[i];
                    break;
                }
            }

            if (previousMarker != null)
            {
                Line line = CreateConnectingLine(previousMarker, marker);
                item.PerspectiveCornerMarkers.Add(line);
            }
        }

        if (cornerIndex == 3)
        {
            UIElement firstMarker = item.PerspectiveCornerMarkers[0];
            Line line = CreateConnectingLine(marker, firstMarker);
            item.PerspectiveCornerMarkers.Add(line);
        }

        item.CurrentCornerIndex++;
    }

    private static Grid CreateCornerMarkerWithLabel(Windows.Foundation.Point position, int cornerIndex)
    {
        Grid markerGrid = new()
        {
            Width = 32,
            Height = 32,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };

        Ellipse marker = new()
        {
            Width = 32,
            Height = 32,
            Fill = new SolidColorBrush(Microsoft.UI.Colors.Red),
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
            StrokeThickness = 3,
            Opacity = 0.9
        };

        TextBlock label = new()
        {
            Text = (cornerIndex + 1).ToString(),
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        markerGrid.Children.Add(marker);
        markerGrid.Children.Add(label);

        Canvas.SetLeft(markerGrid, position.X - 16);
        Canvas.SetTop(markerGrid, position.Y - 16);
        Canvas.SetZIndex(markerGrid, 100);

        return markerGrid;
    }

    private static Line CreateConnectingLine(UIElement fromMarker, UIElement toMarker)
    {
        double fromX = Canvas.GetLeft(fromMarker) + 16;
        double fromY = Canvas.GetTop(fromMarker) + 16;
        double toX = Canvas.GetLeft(toMarker) + 16;
        double toY = Canvas.GetTop(toMarker) + 16;

        Line line = new()
        {
            X1 = fromX,
            Y1 = fromY,
            X2 = toX,
            Y2 = toY,
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.Red),
            StrokeThickness = 2,
            Opacity = 0.7
        };

        Canvas.SetZIndex(line, 50);

        return line;
    }

    private void ViewModel_PreviewStateChanged(object? sender, EventArgs e)
    {
        UpdateBaseGridLayout();

        if (ViewModel.HasImage)
            return;

        isPointerOverImage = false;
        this.ProtectedCursor = null;
        ImageScrollView.ZoomTo(1, null);
        ImageScrollView.ScrollTo(0, 0);
    }

    private void ActionButtonsPanel_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateBaseGridLayout();

    private void UpdateBaseGridLayout()
    {
        ImageRow.Height = ViewModel.HasImage
            ? new GridLength(1, GridUnitType.Star)
            : GridLength.Auto;

        TopSpacerRow.Height = ViewModel.HasImage
            ? new GridLength(0)
            : new GridLength(ActionButtonsPanel.ActualHeight);
    }
}
