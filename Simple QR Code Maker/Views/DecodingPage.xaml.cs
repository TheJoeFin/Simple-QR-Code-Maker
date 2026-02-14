using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using System.Diagnostics;
using System.Drawing;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class DecodingPage : Page
{
    public DecodingViewModel ViewModel
    {
        get;
    }

    public DecodingPage()
    {
        ViewModel = App.GetService<DecodingViewModel>();
        InitializeComponent();

        AdvancedToolsPanel.ViewModel.ImageProcessed += AdvancedToolsViewModel_ImageProcessed;
        AdvancedToolsPanel.ViewModel.PropertyChanged += AdvancedToolsViewModel_PropertyChanged;
        AdvancedToolsPanel.ViewModel.PerspectiveCornersClearedRequested += AdvancedToolsViewModel_PerspectiveCornersClearedRequested;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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
        // Update cursor when eyedropper mode changes
        if (e.PropertyName is (nameof(AdvancedToolsPanel.ViewModel.IsEyedropperBlackMode)) or
            (nameof(AdvancedToolsPanel.ViewModel.IsEyedropperWhiteMode)) or
            (nameof(AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode)))
        {
            UpdateCursorForModes();
        }

        // Clear markers when perspective correction mode is turned off
        if (e.PropertyName == nameof(AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode) &&
            !AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode)
        {
            if (ViewModel.CurrentDecodingItem != null)
            {
                ViewModel.CurrentDecodingItem.PerspectiveCornerMarkers.Clear();
                ViewModel.CurrentDecodingItem.CurrentCornerIndex = 0;
            }
        }
    }

    private void UpdateCursorForModes()
    {
        if (AdvancedToolsPanel.ViewModel.IsEyedropperBlackMode ||
            AdvancedToolsPanel.ViewModel.IsEyedropperWhiteMode ||
            AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode)
        {
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Cross);
        }
        else
        {
            this.ProtectedCursor = null;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.CurrentDecodingItem))
        {
            if (ViewModel.CurrentDecodingItem?.OriginalMagickImage != null)
            {
                AdvancedToolsPanel.ViewModel.SetOriginalImage(ViewModel.CurrentDecodingItem.OriginalMagickImage);
            }
            else if (ViewModel.CurrentDecodingItem is null)
            {
                // Image was cleared — reset all advanced tools state and cursor
                AdvancedToolsPanel.ViewModel.ClearAll();
                this.ProtectedCursor = null;
            }
        }

        if (e.PropertyName is nameof(ViewModel.HasImage) or nameof(ViewModel.CurrentDecodingItem))
        {
            ImageRow.Height = ViewModel.HasImage
                ? new GridLength(1, GridUnitType.Star)
                : GridLength.Auto;
        }
    }

    private async void AdvancedToolsViewModel_ImageProcessed(object? sender, ImageMagick.MagickImage e)
    {
        if (ViewModel.CurrentDecodingItem != null)
        {
            // Clear perspective markers and mode after successful processing
            if (AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode &&
                AdvancedToolsPanel.ViewModel.IsCornerSelectionComplete)
            {
                ViewModel.CurrentDecodingItem.PerspectiveCornerMarkers.Clear();
                ViewModel.CurrentDecodingItem.CurrentCornerIndex = 0;

                // Reset corner points
                AdvancedToolsPanel.ViewModel.ClearPerspectiveCornersCommand.Execute(null);

                // Turn off perspective correction mode to reset cursor and UI
                AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode = false;
            }

            ViewModel.CurrentDecodingItem.ProcessedMagickImage = e;

            // Update the item's OriginalMagickImage to the cumulative result so that
            // re-selecting this item later will resume from the current baseline.
            ViewModel.CurrentDecodingItem.OriginalMagickImage = (ImageMagick.MagickImage)e.Clone();

            await ViewModel.ApplyAdvancedToolsAndRedecodeCommand.ExecuteAsync(ViewModel.CurrentDecodingItem);
        }
    }

    private void DropContainer_DragOver(object sender, DragEventArgs e)
    {
        DataPackageView dataView = e.DataView;

        if (dataView.Contains(StandardDataFormats.Bitmap))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            return;
        }
        else if (dataView.Contains(StandardDataFormats.Uri))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        else if (dataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
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
        if (sender is not Grid imageContainer)
            return;

        if (ImageWithBarcodes?.Source is not Microsoft.UI.Xaml.Media.Imaging.BitmapImage bitmapSource)
            return;

        // Get click position relative to the image container
        Windows.Foundation.Point clickPoint = e.GetCurrentPoint(imageContainer).Position;

        // Get the actual image source dimensions (pixels)
        int imageWidth = bitmapSource.PixelWidth;
        int imageHeight = bitmapSource.PixelHeight;

        // Calculate scale factor: image pixels / display pixels
        double scaleX = imageWidth / imageContainer.ActualWidth;
        double scaleY = imageHeight / imageContainer.ActualHeight;

        // Convert display coordinates to image pixel coordinates
        double imageX = clickPoint.X * scaleX;
        double imageY = clickPoint.Y * scaleY;

        // Clamp to valid image bounds
        int pixelX = Math.Clamp((int)Math.Round(imageX), 0, imageWidth - 1);
        int pixelY = Math.Clamp((int)Math.Round(imageY), 0, imageHeight - 1);

        // Pass the pixel coordinates to the AdvancedToolsViewModel
        Point imagePoint = new(pixelX, pixelY);

        // Handle perspective correction mode
        if (AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode)
        {
            HandlePerspectiveCorrectionClick(imagePoint, clickPoint);
            return;
        }

        // Handle eyedropper mode
        if (AdvancedToolsPanel.ViewModel.IsEyedropperBlackMode ||
            AdvancedToolsPanel.ViewModel.IsEyedropperWhiteMode)
        {
            AdvancedToolsPanel.ViewModel.SetColorFromPoint(imagePoint);
        }
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

        // Set the corner in the ViewModel (image space coordinates)
        AdvancedToolsPanel.ViewModel.SetCornerPoint(imagePoint, cornerIndex);

        // Create visual marker (display space coordinates)
        Grid marker = CreateCornerMarkerWithLabel(displayPoint, cornerIndex);
        item.PerspectiveCornerMarkers.Add(marker);

        // Add connecting line if not the first corner
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

        // If this is the 4th corner, connect back to the first
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
}
