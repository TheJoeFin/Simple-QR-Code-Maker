using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class DecodingPage : Page
{
    public DecodingViewModel ViewModel
    {
        get;
    }

    public ObservableCollection<DeviceInformation> CameraDevices { get; } = [];

    private bool isPointerOverImage;
    private MediaCapture? mediaCapture;
    private MediaPlayer? cameraPreviewPlayer;
    private MediaFrameSource? currentPreviewSource;
    private bool isCameraInitialized;
    private bool isPageLoaded;
    private bool hasCameraDevices;
    private bool isCameraBusy;
    private bool isCameraPreviewing;
    private bool isUpdatingCameraDeviceSelection;
    private bool hasCapturedCameraImage;
    private int cameraSelectionVersion;
    private string cameraStatusMessage = string.Empty;
    private string? activeCameraDeviceId;
    private DeviceInformation? selectedCameraDevice;
    private BitmapImage? capturedCameraImage;

    public bool HasCameraDevices => hasCameraDevices;

    public bool IsCameraBusy => isCameraBusy;

    public bool IsCameraPreviewing => isCameraPreviewing;

    public bool HasCapturedCameraImage => hasCapturedCameraImage;

    public string CameraStatusMessage => cameraStatusMessage;

    public DeviceInformation? SelectedCameraDevice => selectedCameraDevice;

    public BitmapImage? CapturedCameraImage => capturedCameraImage;

    private bool CanCapturePhoto => ViewModel.IsCameraPaneOpen
        && hasCameraDevices
        && selectedCameraDevice is not null
        && isCameraPreviewing
        && !isCameraBusy;

    private bool CanRetakePhoto => ViewModel.IsCameraPaneOpen
        && hasCapturedCameraImage
        && !isCameraBusy;

    private bool ShowCameraPlaceholder => !isCameraPreviewing
        && !hasCapturedCameraImage
        && !isCameraBusy;

    public DecodingPage()
    {
        ViewModel = App.GetService<DecodingViewModel>();
        InitializeComponent();
        CameraDeviceComboBox.ItemsSource = CameraDevices;
        UpdateLoadingOverlay();

        AdvancedToolsPanel.ViewModel.ImageProcessed += AdvancedToolsViewModel_ImageProcessed;
        AdvancedToolsPanel.ViewModel.PropertyChanged += AdvancedToolsViewModel_PropertyChanged;
        AdvancedToolsPanel.ViewModel.PerspectiveCornersClearedRequested += AdvancedToolsViewModel_PerspectiveCornersClearedRequested;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Loaded += DecodingPage_Loaded;
        Unloaded += DecodingPage_Unloaded;
    }

    private async void DecodingPage_Loaded(object sender, RoutedEventArgs e)
    {
        isPageLoaded = true;
        UpdateLoadingOverlay();
        UpdateBaseGridLayout();
        await RefreshCameraDevicesAsync();

        if (ViewModel.IsCameraPaneOpen)
            await UpdateCameraPaneAsync();
    }

    private async void DecodingPage_Unloaded(object sender, RoutedEventArgs e)
    {
        isPageLoaded = false;

        await CleanupCameraAsync();
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
        bool isToolActive = AdvancedToolsPanel.ViewModel.IsEyedropperBlackMode ||
            AdvancedToolsPanel.ViewModel.IsEyedropperWhiteMode ||
            AdvancedToolsPanel.ViewModel.IsPerspectiveCorrectionMode;

        if (isToolActive && isPointerOverImage)
        {
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Cross);
        }
        else
        {
            this.ProtectedCursor = null;
        }
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

    private void GoBackButton_Click(object sender, RoutedEventArgs e) => ViewModel.GoBackCommand.Execute(null);

    private async void OpenNewFileButton_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenNewFileCommand.ExecuteAsync(null);

    private async void PasteImageButton_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenFileFromClipboardCommand.ExecuteAsync(null);

    private void CameraToggleButton_Click(object sender, RoutedEventArgs e)
    {
        bool shouldOpenCamera = !ViewModel.IsCameraPaneOpen;
        if (shouldOpenCamera)
            ViewModel.IsAdvancedToolsVisible = false;

        ViewModel.IsCameraPaneOpen = shouldOpenCamera;
    }

    private void AdvancedToolsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasImage)
            return;

        bool shouldShowAdvancedTools = !ViewModel.IsAdvancedToolsVisible;
        if (shouldShowAdvancedTools)
        {
            ViewModel.IsCameraPaneOpen = false;

            if (ViewModel.CurrentDecodingItem?.OriginalMagickImage is not null)
                AdvancedToolsPanel.ViewModel.SetOriginalImage(ViewModel.CurrentDecodingItem.OriginalMagickImage);
        }

        ViewModel.IsAdvancedToolsVisible = shouldShowAdvancedTools;
    }

    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
                ViewModel.IsAdvancedToolsVisible = false;
                AdvancedToolsPanel.ViewModel.ClearAll();
                isPointerOverImage = false;
                this.ProtectedCursor = null;

                // Reset the ScrollView zoom and scroll position so the
                // container shrinks back to its default 300×300 size.
                ImageScrollView.ZoomTo(1, null);
                ImageScrollView.ScrollTo(0, 0);
            }
        }

        if (e.PropertyName is nameof(ViewModel.HasImage) or nameof(ViewModel.CurrentDecodingItem) or nameof(ViewModel.IsCameraPaneOpen) or nameof(ViewModel.HasActivePreviewSurface))
            UpdateBaseGridLayout();

        if (e.PropertyName is nameof(ViewModel.IsLoading) or nameof(ViewModel.LoadingMessage))
            UpdateLoadingOverlay();

        if (e.PropertyName == nameof(ViewModel.IsCameraPaneOpen))
        {
            await UpdateCameraPaneAsync();
        }
    }

    private void ActionButtonsPanel_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateBaseGridLayout();

    private void UpdateLoadingOverlay()
    {
        bool isLoading = ViewModel.IsLoading;
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        LoadingOverlay.IsHitTestVisible = isLoading;
        LoadingProgressRing.IsActive = isLoading;
        LoadingMessageTextBlock.Text = ViewModel.LoadingMessage;
    }

    private void UpdateBaseGridLayout()
    {
        ImageRow.Height = ViewModel.HasImage
            ? new GridLength(1, GridUnitType.Star)
            : GridLength.Auto;

        TopSpacerRow.Height = ViewModel.HasImage
            ? new GridLength(0)
            : new GridLength(ActionButtonsPanel.ActualHeight);
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
        if (sender is not Grid imageContainer || ViewModel.CurrentDecodingItem is null)
            return;

        double displayedImageWidth = ImageWithBarcodes.ActualWidth;
        double displayedImageHeight = ImageWithBarcodes.ActualHeight;

        if (displayedImageWidth <= 0 || displayedImageHeight <= 0)
            return;

        // Get click position relative to the image container
        Windows.Foundation.Point clickPoint = e.GetCurrentPoint(imageContainer).Position;

        // Get the actual image dimensions (pixels) from the decoded item instead of
        // BitmapImage.PixelWidth/PixelHeight, which can lag behind URI image loading.
        int imageWidth = ViewModel.CurrentDecodingItem.ImagePixelWidth;
        int imageHeight = ViewModel.CurrentDecodingItem.ImagePixelHeight;

        // Calculate scale factor: image pixels / display pixels
        double scaleX = imageWidth / displayedImageWidth;
        double scaleY = imageHeight / displayedImageHeight;

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

    private async void CameraDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingCameraDeviceSelection)
            return;

        selectedCameraDevice = CameraDeviceComboBox.SelectedItem as DeviceInformation;
        int selectionVersion = ++cameraSelectionVersion;

        UpdateCameraUi();

        if (!ViewModel.IsCameraPaneOpen || selectedCameraDevice is null)
            return;

        await StartCameraPreviewAsync(selectedCameraDevice, selectionVersion);
    }

    private async void CapturePhotoButton_Click(object sender, RoutedEventArgs e) => await CapturePhotoAsync();

    private async void RetakePhotoButton_Click(object sender, RoutedEventArgs e) => await RetakePhotoAsync();

    private async Task UpdateCameraPaneAsync()
    {
        if (!isPageLoaded)
            return;

        if (ViewModel.IsCameraPaneOpen)
        {
            await RefreshCameraDevicesAsync();

            if (HasCapturedCameraImage)
                return;

            if (SelectedCameraDevice is not null)
                await StartCameraPreviewAsync(SelectedCameraDevice, cameraSelectionVersion);
        }
        else
        {
            await CleanupCameraAsync();
        }
    }

    private async Task RefreshCameraDevicesAsync()
    {
        DeviceInformationCollection cameras = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        string? preferredCameraId = (CameraDeviceComboBox.SelectedItem as DeviceInformation)?.Id
            ?? selectedCameraDevice?.Id
            ?? activeCameraDeviceId;

        CameraDevices.Clear();
        foreach (DeviceInformation camera in cameras)
            CameraDevices.Add(camera);

        hasCameraDevices = CameraDevices.Count > 0;
        selectedCameraDevice = CameraDevices.FirstOrDefault(camera => camera.Id == preferredCameraId) ?? CameraDevices.FirstOrDefault();
        isUpdatingCameraDeviceSelection = true;
        try
        {
            CameraDeviceComboBox.SelectedItem = selectedCameraDevice;
        }
        finally
        {
            isUpdatingCameraDeviceSelection = false;
        }

        if (!hasCameraDevices)
        {
            await CleanupCameraAsync();
            capturedCameraImage = null;
            hasCapturedCameraImage = false;
            cameraStatusMessage = "No cameras are available on this device.";
        }
        else if (!hasCapturedCameraImage)
        {
            cameraStatusMessage = "Select Capture to take a photo for decoding.";
        }

        UpdateCameraUi();
    }

    private async Task StartCameraPreviewAsync(DeviceInformation cameraDevice, int selectionVersion)
    {
        if (isCameraBusy)
            return;

        if (isCameraInitialized && isCameraPreviewing && activeCameraDeviceId == cameraDevice.Id && selectionVersion == cameraSelectionVersion)
            return;

        await CleanupCameraAsync();

        try
        {
            isCameraBusy = true;
            cameraStatusMessage = $"Starting {cameraDevice.Name}...";
            capturedCameraImage = null;
            hasCapturedCameraImage = false;
            UpdateCameraUi();

            mediaCapture = new MediaCapture();

            MediaCaptureInitializationSettings initializationSettings = new()
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Auto,
                PhotoCaptureSource = PhotoCaptureSource.Auto,
                VideoDeviceId = cameraDevice.Id
            };

            await mediaCapture.InitializeAsync(initializationSettings);

            if (selectionVersion != cameraSelectionVersion || selectedCameraDevice?.Id != cameraDevice.Id)
            {
                await CleanupCameraAsync();
                return;
            }

            currentPreviewSource = GetPreferredPreviewSource(mediaCapture) ?? throw new InvalidOperationException("No renderable preview source is available for the selected camera.");
            await EnsureSupportedPreviewFormatAsync(currentPreviewSource);

            cameraPreviewPlayer = new MediaPlayer
            {
                RealTimePlayback = true,
                AutoPlay = false,
                Source = MediaSource.CreateFromMediaFrameSource(currentPreviewSource)
            };

            CameraPreviewElement.SetMediaPlayer(cameraPreviewPlayer);
            cameraPreviewPlayer.Play();

            isCameraInitialized = true;
            isCameraPreviewing = true;
            activeCameraDeviceId = cameraDevice.Id;
            cameraStatusMessage = "Select Capture to take a photo for decoding.";
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Unable to access camera: {ex}");
            cameraStatusMessage = "Camera access is turned off for this app in Windows settings.";
            await CleanupCameraAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to start camera preview: {ex}");
            cameraStatusMessage = $"Couldn't start {cameraDevice.Name}.";
            await CleanupCameraAsync();
        }
        finally
        {
            isCameraBusy = false;
            UpdateCameraUi();
        }
    }

    private async Task CapturePhotoAsync()
    {
        if (mediaCapture is null || selectedCameraDevice is null || !isCameraPreviewing)
            return;

        try
        {
            isCameraBusy = true;
            cameraStatusMessage = "Capturing photo...";
            UpdateCameraUi();

            StorageFile capturedFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                $"camera-capture-{DateTimeOffset.Now.Ticks}.jpg",
                CreationCollisionOption.GenerateUniqueName);

            await mediaCapture.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), capturedFile);
            await CleanupCameraAsync();

            capturedCameraImage = CreateBitmapImage(capturedFile.Path);
            hasCapturedCameraImage = true;
            cameraStatusMessage = "Photo captured. Use Retake to try another shot.";
            UpdateCameraUi();

            await ViewModel.OpenAndDecodeStorageFile(capturedFile);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to capture photo: {ex}");
            cameraStatusMessage = "Couldn't capture a photo from the selected camera.";
        }
        finally
        {
            isCameraBusy = false;
            UpdateCameraUi();
        }
    }

    private async Task RetakePhotoAsync()
    {
        if (selectedCameraDevice is null)
            return;

        ViewModel.CurrentDecodingItem = null;
        ViewModel.IsInfoBarShowing = false;
        ViewModel.InfoBarMessage = string.Empty;
        ViewModel.IsAdvancedToolsVisible = false;
        AdvancedToolsPanel.ViewModel.ClearAll();
        capturedCameraImage = null;
        hasCapturedCameraImage = false;
        cameraStatusMessage = string.Empty;
        UpdateCameraUi();

        await StartCameraPreviewAsync(selectedCameraDevice, cameraSelectionVersion);
    }

    private async Task StopPreviewAsync()
    {
        if (cameraPreviewPlayer is not null)
        {
            try
            {
                cameraPreviewPlayer.Pause();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to stop camera preview: {ex}");
            }

            CameraPreviewElement.SetMediaPlayer(null);
            cameraPreviewPlayer.Dispose();
            cameraPreviewPlayer = null;
        }

        currentPreviewSource = null;
        isCameraPreviewing = false;
        activeCameraDeviceId = null;
        UpdateCameraUi();
    }

    private async Task CleanupCameraAsync()
    {
        await StopPreviewAsync();
        mediaCapture?.Dispose();
        mediaCapture = null;

        isCameraInitialized = false;
        activeCameraDeviceId = null;
        UpdateCameraUi();
    }

    private static BitmapImage CreateBitmapImage(string imagePath)
    {
        Uri uri = new($"{imagePath}?tick={DateTimeOffset.Now.Ticks}");
        BitmapImage bitmapImage = new(uri)
        {
            CreateOptions = BitmapCreateOptions.IgnoreImageCache
        };

        return bitmapImage;
    }

    private static MediaFrameSource? GetPreferredPreviewSource(MediaCapture capture)
    {
        MediaFrameSource? previewSource = capture.FrameSources
            .FirstOrDefault(static source =>
                source.Value.Info.MediaStreamType == MediaStreamType.VideoPreview &&
                source.Value.Info.SourceKind == MediaFrameSourceKind.Color)
            .Value;

        if (previewSource is not null)
            return previewSource;

        return capture.FrameSources
            .FirstOrDefault(static source =>
                source.Value.Info.MediaStreamType == MediaStreamType.VideoRecord &&
                source.Value.Info.SourceKind == MediaFrameSourceKind.Color)
            .Value;
    }

    private static async Task EnsureSupportedPreviewFormatAsync(MediaFrameSource previewSource)
    {
        if (IsSupportedPreviewFormat(previewSource.CurrentFormat))
            return;

        MediaFrameFormat? supportedFormat = previewSource.SupportedFormats.FirstOrDefault(IsSupportedPreviewFormat) ?? throw new InvalidOperationException("The selected camera does not expose a renderable preview format.");
        await previewSource.SetFormatAsync(supportedFormat);
    }

    private static bool IsSupportedPreviewFormat(MediaFrameFormat? format)
    {
        return format is not null &&
            (string.Equals(format.Subtype, MediaEncodingSubtypes.Nv12, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(format.Subtype, MediaEncodingSubtypes.Yuy2, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(format.Subtype, MediaEncodingSubtypes.Rgb24, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(format.Subtype, MediaEncodingSubtypes.Rgb32, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateCameraUi()
    {
        CameraDeviceComboBox.IsEnabled = hasCameraDevices;
        CameraStatusTextBlock.Text = cameraStatusMessage;
        CameraStatusTextBlock.Visibility = string.IsNullOrWhiteSpace(cameraStatusMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

        CapturePhotoButton.IsEnabled = CanCapturePhoto;
        RetakePhotoButton.IsEnabled = CanRetakePhoto;

        CameraPreviewElement.Visibility = isCameraPreviewing
            ? Visibility.Visible
            : Visibility.Collapsed;

        CapturedCameraImageControl.Source = capturedCameraImage;
        CapturedCameraImageControl.Visibility = hasCapturedCameraImage
            ? Visibility.Visible
            : Visibility.Collapsed;

        CameraPlaceholderPanel.Visibility = ShowCameraPlaceholder
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
