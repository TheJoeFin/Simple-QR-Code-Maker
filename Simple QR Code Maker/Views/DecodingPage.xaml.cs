using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Simple_QR_Code_Maker.ViewModels;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

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
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.DecodingImageItems.CollectionChanged += DecodingImageItems_CollectionChanged;
    }

    private void AdvancedToolsViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Update cursor when eyedropper mode changes
        if (e.PropertyName == nameof(AdvancedToolsPanel.ViewModel.IsEyedropperBlackMode) ||
            e.PropertyName == nameof(AdvancedToolsPanel.ViewModel.IsEyedropperWhiteMode))
        {
            UpdateCursorForEyedropperMode();
        }
    }

    private void UpdateCursorForEyedropperMode()
    {
        if (AdvancedToolsPanel.ViewModel.IsEyedropperBlackMode ||
            AdvancedToolsPanel.ViewModel.IsEyedropperWhiteMode)
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
                                while (parent != null && parent is not InfoBar)
                                {
                                    parent = VisualTreeHelper.GetParent(parent);
                                }

                                if (parent is InfoBar infoBar)
                                {
                                    // Hide the InfoBar
                                    infoBar.Visibility = Visibility.Collapsed;

                                    // Find and show the HyperlinkButton (sibling of InfoBar)
                                    var stackPanel = VisualTreeHelper.GetParent(infoBar) as StackPanel;
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
                                var stackPanel = VisualTreeHelper.GetParent(button) as StackPanel;
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
                    // Check if eyedropper mode is active
                    if (!AdvancedToolsPanel.ViewModel.IsEyedropperBlackMode && 
                        !AdvancedToolsPanel.ViewModel.IsEyedropperWhiteMode)
                    {
                        return;
                    }

                    if (sender is not Grid imageContainer)
                        return;

                    // Get the Image control
                    var image = FindChildByName<Image>(imageContainer, "ImageWithBarcodes");
                    if (image?.Source is not Microsoft.UI.Xaml.Media.Imaging.BitmapImage bitmapSource)
                        return;

                    // Get click position relative to the image container
                    var clickPoint = e.GetCurrentPoint(imageContainer).Position;

                    // Get the actual image dimensions
                    var imageWidth = bitmapSource.PixelWidth;
                    var imageHeight = bitmapSource.PixelHeight;

                    // Get the displayed dimensions (scaled by Viewbox)
                    var displayWidth = imageContainer.ActualWidth;
                    var displayHeight = imageContainer.ActualHeight;

                    // Calculate the scale factor
                    double scaleX = imageWidth / displayWidth;
                    double scaleY = imageHeight / displayHeight;

                    // Convert click coordinates to image pixel coordinates
                    int pixelX = (int)(clickPoint.X * scaleX);
                    int pixelY = (int)(clickPoint.Y * scaleY);

                    // Clamp to image bounds
                    pixelX = Math.Clamp(pixelX, 0, imageWidth - 1);
                    pixelY = Math.Clamp(pixelY, 0, imageHeight - 1);

                    Debug.WriteLine($"Clicked at display: ({clickPoint.X}, {clickPoint.Y}), pixel: ({pixelX}, {pixelY})");

                    // Pass the pixel coordinates to the AdvancedToolsViewModel
                    var imagePoint = new System.Drawing.Point(pixelX, pixelY);
                    AdvancedToolsPanel.ViewModel.SetColorFromPoint(imagePoint);
                }

                private T? FindChildByName<T>(DependencyObject parent, string childName) where T : FrameworkElement
                {
                    if (parent == null)
                        return null;

                    int childCount = VisualTreeHelper.GetChildrenCount(parent);
                    for (int i = 0; i < childCount; i++)
                    {
                        var child = VisualTreeHelper.GetChild(parent, i);

                        if (child is T typedChild && typedChild.Name == childName)
                            return typedChild;

                        var result = FindChildByName<T>(child, childName);
                        if (result != null)
                            return result;
                    }

                    return null;
                }
            }
