using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Simple_QR_Code_Maker.Helpers;
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
    private bool _isDraggingCutOut;
    private Windows.Foundation.Point _cutOutCanvasStart;
    private Windows.Foundation.Point _cutOutCanvasEnd;

    public DecodingPage()
    {
        ViewModel = App.GetService<DecodingViewModel>();
        InitializeComponent();
        AdvancedToolsPanel.ViewModel = ViewModel.AdvancedTools;

        ViewModel.AdvancedTools.ImageProcessed += AdvancedToolsViewModel_ImageProcessed;
        ViewModel.AdvancedTools.PropertyChanged += AdvancedToolsViewModel_PropertyChanged;
        ViewModel.AdvancedTools.PerspectiveCornersClearedRequested += AdvancedToolsViewModel_PerspectiveCornersClearedRequested;
        ViewModel.AdvancedTools.CutOutSelectionClearedRequested += AdvancedToolsViewModel_CutOutSelectionClearedRequested;
        ViewModel.AdvancedTools.UnwarpPointsClearedRequested += AdvancedToolsViewModel_UnwarpPointsClearedRequested;
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

    private void AdvancedToolsViewModel_CutOutSelectionClearedRequested(object? sender, EventArgs e)
    {
        _isDraggingCutOut = false;
        SelectionRectangle.Visibility = Visibility.Collapsed;
    }

    private void AdvancedToolsViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdvancedToolsViewModel.IsAnyToolActive))
            UpdateCursorForModes();

        if (e.PropertyName is nameof(AdvancedToolsViewModel.PlacedCount)
            or nameof(AdvancedToolsViewModel.AllCornersPlaced)
            or nameof(AdvancedToolsViewModel.UnwarpControlPoints))
            RefreshUnwarpMarkers();
    }

    private void AdvancedToolsViewModel_UnwarpPointsClearedRequested(object? sender, EventArgs e)
    {
        if (ViewModel.CurrentDecodingItem == null) return;
        var toRemove = ViewModel.CurrentDecodingItem.PerspectiveCornerMarkers
            .Where(m => m is FrameworkElement fe && "unwarp".Equals(fe.Tag as string))
            .ToList();
        foreach (var m in toRemove)
            ViewModel.CurrentDecodingItem.PerspectiveCornerMarkers.Remove(m);
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

    private async void PasteImageButton_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenFileFromClipboardCommand.ExecuteAsync(null);

    private async void PasteKeyboardAccelerator_Invoked(KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!ViewModel.CanPasteImage)
            return;
        args.Handled = true;
        await ViewModel.OpenFileFromClipboardCommand.ExecuteAsync(null);
    }

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
            await ViewModel.OpenAndDecodeStorageFiles(storageItems);
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

        if (ViewModel.AdvancedTools.IsCutOutRegionMode)
        {
            _isDraggingCutOut = true;
            _cutOutCanvasStart = clickPoint;
            _cutOutCanvasEnd = clickPoint;
            imageContainer.CapturePointer(e.Pointer);
            return;
        }

        if (ViewModel.AdvancedTools.IsPerspectiveCorrectionMode)
        {
            HandlePerspectiveCorrectionClick(imagePoint, clickPoint);
            return;
        }

        if (ViewModel.AdvancedTools.IsUnwarpMode)
        {
            HandleUnwarpClick(imagePoint);
            return;
        }

        if (ViewModel.AdvancedTools.IsEyedropperBlackMode ||
            ViewModel.AdvancedTools.IsEyedropperWhiteMode)
            ViewModel.AdvancedTools.SetColorFromPoint(imagePoint);
    }

    private void ImageContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingCutOut || sender is not Grid imageContainer)
            return;

        _cutOutCanvasEnd = e.GetCurrentPoint(imageContainer).Position;
        UpdateSelectionRectangleVisual();
    }

    private void ImageContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingCutOut || sender is not Grid imageContainer || ViewModel.CurrentDecodingItem is null)
            return;

        imageContainer.ReleasePointerCapture(e.Pointer);
        _isDraggingCutOut = false;

        _cutOutCanvasEnd = e.GetCurrentPoint(imageContainer).Position;
        UpdateSelectionRectangleVisual();

        double displayedW = ImageWithBarcodes.ActualWidth;
        double displayedH = ImageWithBarcodes.ActualHeight;
        if (displayedW <= 0 || displayedH <= 0)
            return;

        double scaleX = ViewModel.CurrentDecodingItem.ImagePixelWidth / displayedW;
        double scaleY = ViewModel.CurrentDecodingItem.ImagePixelHeight / displayedH;

        System.Drawing.Point imgStart = new(
            Math.Clamp((int)(_cutOutCanvasStart.X * scaleX), 0, ViewModel.CurrentDecodingItem.ImagePixelWidth - 1),
            Math.Clamp((int)(_cutOutCanvasStart.Y * scaleY), 0, ViewModel.CurrentDecodingItem.ImagePixelHeight - 1));
        System.Drawing.Point imgEnd = new(
            Math.Clamp((int)(_cutOutCanvasEnd.X * scaleX), 0, ViewModel.CurrentDecodingItem.ImagePixelWidth - 1),
            Math.Clamp((int)(_cutOutCanvasEnd.Y * scaleY), 0, ViewModel.CurrentDecodingItem.ImagePixelHeight - 1));

        ViewModel.AdvancedTools.SetCutOutRegion(imgStart, imgEnd);
    }

    private void UpdateSelectionRectangleVisual()
    {
        double minX = Math.Min(_cutOutCanvasStart.X, _cutOutCanvasEnd.X);
        double minY = Math.Min(_cutOutCanvasStart.Y, _cutOutCanvasEnd.Y);
        double w = Math.Abs(_cutOutCanvasEnd.X - _cutOutCanvasStart.X);
        double h = Math.Abs(_cutOutCanvasEnd.Y - _cutOutCanvasStart.Y);

        Canvas.SetLeft(SelectionRectangle, minX);
        Canvas.SetTop(SelectionRectangle, minY);
        SelectionRectangle.Width = w;
        SelectionRectangle.Height = h;
        SelectionRectangle.Visibility = w > 2 && h > 2 ? Visibility.Visible : Visibility.Collapsed;
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

    private void HandleUnwarpClick(System.Drawing.Point imagePoint)
    {
        if (ViewModel.CurrentDecodingItem == null) return;
        var tools = ViewModel.AdvancedTools;
        if (tools.NextUnwarpPoint == null) return;

        tools.PlaceNextUnwarpPoint(imagePoint);
        // RefreshUnwarpMarkers is called via the UnwarpControlPoints PropertyChanged subscription.
    }

    private void RefreshUnwarpMarkers()
    {
        var item = ViewModel.CurrentDecodingItem;
        if (item == null) return;

        // Remove stale unwarp markers.
        var toRemove = item.PerspectiveCornerMarkers
            .Where(m => m is FrameworkElement fe && "unwarp".Equals(fe.Tag as string))
            .ToList();
        foreach (var m in toRemove)
            item.PerspectiveCornerMarkers.Remove(m);

        var tools = ViewModel.AdvancedTools;
        if (!tools.IsUnwarpMode) return;

        double displayW = ImageWithBarcodes.ActualWidth;
        double displayH = ImageWithBarcodes.ActualHeight;
        int imageW = item.ImagePixelWidth;
        int imageH = item.ImagePixelHeight;
        if (imageW <= 0 || imageH <= 0 || displayW <= 0 || displayH <= 0) return;

        var placedCorners = tools.UnwarpControlPoints
            .Where(p => p.Kind == UnwarpPointKind.OuterCorner && p.PlacedImagePoint.HasValue)
            .OrderBy(p => p.OrderIndex)
            .ToList();

        // Solid corner markers.
        var cornerDisplayPoints = new List<Windows.Foundation.Point>();
        foreach (var pt in placedCorners)
        {
            var dp = ToDisplayPoint(pt.PlacedImagePoint!.Value, displayW, displayH, imageW, imageH);
            cornerDisplayPoints.Add(dp);
            var marker = CreateUnwarpCornerMarker(dp, pt.Label);
            item.PerspectiveCornerMarkers.Add(marker);
        }

        // Connecting lines + alignment markers once all 4 corners are placed.
        if (placedCorners.Count == 4)
        {
            for (int i = 0; i < 4; i++)
            {
                var line = CreateUnwarpBoundaryLine(cornerDisplayPoints[i], cornerDisplayPoints[(i + 1) % 4]);
                item.PerspectiveCornerMarkers.Add(line);
            }

            var tl = placedCorners[0].PlacedImagePoint!.Value;
            var tr = placedCorners[1].PlacedImagePoint!.Value;
            var br = placedCorners[2].PlacedImagePoint!.Value;
            var bl = placedCorners[3].PlacedImagePoint!.Value;

            foreach (var pt in tools.UnwarpControlPoints.Where(p => p.Kind == UnwarpPointKind.AlignmentPattern))
            {
                Windows.Foundation.Point dp;
                bool isGhost;

                if (pt.PlacedImagePoint.HasValue)
                {
                    dp = ToDisplayPoint(pt.PlacedImagePoint.Value, displayW, displayH, imageW, imageH);
                    isGhost = false;
                }
                else
                {
                    // Bilinear estimate (note: bl before br in the signature).
                    var est = QrAlignmentPatternHelper.EstimateImagePosition(tl, tr, bl, br, pt.ModuleRow, pt.ModuleCol, tools.QrVersion);
                    dp = ToDisplayPoint(new System.Drawing.Point((int)est.X, (int)est.Y), displayW, displayH, imageW, imageH);
                    isGhost = true;
                }

                var marker = CreateUnwarpAlignmentMarker(dp, pt.Label, isGhost);
                item.PerspectiveCornerMarkers.Add(marker);
            }
        }
    }

    private static Windows.Foundation.Point ToDisplayPoint(
        System.Drawing.Point imagePoint,
        double displayW, double displayH, int imageW, int imageH)
    {
        return new Windows.Foundation.Point(
            imagePoint.X * displayW / imageW,
            imagePoint.Y * displayH / imageH);
    }

    private static Grid CreateUnwarpCornerMarker(Windows.Foundation.Point position, string label)
    {
        Grid markerGrid = new()
        {
            Width = 32,
            Height = 32,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Tag = "unwarp"
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

        TextBlock lbl = new()
        {
            Text = label,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        markerGrid.Children.Add(marker);
        markerGrid.Children.Add(lbl);
        Canvas.SetLeft(markerGrid, position.X - 16);
        Canvas.SetTop(markerGrid, position.Y - 16);
        Canvas.SetZIndex(markerGrid, 100);

        return markerGrid;
    }

    private static Grid CreateUnwarpAlignmentMarker(Windows.Foundation.Point position, string label, bool isGhost)
    {
        Grid markerGrid = new()
        {
            Width = 24,
            Height = 24,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Tag = "unwarp",
            Opacity = isGhost ? 0.4 : 0.9
        };

        Ellipse marker = new()
        {
            Width = 24,
            Height = 24,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212)),
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
            StrokeThickness = 2
        };

        TextBlock lbl = new()
        {
            Text = label,
            FontSize = 9,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        markerGrid.Children.Add(marker);
        markerGrid.Children.Add(lbl);
        Canvas.SetLeft(markerGrid, position.X - 12);
        Canvas.SetTop(markerGrid, position.Y - 12);
        Canvas.SetZIndex(markerGrid, 100);

        return markerGrid;
    }

    private static Line CreateUnwarpBoundaryLine(Windows.Foundation.Point from, Windows.Foundation.Point to)
    {
        Line line = new()
        {
            X1 = from.X,
            Y1 = from.Y,
            X2 = to.X,
            Y2 = to.Y,
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.Red),
            StrokeThickness = 1.5,
            Opacity = 0.4,
            Tag = "unwarp"
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

        //TopSpacerRow.Height = ViewModel.HasImage
        //    ? new GridLength(1, GridUnitType.Star)
        //    : new GridLength(1, GridUnitType.Auto);
    }
}
