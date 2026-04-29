using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Controls;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Microsoft.Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using WinRT.Interop;
using ZXing;
using System.Linq;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class DecodingViewModel : ObservableRecipient, INavigationAware
{
    [ObservableProperty]
    public partial string InfoBarMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsInfoBarShowing { get; set; } = false;

    [ObservableProperty]
    public partial string DecodedContentInfoBarTitle { get; set; } = "QR Code Content";

    [ObservableProperty]
    public partial InfoBarSeverity DecodedContentInfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsDecodedContentUrl { get; set; } = false;

    [ObservableProperty]
    public partial bool IsLikelyRedirector { get; set; } = false;

    [ObservableProperty]
    public partial string RedirectorWarningMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsRedirectorWarningVisible { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    public partial BitmapImage? PickedImage { get; set; }

    [ObservableProperty]
    public partial bool CanPasteImage { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(HasActivePreviewSurface))]
    public partial DecodingImageItem? CurrentDecodingItem { get; set; } = null;

    [ObservableProperty]
    public partial bool IsAdvancedToolsVisible { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivePreviewSurface))]
    public partial bool IsCameraPaneOpen { get; set; } = false;

    [ObservableProperty]
    public partial bool IsSidePaneOpen { get; set; } = false;

    [ObservableProperty]
    public partial bool IsFaqPaneOpen { get; set; } = false;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial string LoadingMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDecodingHistoryPaneOpen { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDecodingHistory))]
    public partial ObservableCollection<DecodingHistoryItem> DecodingHistoryItems { get; set; } = [];

    public bool HasDecodingHistory => DecodingHistoryItems.Count > 0;

    public AdvancedToolsViewModel AdvancedTools { get; } = new();

    public bool HasImage => CurrentDecodingItem is not null;

    public bool HasActivePreviewSurface => HasImage || IsCameraPaneOpen;

    public bool CanOpenCurrentSourceFile => HasImage
        && currentSourceKind == DecodingSourceKind.File
        && !string.IsNullOrEmpty(currentSourceFilePath)
        && File.Exists(currentSourceFilePath);

    public bool CanSaveCurrentDecodedImage => HasImage
        && !string.IsNullOrEmpty(currentCachedImagePath)
        && File.Exists(currentCachedImagePath);

    public bool CanOpenCurrentContainingFolder => CanOpenCurrentSourceFile;

    public string CurrentImageTitle => currentSourceKind switch
    {
        DecodingSourceKind.File when !string.IsNullOrEmpty(currentSourceFilePath)
            => Path.GetFileName(currentSourceFilePath),
        DecodingSourceKind.Clipboard => "Clipboard image",
        DecodingSourceKind.Camera => "Camera capture",
        DecodingSourceKind.SnippingTool => "Snipping Tool capture",
        _ => CurrentDecodingItem?.FileName ?? string.Empty,
    };

    public event EventHandler? CameraPaneOpenChanged;
    public event EventHandler? PreviewStateChanged;

    private HistoryItem? navigationHistoryItem = null;
    private bool warnWhenLikelyRedirector = RedirectorWarningSettingsHelper.DefaultWarnWhenLikelyRedirector;
    private IReadOnlyList<string> safeRedirectorDomains = [];
    private DecodingSourceKind nextSourceKind = DecodingSourceKind.File;
    private bool suppressHistorySave = false;
    private string? nextSourceFilePathOverride = null;
    private DecodingSourceKind currentSourceKind = DecodingSourceKind.File;
    private string? currentSourceFilePath = null;
    private string? currentCachedImagePath = null;

    private INavigationService NavigationService { get; }
    public ILocalSettingsService LocalSettingsService { get; }

    private readonly List<string> imageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".svg",
    ];

    public DecodingViewModel(INavigationService navigationService, ILocalSettingsService localSettingsService)
    {
        NavigationService = navigationService;
        LocalSettingsService = localSettingsService;

        AttachClipboardHandler();
    }

    partial void OnIsAdvancedToolsVisibleChanged(bool value)
    {
        if (value && IsCameraPaneOpen)
            IsCameraPaneOpen = false;

        IsSidePaneOpen = value || IsCameraPaneOpen;
    }

    partial void OnIsCameraPaneOpenChanged(bool value)
    {
        if (value && IsAdvancedToolsVisible)
            IsAdvancedToolsVisible = false;

        IsSidePaneOpen = value || IsAdvancedToolsVisible;
        CameraPaneOpenChanged?.Invoke(this, EventArgs.Empty);
        PreviewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnCurrentDecodingItemChanged(DecodingImageItem? value)
    {
        IsAdvancedToolsVisible = false;
        AdvancedTools.ClearAll();
        ResetDecodedContentInfoBar();

        if (value?.OriginalMagickImage is not null)
            AdvancedTools.SetOriginalImage(value.OriginalMagickImage);

        if (value is not null)
        {
            currentSourceKind = nextSourceKind;
            currentSourceFilePath = nextSourceKind == DecodingSourceKind.File
                ? (nextSourceFilePathOverride ?? value.ImagePath)
                : null;
            currentCachedImagePath = !string.IsNullOrEmpty(value.CachedBitmapPath)
                ? value.CachedBitmapPath
                : value.ImagePath;
            nextSourceFilePathOverride = null;

            if (!suppressHistorySave)
                _ = SaveToDecodingHistoryAsync(value);
            suppressHistorySave = false;
        }
        else
        {
            currentSourceFilePath = null;
            currentCachedImagePath = null;
            nextSourceFilePathOverride = null;
        }

        OnPropertyChanged(nameof(CanOpenCurrentSourceFile));
        OnPropertyChanged(nameof(CanSaveCurrentDecodedImage));
        OnPropertyChanged(nameof(CanOpenCurrentContainingFolder));
        OnPropertyChanged(nameof(CurrentImageTitle));

        PreviewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    private bool isWaitingForSnippingTool = false;
    private uint clipboardSequenceOnSnipLaunch = 0;

    private async void Clipboard_ContentChanged(object? sender, object e)
    {
        CheckIfCanPaste();

        if (isWaitingForSnippingTool && CanPasteImage)
        {
            nextSourceKind = DecodingSourceKind.SnippingTool;
            isWaitingForSnippingTool = false;
            await OpenFileFromClipboardCommand.ExecuteAsync(null);
        }
    }

    private void CheckIfCanPaste()
    {
        try
        {
            DataPackageView clipboardData = Clipboard.GetContent();

            if (clipboardData.Contains(StandardDataFormats.StorageItems)
                || clipboardData.Contains(StandardDataFormats.Bitmap))
                CanPasteImage = true;
            else
                CanPasteImage = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to check clipboard: {ex.Message}");
            CanPasteImage = false;
        }
    }

    private void AttachClipboardHandler()
    {
        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        Clipboard.ContentChanged += Clipboard_ContentChanged;
        CheckIfCanPaste();

        App.MainWindow.Activated -= MainWindow_Activated;
        App.MainWindow.Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated || !isWaitingForSnippingTool)
            return;

        // Yield briefly so Clipboard_ContentChanged can fire first if the clipboard
        // was already updated by the snipping tool before the window re-activated.
        await Task.Delay(150);

        if (!isWaitingForSnippingTool)
            return; // Clipboard_ContentChanged already handled it

        // If the clipboard sequence number hasn't changed the user canceled the snip —
        // don't fall back to pasting whatever was in the clipboard before.
        if (GetClipboardSequenceNumber() == clipboardSequenceOnSnipLaunch)
        {
            isWaitingForSnippingTool = false;
            return;
        }

        nextSourceKind = DecodingSourceKind.SnippingTool;
        isWaitingForSnippingTool = false;
        CheckIfCanPaste();
        if (CanPasteImage)
            await OpenFileFromClipboardCommand.ExecuteAsync(null);
    }

    private void ResetViewState()
    {
        if (CurrentDecodingItem is not null)
        {
            CurrentDecodingItem.BitmapImage = null;
            CurrentDecodingItem.ProcessedBitmapImage = null;
            CurrentDecodingItem.CodeBorders.Clear();
            CurrentDecodingItem.PerspectiveCornerMarkers.Clear();
            CurrentDecodingItem.CurrentCornerIndex = 0;
            CurrentDecodingItem.OriginalMagickImage = null;
            CurrentDecodingItem.ProcessedMagickImage = null;
        }

        CurrentDecodingItem = null;
        PickedImage = null;
        ResetDecodedContentInfoBar();
        IsAdvancedToolsVisible = false;
        IsCameraPaneOpen = false;
        IsSidePaneOpen = false;
        IsFaqPaneOpen = false;
        IsLoading = false;
        LoadingMessage = string.Empty;
    }

    private void ResetDecodedContentInfoBar()
    {
        IsInfoBarShowing = false;
        DecodedContentInfoBarTitle = "QR Code Content";
        DecodedContentInfoBarSeverity = InfoBarSeverity.Informational;
        InfoBarMessage = string.Empty;
        IsDecodedContentUrl = false;
        IsLikelyRedirector = false;
        RedirectorWarningMessage = string.Empty;
        IsRedirectorWarningVisible = false;
    }

    private void UpdateDecodedContentInfoBar(string decodedText)
    {
        RedirectorUrlClassification classification = RedirectorWarningHelper.Classify(decodedText, safeRedirectorDomains);

        InfoBarMessage = decodedText;
        IsDecodedContentUrl = classification.IsAbsoluteUri;
        IsLikelyRedirector = warnWhenLikelyRedirector && classification.ShouldWarn;

        if (warnWhenLikelyRedirector && classification.ShouldWarn)
        {
            DecodedContentInfoBarTitle = "Known redirector detected";
            DecodedContentInfoBarSeverity = InfoBarSeverity.Warning;
            RedirectorWarningMessage = $"This code points to {classification.Host}, which is a known redirector. Open it carefully because the final destination is hidden until after the redirect.";
            IsRedirectorWarningVisible = true;
        }
        else
        {
            DecodedContentInfoBarTitle = "QR Code Content";
            DecodedContentInfoBarSeverity = InfoBarSeverity.Informational;
            RedirectorWarningMessage = string.Empty;
            IsRedirectorWarningVisible = false;
        }

        IsInfoBarShowing = true;
    }

    public async void OnNavigatedTo(object parameter)
    {
        AttachClipboardHandler();
        ResetViewState();
        warnWhenLikelyRedirector = await RedirectorWarningSettingsHelper.ReadWarningEnabledAsync(LocalSettingsService);
        safeRedirectorDomains = await RedirectorWarningSettingsHelper.ReadSafeDomainsAsync(LocalSettingsService);

        DecodingHistoryItems = await DecodingHistoryStorageHelper.LoadHistoryAsync();

        // Store the HistoryItem to pass back when returning to main page
        if (parameter is HistoryItem historyItem)
        {
            navigationHistoryItem = historyItem;
        }
        // For backward compatibility, also handle string parameter
        else if (parameter is string url)
        {
            navigationHistoryItem = new HistoryItem { CodesContent = url };
        }
        // Handle a shared or opened image file
        else if (parameter is StorageFile storageFile)
        {
            nextSourceKind = DecodingSourceKind.File;
            await OpenAndDecodeStorageFile(storageFile);
        }
    }

    [RelayCommand]
    private void ClearImages()
    {
        ResetViewState();
    }

    [RelayCommand]
    private void ToggleAdvancedTools()
    {
        if (!HasImage)
            return;

        IsAdvancedToolsVisible = !IsAdvancedToolsVisible;
    }

    [RelayCommand]
    private async Task CapturePhoto()
    {
        nextSourceKind = DecodingSourceKind.Camera;
        IsLoading = true;
        LoadingMessage = "Opening camera…";

        try
        {
            CameraCaptureUI dialog = new CameraCaptureUI(App.MainWindow.AppWindow.Id);
            dialog.PhotoSettings.AllowCropping = false;

            // Start the async operation then yield so the dispatcher renders
            // "Opening camera…" before we synchronously overwrite the message.
            var captureOperation = dialog.CaptureFileAsync(CameraCaptureUIMode.Photo);
            await Task.Yield();
            LoadingMessage = "Taking picture…";

            StorageFile? file = await captureOperation;
            if (file is not null)
            {
                LoadingMessage = "Processing image…";
                await OpenAndDecodeStorageFile(file);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Camera capture failed: {ex}");
            DecodedContentInfoBarTitle = "Camera unavailable";
            InfoBarMessage = ex.Message;
            DecodedContentInfoBarSeverity = InfoBarSeverity.Error;
            IsInfoBarShowing = true;
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleCameraPane() => IsCameraPaneOpen = !IsCameraPaneOpen;

    [RelayCommand]
    private async Task LaunchSnippingTool()
    {
        clipboardSequenceOnSnipLaunch = GetClipboardSequenceNumber();
        isWaitingForSnippingTool = true;
        await Launcher.LaunchUriAsync(new Uri("ms-screenclip:"));
    }

    [RelayCommand]
    private void ToggleFaqPaneOpen() => IsFaqPaneOpen = !IsFaqPaneOpen;

    [RelayCommand]
    private void OpenFaqWithSearch(string searchText)
    {
        IsFaqPaneOpen = true;
        WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, searchText));
    }

    [RelayCommand]
    private async Task ApplyAdvancedToolsAndRedecode(DecodingImageItem item)
    {
        if (item?.ProcessedMagickImage == null)
            return;

        IsLoading = true;
        LoadingMessage = "Applying adjustments…";

        try
        {
            Bitmap bitmap = ImageProcessingHelper.ConvertToBitmap(item.ProcessedMagickImage);

            string cachePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{DateTimeOffset.Now.Ticks}_processed.png");
            bitmap.Save(cachePath);

            Uri uri = new($"{cachePath}?tick={DateTimeOffset.Now.Ticks}");
            BitmapImage processedBitmapImage = new(uri)
            {
                CreateOptions = BitmapCreateOptions.IgnoreImageCache
            };

            IEnumerable<(string, Result)> strings = BarcodeHelpers.GetStringsFromBitmap(bitmap);

            ObservableCollection<TextBorder> codeBorders = [];
            foreach ((string, Result) resultItem in strings)
            {
                TextBorder textBorder = new(resultItem.Item2);
                textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
                codeBorders.Add(textBorder);
            }

            item.CachedBitmapPath = cachePath;
            item.ProcessedBitmapImage = processedBitmapImage;
            item.CodeBorders = codeBorders;
            item.ImagePixelWidth = (int)item.ProcessedMagickImage.Width;
            item.ImagePixelHeight = (int)item.ProcessedMagickImage.Height;
            item.BitmapImage = processedBitmapImage;

            List<string> decodedTexts = codeBorders.Select(b => b.BorderInfo.Text).ToList();
            await DecodingHistoryStorageHelper.UpdateLatestAndSaveAsync(DecodingHistoryItems, cachePath, decodedTexts);
            OnPropertyChanged(nameof(HasDecodingHistory));
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task OpenFileFromClipboard()
    {
        IsLoading = true;
        LoadingMessage = "Opening image from clipboard…";

        try
        {
            CurrentDecodingItem = null;
            DataPackageView clipboardData = Clipboard.GetContent();

            if (clipboardData.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> clipboardItems = await clipboardData.GetStorageItemsAsync();
                if (clipboardItems.Count == 0)
                    return;

                nextSourceKind = DecodingSourceKind.File;
                OpenAndDecodeStorageFiles(clipboardItems);
            }
            else if (clipboardData.Contains(StandardDataFormats.Bitmap))
            {
                if (nextSourceKind != DecodingSourceKind.SnippingTool)
                    nextSourceKind = DecodingSourceKind.Clipboard;

                RandomAccessStreamReference bitmapStreamRef = await clipboardData.GetBitmapAsync();

                IRandomAccessStreamWithContentType stream = await bitmapStreamRef.OpenReadAsync();

                if (stream is not null)
                    await OpenAndDecodeBitmapAsync(stream);
            }
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task TryLaunchLink()
    {
        bool success = Uri.TryCreate(InfoBarMessage, UriKind.Absolute, out Uri? uri);

        if (success && uri is not null)
            _ = await Launcher.LaunchUriAsync(uri);
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo(typeof(MainViewModel).FullName!, navigationHistoryItem);
    }

    [RelayCommand]
    private void AddMessageToClipboard()
    {
        if (string.IsNullOrWhiteSpace(InfoBarMessage))
            return;

        DataPackage dataPackage = new();
        dataPackage.SetText(InfoBarMessage);
        Clipboard.SetContent(dataPackage);
    }

    [RelayCommand]
    private async Task OpenNewFile()
    {
        nextSourceKind = DecodingSourceKind.File;
        PickedImage = null;
        CurrentDecodingItem = null;
        IsInfoBarShowing = false;

        IsLoading = true;
        LoadingMessage = "Opening image…";

        try
        {
            FileOpenPicker fileOpenPicker = new()
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
            };

            foreach (string extension in imageExtensions)
                fileOpenPicker.FileTypeFilter.Add(extension);

            Window saveWindow = new();
            IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
            InitializeWithWindow.Initialize(fileOpenPicker, windowHandleSave);

            StorageFile? pickedFile = await fileOpenPicker.PickSingleFileAsync();

            if (pickedFile is null)
                return;

            await OpenAndDecodeStorageFile(pickedFile);
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void EditQrCode(object parameter)
    {
        if (string.IsNullOrWhiteSpace(InfoBarMessage))
            return;

        bool isVCard = VCardBuilderHelper.IsVCard(InfoBarMessage);
        bool isWifi = WifiBuilderHelper.IsWifi(InfoBarMessage);
        bool isEmail = EmailBuilderHelper.IsEmail(InfoBarMessage);
        QrContentKind contentKind = isVCard
            ? QrContentKind.VCard
            : isWifi
                ? QrContentKind.WiFi
                : isEmail
                    ? QrContentKind.Email
                    : QrContentKind.PlainText;
        MultiLineCodeMode? overrideMode = contentKind == QrContentKind.PlainText
            ? null
            : MultiLineCodeMode.MultilineOneCode;

        // Create a new HistoryItem with the decoded text, preserving other state if available
        HistoryItem editHistoryItem = navigationHistoryItem != null
            ? new HistoryItem
            {
                CodesContent = InfoBarMessage,
                ContentKind = contentKind,
                MultiLineCodeModeOverride = overrideMode,
                Foreground = navigationHistoryItem.Foreground,
                Background = navigationHistoryItem.Background,
                ErrorCorrection = navigationHistoryItem.ErrorCorrection,
                LogoImagePath = navigationHistoryItem.LogoImagePath,
                LogoEmoji = navigationHistoryItem.LogoEmoji,
                LogoEmojiStyle = navigationHistoryItem.LogoEmojiStyle,
                LogoSizePercentage = navigationHistoryItem.LogoSizePercentage,
                LogoPaddingPixels = navigationHistoryItem.LogoPaddingPixels,
            }
            : new HistoryItem
            {
                CodesContent = InfoBarMessage,
                ContentKind = contentKind,
                MultiLineCodeModeOverride = overrideMode,
            };

        NavigationService.NavigateTo(typeof(MainViewModel).FullName!, editHistoryItem);
    }

    private async Task OpenAndDecodeBitmapAsync(IRandomAccessStreamWithContentType streamWithContentType)
    {
        // System.Drawing.Bitmap cannot read clipboard DIB streams (which lack the BMP file
        // header). Use WinRT BitmapDecoder (WIC) which handles all clipboard bitmap formats,
        // then re-encode to PNG so MagickImage has a clean, fully-headered stream to read.
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(streamWithContentType);
        SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        using InMemoryRandomAccessStream pngStream = new();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, pngStream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();
        pngStream.Seek(0);

        MagickImage magickImage = new(pngStream.AsStreamForRead());
        magickImage.AutoOrient();

        // Convert the oriented MagickImage to a Bitmap for ZXing decoding
        // so that ResultPoints are in the same coordinate space as the displayed image
        Bitmap orientedBitmap = ImageProcessingHelper.ConvertToBitmap(magickImage);

        string cachePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{DateTimeOffset.Now.Ticks}.png");
        orientedBitmap.Save(cachePath);

        Uri uri = new($"{cachePath}?tick={DateTimeOffset.Now.Ticks}");
        BitmapImage thisPickedImage = new(uri)
        {
            CreateOptions = BitmapCreateOptions.IgnoreImageCache
        };

        IEnumerable<(string, Result)> strings = BarcodeHelpers.GetStringsFromBitmap(orientedBitmap);

        ObservableCollection<TextBorder> codeBorders = [];
        foreach ((string, Result) item in strings)
        {
            TextBorder textBorder = new(item.Item2);
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            codeBorders.Add(textBorder);
        }

        DecodingImageItem decodingImage = new()
        {
            ImagePath = cachePath,
            CachedBitmapPath = cachePath,
            BitmapImage = thisPickedImage,
            ImagePixelWidth = (int)magickImage.Width,
            ImagePixelHeight = (int)magickImage.Height,
            CodeBorders = codeBorders,
            OriginalMagickImage = magickImage,
        };

        CurrentDecodingItem = decodingImage;
    }

    public async void OpenAndDecodeStorageFiles(IReadOnlyList<IStorageItem> pickedFiles)
    {
        nextSourceKind = DecodingSourceKind.File;
        IStorageItem? first = pickedFiles.Count > 0 ? pickedFiles[0] : null;
        if (first is StorageFile storageFile)
            await OpenAndDecodeStorageFile(storageFile);
    }

    public async Task OpenAndDecodeStorageFile(StorageFile storageFile)
    {
        IsLoading = true;
        LoadingMessage = "Opening image\u2026";

        try
        {
            DecodingImageItem? decodedItem = await GetDecodingImageItemFromStorageFileAsync(storageFile);
            if (decodedItem is not null)
                CurrentDecodingItem = decodedItem;
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    private async Task<DecodingImageItem?> GetDecodingImageItemFromStorageFileAsync(StorageFile storageFile)
    {
        if (!imageExtensions.Contains(Path.GetExtension(storageFile.Path).ToLowerInvariant()))
            return null;

        // Load image using ImageProcessingHelper which handles EXIF orientation properly
        MagickImage magickImage = await ImageProcessingHelper.LoadImageFromStorageFile(storageFile);

        // Convert the oriented MagickImage to a Bitmap for ZXing decoding
        // so that ResultPoints are in the same coordinate space as the displayed image
        Bitmap orientedBitmap = ImageProcessingHelper.ConvertToBitmap(magickImage);

        string cachePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{DateTimeOffset.Now.Ticks}.png");
        orientedBitmap.Save(cachePath);

        Uri uri = new($"{cachePath}?tick={DateTimeOffset.Now.Ticks}");
        BitmapImage thisPickedImage = new(uri)
        {
            CreateOptions = BitmapCreateOptions.IgnoreImageCache
        };

        IEnumerable<(string, Result)> strings = BarcodeHelpers.GetStringsFromBitmap(orientedBitmap);

        ObservableCollection<TextBorder> codeBorders = [];
        foreach ((string, Result) item in strings)
        {
            TextBorder textBorder = new(item.Item2);
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            codeBorders.Add(textBorder);
        }

        DecodingImageItem decodingImage = new()
        {
            ImagePath = storageFile.Path,
            CachedBitmapPath = cachePath,
            BitmapImage = thisPickedImage,
            ImagePixelWidth = (int)magickImage.Width,
            ImagePixelHeight = (int)magickImage.Height,
            CodeBorders = codeBorders,
            OriginalMagickImage = magickImage,
        };

        return decodingImage;
    }

    private void TextBorder_TextBorder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBorder textBorder)
            return;

        UpdateDecodedContentInfoBar(textBorder.BorderInfo.Text);
    }

    [RelayCommand]
    private void ToggleDecodingHistoryPaneOpen() => IsDecodingHistoryPaneOpen = !IsDecodingHistoryPaneOpen;

    [RelayCommand]
    private async Task ClearDecodingHistory()
    {
        DecodingHistoryItems.Clear();
        await DecodingHistoryStorageHelper.SaveHistoryAsync(DecodingHistoryItems);
        OnPropertyChanged(nameof(HasDecodingHistory));
    }

    [RelayCommand]
    private async Task OpenCurrentSourceFile()
    {
        if (!CanOpenCurrentSourceFile || string.IsNullOrEmpty(currentSourceFilePath))
            return;

        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(currentSourceFilePath);
            await Launcher.LaunchFileAsync(file);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open source file: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveCurrentDecodedImage()
    {
        if (!CanSaveCurrentDecodedImage || string.IsNullOrEmpty(currentCachedImagePath))
            return;

        string suggestedFileName = !string.IsNullOrEmpty(currentSourceFilePath)
            ? Path.GetFileNameWithoutExtension(currentSourceFilePath)
            : Path.GetFileNameWithoutExtension(currentCachedImagePath);

        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = suggestedFileName,
        };
        picker.FileTypeChoices.Add("PNG Image", [".png"]);

        Window saveWindow = new();
        IntPtr hwnd = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        StorageFile? destFile = await picker.PickSaveFileAsync();
        if (destFile is null)
            return;

        try
        {
            await Task.Run(() => File.Copy(currentCachedImagePath, destFile.Path, overwrite: true));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save image: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenCurrentContainingFolder()
    {
        if (!CanOpenCurrentContainingFolder || string.IsNullOrEmpty(currentSourceFilePath))
            return;

        string? folderPath = Path.GetDirectoryName(currentSourceFilePath);
        if (folderPath is null)
            return;

        try
        {
            StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            FolderLauncherOptions options = new();
            StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(currentSourceFilePath);
            options.ItemsToSelect.Add(sourceFile);
            await Launcher.LaunchFolderAsync(folder, options);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open containing folder: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenDecodingHistoryItem(DecodingHistoryItem item)
    {
        if (!item.HasSavedImage)
            return;

        suppressHistorySave = true;
        nextSourceKind = item.SourceKind;
        nextSourceFilePathOverride = item.SourceFilePath;

        try
        {
            StorageFile savedFile = await StorageFile.GetFileFromPathAsync(item.SavedImagePath);
            await OpenAndDecodeStorageFile(savedFile);
            IsDecodingHistoryPaneOpen = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open history item: {ex.Message}");
            suppressHistorySave = false;
            nextSourceFilePathOverride = null;
        }
    }

    private async Task SaveToDecodingHistoryAsync(DecodingImageItem item)
    {
        // Capture UI-bound data synchronously before any awaits
        string pathToSave = !string.IsNullOrEmpty(item.CachedBitmapPath)
            ? item.CachedBitmapPath
            : item.ImagePath;
        List<string> texts = item.CodeBorders.Select(b => b.BorderInfo.Text).ToList();
        string? sourceFilePath = nextSourceKind == DecodingSourceKind.File ? item.ImagePath : null;
        DecodingSourceKind sourceKind = nextSourceKind;

        string? savedImagePath = await DecodingHistoryStorageHelper.SaveImageCopyAsync(pathToSave);

        DecodingHistoryItem historyItem = new()
        {
            SavedImagePath = savedImagePath ?? string.Empty,
            SourceKind = sourceKind,
            SourceFilePath = sourceFilePath,
            DecodedTexts = texts,
        };

        await DecodingHistoryStorageHelper.AddAndSaveAsync(DecodingHistoryItems, historyItem);
        OnPropertyChanged(nameof(HasDecodingHistory));
    }

    public void OnNavigatedFrom()
    {
        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        App.MainWindow.Activated -= MainWindow_Activated;
        ResetViewState();
    }
}
