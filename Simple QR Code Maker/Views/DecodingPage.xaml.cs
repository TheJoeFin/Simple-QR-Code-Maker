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
        ViewModel.DecodingImageItems.CollectionChanged += DecodingImageItems_CollectionChanged;
    }

    private void AdvancedToolsViewModel_PerspectiveCornersClearedRequested(object? sender, EventArgs e)
    {
        if (ViewModel.SelectedDecodingImageItem != null)
        {
            ViewModel.SelectedDecodingImageItem.PerspectiveCornerMarkers.Clear();
            ViewModel.SelectedDecodingImageItem.CurrentCornerIndex = 0;
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
            if (ViewModel.SelectedDecodingImageItem != null)
            {
                ViewModel.SelectedDecodingImageItem.PerspectiveCornerMarkers.Clear();
                ViewModel.SelectedDecodingImageItem.CurrentCornerIndex = 0;
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

    private void DecodingImageItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && ViewModel.DecodingImageItems.Count > 0)
        {
            ViewModel.SelectedDecodingImageItem = ViewModel.DecodingImageItems[0];

            if (ViewModel.SelectedDecodingImageItem?.OriginalMagickImage != null)
            {
                AdvancedToolsPanel.ViewModel.SetOriginalImage(ViewModel.SelectedDecodingImageItem.OriginalMagickImage);
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.DecodingImageItems) && ViewModel.DecodingImageItems.Count > 0)
        {
            ViewModel.SelectedDecodingImageItem = ViewModel.DecodingImageItems[0];

            if (ViewModel.SelectedDecodingImageItem?.OriginalMagickImage != null)
            {
                AdvancedToolsPanel.ViewModel.SetOriginalImage(ViewModel.SelectedDecodingImageItem.OriginalMagickImage);
            }
        }
    }

    private async void AdvancedToolsViewModel_ImageProcessed(object? sender, ImageMagick.MagickImage e)
    {
        if (ViewModel.SelectedDecodingImageItem != null)
        {
            // Clear perspective markers and mode after successful processing
            if (AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode &&
                AdvancedToolsPanel.ViewModel.IsCornerSelectionComplete)
            {
                ViewModel.SelectedDecodingImageItem.PerspectiveCornerMarkers.Clear();
                ViewModel.SelectedDecodingImageItem.CurrentCornerIndex = 0;

                // Reset corner points
                AdvancedToolsPanel.ViewModel.ClearPerspectiveCornersCommand.Execute(null);

                // Turn off perspective correction mode to reset cursor and UI
                AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode = false;
            }

            ViewModel.SelectedDecodingImageItem.ProcessedMagickImage = e;
            await ViewModel.ApplyAdvancedToolsAndRedecodeCommand.ExecuteAsync(ViewModel.SelectedDecodingImageItem);
        }
    }

    private void GridViewContainer_DragOver(object sender, DragEventArgs e)
    {
        DataPackageView dataView = e.DataView;

        if (dataView.Contains(StandardDataFormats.Bitmap))
        {
            Debug.WriteLine($"contains Bitmap");
            e.AcceptedOperation = DataPackageOperation.Copy;
            return;
        }
        else if (dataView.Contains(StandardDataFormats.Uri))
        {
            Debug.WriteLine($"contains Uri");
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        else if (dataView.Contains(StandardDataFormats.StorageItems))
        {
            Debug.WriteLine($"contains StorageItems");
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void GridViewContainer_Drop(object sender, DragEventArgs e)
    {
        DragOperationDeferral def = e.GetDeferral();
        e.Handled = true;
        def.Complete();

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            Debug.WriteLine("Dropped StorageItem");
            IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();

            ViewModel.OpenAndDecodeStorageFiles(storageItems);
        }
    }

    private void DismissNoCodesWarning_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Models.DecodingImageItem item)
        {
            item.IsNoCodesWarningDismissed = true;

            // Find the InfoBar and HyperlinkButton in the visual tree
            DependencyObject parent = button;
            while (parent is not null and not InfoBar)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            if (parent is InfoBar infoBar)
            {
                // Hide the InfoBar
                infoBar.Visibility = Visibility.Collapsed;

                // Find and show the HyperlinkButton (sibling of InfoBar)
                StackPanel? stackPanel = VisualTreeHelper.GetParent(infoBar) as StackPanel;
                if (stackPanel != null)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(stackPanel); i++)
                    {
                        if (VisualTreeHelper.GetChild(stackPanel, i) is HyperlinkButton linkButton)
                        {
                            linkButton.Visibility = Visibility.Visible;
                            break;
                        }
                    }
                }
            }
        }
    }

    private void ShowNoCodesInfo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton button && button.Tag is Models.DecodingImageItem item)
        {
            item.IsNoCodesWarningDismissed = false;

            // Hide the HyperlinkButton
            button.Visibility = Visibility.Collapsed;

            // Find and show the InfoBar (sibling)
            StackPanel? stackPanel = VisualTreeHelper.GetParent(button) as StackPanel;
            if (stackPanel != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(stackPanel); i++)
                {
                    if (VisualTreeHelper.GetChild(stackPanel, i) is InfoBar infoBar)
                    {
                        infoBar.Visibility = Visibility.Visible;
                        break;
                    }
                }
            }
        }
    }

    private void ImageContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid imageContainer)
            return;

        // Get the Image control
        Image? image = FindChildByName<Image>(imageContainer, "ImageWithBarcodes");
        if (image?.Source is not Microsoft.UI.Xaml.Media.Imaging.BitmapImage bitmapSource)
            return;

        // Get click position relative to the image container
        Windows.Foundation.Point clickPoint = e.GetCurrentPoint(imageContainer).Position;

        // Also get position relative to the Image control itself
        Windows.Foundation.Point clickPointOnImage = e.GetCurrentPoint(image).Position;

        // Get the actual image source dimensions (pixels)
        int imageWidth = bitmapSource.PixelWidth;
        int imageHeight = bitmapSource.PixelHeight;

        Debug.WriteLine($"");
        Debug.WriteLine($"=================================================");
        Debug.WriteLine($"===== COORDINATE CONVERSION DEBUG =====");
        Debug.WriteLine($"=================================================");
        Debug.WriteLine($"");
        Debug.WriteLine($"IMAGE SOURCE:");
        Debug.WriteLine($"  Pixel size: {imageWidth} x {imageHeight}");
        Debug.WriteLine($"");

        // Walk up the visual tree to understand layout
        Debug.WriteLine($"VISUAL TREE (Image → Root):");
        FrameworkElement? element = image;
        int level = 0;
        while (element != null && level < 10)
        {
            string indent = new string(' ', level * 2);
            string typeName = element.GetType().Name;
            Debug.WriteLine($"{indent}[{level}] {typeName}:");
            Debug.WriteLine($"{indent}    ActualSize: {element.ActualWidth:F2} x {element.ActualHeight:F2}");

            if (element is Border border)
            {
                string w = double.IsNaN(border.Width) ? "Auto" : border.Width.ToString("F0");
                string h = double.IsNaN(border.Height) ? "Auto" : border.Height.ToString("F0");
                Debug.WriteLine($"{indent}    Width={w}, Height={h}");
            }
            else if (element is Viewbox vb)
            {
                Debug.WriteLine($"{indent}    Stretch={vb.Stretch}");
            }

            element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            level++;
        }
        Debug.WriteLine($"");

        // Walk up the visual tree to find the Viewbox to understand scaling
        FrameworkElement? parent = imageContainer;
        Viewbox? scalingViewbox = null;
        double displayWidth = imageContainer.ActualWidth;
        double displayHeight = imageContainer.ActualHeight;

        while (parent != null)
        {
            parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
            if (parent is Viewbox vb)
            {
                scalingViewbox = vb;
                displayWidth = vb.ActualWidth;
                displayHeight = vb.ActualHeight;
                Debug.WriteLine($"Found Viewbox: ActualSize={vb.ActualWidth:F2} x {vb.ActualHeight:F2}");
                break;
            }
        }

        Debug.WriteLine($"COORDINATES:");
        Debug.WriteLine($"  Click in container: ({clickPoint.X:F2}, {clickPoint.Y:F2})");
        Debug.WriteLine($"  Click on image:     ({clickPointOnImage.X:F2}, {clickPointOnImage.Y:F2})");
        Debug.WriteLine($"  Using container size: {imageContainer.ActualWidth:F2} x {imageContainer.ActualHeight:F2}");
        Debug.WriteLine($"");

        // CRITICAL: The click coordinates are in the DISPLAY coordinate space (scaled by Viewbox)
        // We need to convert them to IMAGE PIXEL space by applying the scale factor
        // Calculate scale factor: image pixels / display pixels
        double scaleX = imageWidth / imageContainer.ActualWidth;
        double scaleY = imageHeight / imageContainer.ActualHeight;

        Debug.WriteLine($"SCALE CALCULATION:");
        Debug.WriteLine($"  ScaleX = {imageWidth} / {imageContainer.ActualWidth:F2} = {scaleX:F4}");
        Debug.WriteLine($"  ScaleY = {imageHeight} / {imageContainer.ActualHeight:F2} = {scaleY:F4}");
        Debug.WriteLine($"");

        // Convert display coordinates to image pixel coordinates
        double imageX = clickPoint.X * scaleX;
        double imageY = clickPoint.Y * scaleY;

        Debug.WriteLine($"CONVERSION TO IMAGE PIXELS:");
        Debug.WriteLine($"  Container click ({clickPoint.X:F2}, {clickPoint.Y:F2}) * scale ({scaleX:F4}, {scaleY:F4})");
        Debug.WriteLine($"  = Image pixel ({imageX:F0}, {imageY:F0})");
        Debug.WriteLine($"");

        // Clamp to valid image bounds
        int pixelX = Math.Clamp((int)Math.Round(imageX), 0, imageWidth - 1);
        int pixelY = Math.Clamp((int)Math.Round(imageY), 0, imageHeight - 1);

        Debug.WriteLine($"FINAL RESULT (after clamping):");
        Debug.WriteLine($"  >> Converted pixel coordinates: ({pixelX}, {pixelY})");
        Debug.WriteLine($"");
        Debug.WriteLine($"=================================================");

        // Log expected values for testing
        Debug.WriteLine($"EXPECTED COORDINATES FOR TEST:");
        Debug.WriteLine($"  If actual pixels are (2765, 3393), display should be: ({2765 / scaleX:F2}, {3393 / scaleY:F2})");
        Debug.WriteLine($"  If actual pixels are (3230, 3405), display should be: ({3230 / scaleX:F2}, {3405 / scaleY:F2})");
        Debug.WriteLine($"  If actual pixels are (3224, 3859), display should be: ({3224 / scaleX:F2}, {3859 / scaleY:F2})");
        Debug.WriteLine($"  If actual pixels are (2745, 3863), display should be: ({2745 / scaleX:F2}, {3863 / scaleY:F2})");
        Debug.WriteLine($"=================================================");
        Debug.WriteLine($"");

        // Pass the pixel coordinates to the AdvancedToolsViewModel
        Point imagePoint = new(pixelX, pixelY);

        // Handle perspective correction mode
        if (AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode)
        {
            // For visual markers, we need the click position in the ACTUAL displayed (scaled) space
            // The click is relative to the full-size container, but markers need to be in scaled space
            // Since the container IS full size but DISPLAYED scaled, use the click as-is
            // (The canvas for markers is bound to the same container size, so coordinates match)
            HandlePerspectiveCorrectionClick(imagePoint, clickPoint, image);
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
        Windows.Foundation.Point displayPoint,
        Image imageControl)
    {
        if (ViewModel.SelectedDecodingImageItem == null)
            return;

        DecodingImageItem item = ViewModel.SelectedDecodingImageItem;
        int cornerIndex = item.CurrentCornerIndex;

        Debug.WriteLine($"=== Click {cornerIndex + 1} ===");
        Debug.WriteLine($"Current collection count: {item.PerspectiveCornerMarkers.Count}");

        if (cornerIndex >= 4)
        {
            Debug.WriteLine("Already have 4 corners, ignoring click");
            return;
        }

        // Set the corner in the ViewModel (image space coordinates)
        AdvancedToolsPanel.ViewModel.SetCornerPoint(imagePoint, cornerIndex);

        // Create visual marker (display space coordinates)
        Grid marker = CreateCornerMarkerWithLabel(displayPoint, cornerIndex);
        item.PerspectiveCornerMarkers.Add(marker);
        Debug.WriteLine($"Added marker {cornerIndex} at display: ({displayPoint.X}, {displayPoint.Y}), image: ({imagePoint.X}, {imagePoint.Y})");
        Debug.WriteLine($"Collection now has {item.PerspectiveCornerMarkers.Count} items");

        // Add connecting line if not the first corner
        if (cornerIndex > 0)
        {
            // Get the previous marker (the one we just added is at Count-1)
            // Search backwards from Count-2 to find the previous Grid marker
            UIElement? previousMarker = null;
            int searchStart = item.PerspectiveCornerMarkers.Count - 2;
            Debug.WriteLine($"Searching for previous marker from index {searchStart} backwards");

            for (int i = searchStart; i >= 0; i--)
            {
                UIElement element = item.PerspectiveCornerMarkers[i];
                Debug.WriteLine($"  Index {i}: {element.GetType().Name}");

                if (element is Grid)
                {
                    previousMarker = element;
                    Debug.WriteLine($"  Found previous Grid marker at index {i}");
                    break;
                }
            }

            if (previousMarker != null)
            {
                Line line = CreateConnectingLine(previousMarker, marker);
                item.PerspectiveCornerMarkers.Add(line);
                Debug.WriteLine($"Added connecting line, collection now has {item.PerspectiveCornerMarkers.Count} items");
            }
            else
            {
                Debug.WriteLine("WARNING: Could not find previous marker!");
            }
        }

        // If this is the 4th corner, connect back to the first
        if (cornerIndex == 3)
        {
            UIElement firstMarker = item.PerspectiveCornerMarkers[0];
            Line line = CreateConnectingLine(marker, firstMarker);
            item.PerspectiveCornerMarkers.Add(line);
            Debug.WriteLine($"Added closing line, collection now has {item.PerspectiveCornerMarkers.Count} items");
        }

        // Increment corner index
        item.CurrentCornerIndex++;
        Debug.WriteLine($"Corner index incremented to {item.CurrentCornerIndex}");

        Debug.WriteLine($"Selected corner {cornerIndex + 1} at image: ({imagePoint.X}, {imagePoint.Y}), display: ({displayPoint.X}, {displayPoint.Y})");
    }

    private static Grid CreateCornerMarkerWithLabel(Windows.Foundation.Point position, int cornerIndex)
    {
        // Create a container grid for the marker and label
        Grid markerGrid = new()
        {
            Width = 32,
            Height = 32,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };

        // Create the circle marker
        Ellipse marker = new()
        {
            Width = 32,
            Height = 32,
            Fill = new SolidColorBrush(Microsoft.UI.Colors.Red),
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
            StrokeThickness = 3,
            Opacity = 0.9
        };

        // Create the number label
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

    private T? FindChildByName<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        if (parent == null)
            return null;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild && typedChild.Name == childName)
                return typedChild;

            T? result = FindChildByName<T>(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }
}
