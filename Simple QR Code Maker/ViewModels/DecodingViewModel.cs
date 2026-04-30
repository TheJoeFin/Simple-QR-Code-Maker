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

public partial class DecodingViewModel : ObservableRecipient, INavigationAware, INavigationStateProvider
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
    public partial bool IsCancellableOperationRunning { get; set; } = false;

    [ObservableProperty]
    public partial bool IsDecodingHistoryPaneOpen { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDecodingHistory))]
    public partial ObservableCollection<DecodingHistoryItem> DecodingHistoryItems { get; set; } = [];

    public bool HasDecodingHistory => DecodingHistoryItems.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeftPaneOpen))]
    public partial bool IsFolderPaneOpen { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeftPaneOpen))]
    public partial bool IsCutOutImagesPaneOpen { get; set; } = false;

    public bool IsLeftPaneOpen => IsFolderPaneOpen || IsCutOutImagesPaneOpen;

    [ObservableProperty]
    public partial string FolderPaneFolderName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolderFiles))]
    public partial ObservableCollection<FolderFileItem> FolderFiles { get; set; } = [];

    public bool HasFolderFiles => FolderFiles.Count > 0;

    public AdvancedToolsViewModel AdvancedTools { get; } = new();

    public ObservableCollection<DecodingImageItem> DecodingImageItems { get; } = [];

    public bool IsMultiImagePanelVisible => DecodingImageItems.Count > 1;

    public bool HasLeftPaneContent => HasFolderFiles || IsMultiImagePanelVisible;

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
    private StorageFolder? currentFolderPaneFolder = null;
    private FolderFileItem? currentFolderFileItem = null;

    private const int LargeFolderThreshold = 50;

    private CancellationTokenSource? _batchCts;

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
        AdvancedTools.RegionCutOut += AdvancedTools_RegionCutOut;
    }

    private async void AdvancedTools_RegionCutOut(object? sender, ImageMagick.MagickImage croppedImage)
    {
        await AddCutOutImageAsync(croppedImage);
    }

    private async Task AddCutOutImageAsync(ImageMagick.MagickImage croppedImage)
    {
        IsLoading = true;
        LoadingMessage = "Extracting region…";

        try
        {
            System.Drawing.Bitmap bitmap = ImageProcessingHelper.ConvertToBitmap(croppedImage);

            string cachePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{DateTimeOffset.Now.Ticks}_cutout.png");
            bitmap.Save(cachePath);

            Uri uri = new($"{cachePath}?tick={DateTimeOffset.Now.Ticks}");
            BitmapImage bitmapImage = new(uri) { CreateOptions = BitmapCreateOptions.IgnoreImageCache };

            IEnumerable<(string, Result)> strings = BarcodeHelpers.GetStringsFromBitmap(bitmap);
            ObservableCollection<TextBorder> codeBorders = [];
            foreach ((string, Result) resultItem in strings)
            {
                TextBorder textBorder = new(resultItem.Item2);
                textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
                codeBorders.Add(textBorder);
            }

            if (HasFolderFiles && currentFolderFileItem != null)
            {
                int cutOutNumber = currentFolderFileItem.CutOuts.Count + 1;
                DecodingImageItem cutOutItem = new()
                {
                    ImagePath = cachePath,
                    CachedBitmapPath = cachePath,
                    BitmapImage = bitmapImage,
                    ImagePixelWidth = (int)croppedImage.Width,
                    ImagePixelHeight = (int)croppedImage.Height,
                    CodeBorders = codeBorders,
                    OriginalMagickImage = croppedImage,
                    Label = $"Cut-out {cutOutNumber}",
                    ParentFileName = currentFolderFileItem.FileName,
                };
                currentFolderFileItem.CutOuts.Add(cutOutItem);
                IsFolderPaneOpen = true;
                SelectDecodingImageItem(cutOutItem);
            }
            else
            {
                if (DecodingImageItems.Count == 1 && string.IsNullOrEmpty(DecodingImageItems[0].Label))
                    DecodingImageItems[0].Label = "Original";

                int regionNumber = DecodingImageItems.Count;
                DecodingImageItem cutOutItem = new()
                {
                    ImagePath = cachePath,
                    CachedBitmapPath = cachePath,
                    BitmapImage = bitmapImage,
                    ImagePixelWidth = (int)croppedImage.Width,
                    ImagePixelHeight = (int)croppedImage.Height,
                    CodeBorders = codeBorders,
                    OriginalMagickImage = croppedImage,
                    Label = $"Region {regionNumber}",
                };
                DecodingImageItems.Add(cutOutItem);
                OnPropertyChanged(nameof(IsMultiImagePanelVisible));
                OnPropertyChanged(nameof(HasLeftPaneContent));
                IsCutOutImagesPaneOpen = true;
                SelectDecodingImageItem(cutOutItem);
            }
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    public void SelectDecodingImageItem(DecodingImageItem item)
    {
        if (item == CurrentDecodingItem)
            return;

        suppressHistorySave = true;
        CurrentDecodingItem = item;
    }

    [RelayCommand]
    private async Task SaveCutOut(DecodingImageItem item)
    {
        if (string.IsNullOrEmpty(item.ImagePath) || !File.Exists(item.ImagePath))
            return;

        // Auto-save to the open folder and promote to a regular folder item.
        if (currentFolderPaneFolder is not null && !string.IsNullOrEmpty(item.ParentFileName))
        {
            await SaveCutOutToFolderAsync(item);
            return;
        }

        // Fallback: show a save picker for cut-outs made outside of a folder session.
        string nameWithoutExt = string.IsNullOrEmpty(item.ParentFileName)
            ? "image"
            : Path.GetFileNameWithoutExtension(item.ParentFileName);

        string labelPart = item.Label.Replace("-", "").Replace(" ", "").ToLowerInvariant();
        string suggestedName = $"{nameWithoutExt}-{labelPart}";

        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = suggestedName,
        };
        picker.FileTypeChoices.Add("PNG Image", [".png"]);

        Window saveWindow = new();
        IntPtr hwnd = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        StorageFile? destFile = await picker.PickSaveFileAsync();
        if (destFile is null)
            return;

        await Task.Run(() => File.Copy(item.ImagePath, destFile.Path, overwrite: true));
    }

    private async Task SaveCutOutToFolderAsync(DecodingImageItem item)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(item.ParentFileName);
        string labelPart = item.Label.Replace("-", "").Replace(" ", "").ToLowerInvariant();
        string fileName = $"{nameWithoutExt}-{labelPart}.png";

        StorageFile savedFile = await currentFolderPaneFolder!.CreateFileAsync(
            fileName, CreationCollisionOption.GenerateUniqueName);
        await Task.Run(() => File.Copy(item.ImagePath, savedFile.Path, overwrite: true));

        // Remove the cut-out from its parent's child list.
        FolderFileItem? parentItem = FolderFiles.FirstOrDefault(
            f => string.Equals(f.FileName, item.ParentFileName, StringComparison.OrdinalIgnoreCase));
        parentItem?.CutOuts.Remove(item);

        // Insert a new regular folder item right after the parent (or at the end).
        FolderFileItem newFolderItem = new(savedFile);
        await newFolderItem.LoadThumbnailAsync();

        int insertIndex = parentItem is not null
            ? FolderFiles.IndexOf(parentItem) + 1
            : FolderFiles.Count;
        FolderFiles.Insert(insertIndex, newFolderItem);

        OnPropertyChanged(nameof(HasFolderFiles));
        OnPropertyChanged(nameof(HasLeftPaneContent));

        // If this cut-out was being viewed, update tracking so subsequent actions
        // treat the saved file as the current folder item.
        if (CurrentDecodingItem == item)
        {
            currentFolderFileItem = newFolderItem;
            currentSourceFilePath = savedFile.Path;
            item.ImagePath = savedFile.Path;
            OnPropertyChanged(nameof(CanOpenCurrentSourceFile));
            OnPropertyChanged(nameof(CanOpenCurrentContainingFolder));
            OnPropertyChanged(nameof(CurrentImageTitle));
        }
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

    partial void OnIsFolderPaneOpenChanged(bool value)
    {
        if (value && IsDecodingHistoryPaneOpen)
            IsDecodingHistoryPaneOpen = false;
        if (value && IsCutOutImagesPaneOpen)
            IsCutOutImagesPaneOpen = false;
    }

    partial void OnIsCutOutImagesPaneOpenChanged(bool value)
    {
        if (value && IsDecodingHistoryPaneOpen)
            IsDecodingHistoryPaneOpen = false;
        if (value && IsFolderPaneOpen)
            IsFolderPaneOpen = false;
    }

    [RelayCommand]
    private void CloseCutOutImagesPane() => IsCutOutImagesPaneOpen = false;

    [RelayCommand]
    private void ToggleLeftPane()
    {
        if (IsMultiImagePanelVisible)
            IsCutOutImagesPaneOpen = !IsCutOutImagesPaneOpen;
        else if (HasFolderFiles)
            IsFolderPaneOpen = !IsFolderPaneOpen;
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
        DecodingImageItems.Clear();
        OnPropertyChanged(nameof(IsMultiImagePanelVisible));
        PickedImage = null;
        ResetDecodedContentInfoBar();
        IsAdvancedToolsVisible = false;
        IsCameraPaneOpen = false;
        IsSidePaneOpen = false;
        IsFaqPaneOpen = false;
        IsFolderPaneOpen = false;
        IsCutOutImagesPaneOpen = false;
        FolderFiles.Clear();
        FolderPaneFolderName = string.Empty;
        currentFolderPaneFolder = null;
        currentFolderFileItem = null;
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

    public object? CreateNavigationState()
    {
        return new DecodingNavigationState
        {
            NavigationHistoryItem = CloneHistoryItem(navigationHistoryItem),
            CurrentSourceKind = currentSourceKind,
            CurrentSourceFilePath = currentSourceFilePath,
            CurrentCachedImagePath = currentCachedImagePath,
            CurrentFolderPath = currentFolderPaneFolder?.Path,
            CurrentFolderFilePath = currentFolderFileItem?.File.Path,
            InfoBarMessage = InfoBarMessage,
            IsInfoBarShowing = IsInfoBarShowing,
            DecodedContentInfoBarTitle = DecodedContentInfoBarTitle,
            DecodedContentInfoBarSeverity = DecodedContentInfoBarSeverity,
            IsDecodedContentUrl = IsDecodedContentUrl,
            IsLikelyRedirector = IsLikelyRedirector,
            RedirectorWarningMessage = RedirectorWarningMessage,
            IsRedirectorWarningVisible = IsRedirectorWarningVisible,
            IsAdvancedToolsVisible = IsAdvancedToolsVisible,
            IsCameraPaneOpen = IsCameraPaneOpen,
            IsFaqPaneOpen = IsFaqPaneOpen,
            IsDecodingHistoryPaneOpen = IsDecodingHistoryPaneOpen,
            IsFolderPaneOpen = IsFolderPaneOpen,
            IsCutOutImagesPaneOpen = IsCutOutImagesPaneOpen,
            FolderPaneFolderName = FolderPaneFolderName,
            DecodingImageItems = [.. DecodingImageItems.Select(CreateDecodingImageNavigationState)],
            FolderFiles = [.. FolderFiles.Select(CreateFolderFileNavigationState)],
            CurrentDecodingItem = CurrentDecodingItem is not null
                ? CreateDecodingImageNavigationState(CurrentDecodingItem)
                : null,
        };
    }

    public async void OnNavigatedTo(object parameter)
    {
        AttachClipboardHandler();
        warnWhenLikelyRedirector = await RedirectorWarningSettingsHelper.ReadWarningEnabledAsync(LocalSettingsService);
        safeRedirectorDomains = await RedirectorWarningSettingsHelper.ReadSafeDomainsAsync(LocalSettingsService);

        DecodingHistoryItems = await DecodingHistoryStorageHelper.LoadHistoryAsync();

        if (parameter is DecodingNavigationState navigationState)
        {
            await RestoreNavigationStateAsync(navigationState);
            return;
        }

        ResetViewState();

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

    private async Task RestoreNavigationStateAsync(DecodingNavigationState state)
    {
        ResetViewState();
        navigationHistoryItem = CloneHistoryItem(state.NavigationHistoryItem);
        nextSourceKind = state.CurrentSourceKind;

        Dictionary<string, DecodingImageItem> restoredImages = new(StringComparer.OrdinalIgnoreCase);

        foreach (DecodingImageNavigationState imageState in state.DecodingImageItems)
        {
            DecodingImageItem? restoredItem = await RestoreDecodingImageItemAsync(imageState);
            if (restoredItem is null)
                continue;

            DecodingImageItems.Add(restoredItem);
            restoredImages[GetNavigationKey(imageState)] = restoredItem;
        }

        OnPropertyChanged(nameof(IsMultiImagePanelVisible));

        FolderPaneFolderName = state.FolderPaneFolderName;
        currentFolderPaneFolder = await GetStorageFolderIfExistsAsync(state.CurrentFolderPath);

        foreach (FolderFileNavigationState folderFileState in state.FolderFiles)
        {
            FolderFileItem? restoredFolderFile = await RestoreFolderFileItemAsync(folderFileState, restoredImages);
            if (restoredFolderFile is not null)
                FolderFiles.Add(restoredFolderFile);
        }

        currentFolderFileItem = FolderFiles.FirstOrDefault(item =>
            string.Equals(item.File.Path, state.CurrentFolderFilePath, StringComparison.OrdinalIgnoreCase));

        OnPropertyChanged(nameof(HasFolderFiles));
        OnPropertyChanged(nameof(HasLeftPaneContent));

        DecodingImageItem? currentItem = null;
        if (state.CurrentDecodingItem is not null)
        {
            string currentItemKey = GetNavigationKey(state.CurrentDecodingItem);
            if (!restoredImages.TryGetValue(currentItemKey, out currentItem))
            {
                currentItem = await RestoreDecodingImageItemAsync(state.CurrentDecodingItem);
                if (currentItem is not null)
                    restoredImages[currentItemKey] = currentItem;
            }
        }

        suppressHistorySave = true;
        CurrentDecodingItem = currentItem;
        suppressHistorySave = false;

        currentSourceKind = state.CurrentSourceKind;
        currentSourceFilePath = state.CurrentSourceFilePath;
        currentCachedImagePath = state.CurrentCachedImagePath;
        nextSourceFilePathOverride = null;

        OnPropertyChanged(nameof(CanOpenCurrentSourceFile));
        OnPropertyChanged(nameof(CanSaveCurrentDecodedImage));
        OnPropertyChanged(nameof(CanOpenCurrentContainingFolder));
        OnPropertyChanged(nameof(CurrentImageTitle));

        InfoBarMessage = state.InfoBarMessage;
        IsInfoBarShowing = state.IsInfoBarShowing;
        DecodedContentInfoBarTitle = state.DecodedContentInfoBarTitle;
        DecodedContentInfoBarSeverity = state.DecodedContentInfoBarSeverity;
        IsDecodedContentUrl = state.IsDecodedContentUrl;
        IsLikelyRedirector = state.IsLikelyRedirector;
        RedirectorWarningMessage = state.RedirectorWarningMessage;
        IsRedirectorWarningVisible = state.IsRedirectorWarningVisible;

        IsCameraPaneOpen = state.IsCameraPaneOpen;
        IsAdvancedToolsVisible = state.IsAdvancedToolsVisible && CurrentDecodingItem is not null;
        IsFaqPaneOpen = state.IsFaqPaneOpen;
        IsDecodingHistoryPaneOpen = state.IsDecodingHistoryPaneOpen;
        IsFolderPaneOpen = state.IsFolderPaneOpen && FolderFiles.Count > 0;
        IsCutOutImagesPaneOpen = state.IsCutOutImagesPaneOpen && DecodingImageItems.Count > 1;
        IsLoading = false;
        LoadingMessage = string.Empty;
    }

    private static DecodingImageNavigationState CreateDecodingImageNavigationState(DecodingImageItem item)
    {
        return new DecodingImageNavigationState
        {
            ImagePath = item.ImagePath,
            CachedBitmapPath = item.CachedBitmapPath,
            ImagePixelWidth = item.ImagePixelWidth,
            ImagePixelHeight = item.ImagePixelHeight,
            IsNoCodesWarningDismissed = item.IsNoCodesWarningDismissed,
            Label = item.Label,
            ParentFileName = item.ParentFileName,
            CodeBorders =
            [
                .. item.CodeBorders.Select(border => new TextBorderNavigationState
                {
                    Text = border.BorderInfo.Text,
                    BorderRect = border.BorderInfo.BorderRect,
                })
            ],
        };
    }

    private static FolderFileNavigationState CreateFolderFileNavigationState(FolderFileItem item)
    {
        return new FolderFileNavigationState
        {
            FilePath = item.File.Path,
            CutOuts = [.. item.CutOuts.Select(CreateDecodingImageNavigationState)],
        };
    }

    private async Task<FolderFileItem?> RestoreFolderFileItemAsync(
        FolderFileNavigationState state,
        IDictionary<string, DecodingImageItem> restoredImages)
    {
        if (string.IsNullOrWhiteSpace(state.FilePath) || !File.Exists(state.FilePath))
            return null;

        StorageFile file = await StorageFile.GetFileFromPathAsync(state.FilePath);
        FolderFileItem folderFileItem = new(file);
        _ = folderFileItem.LoadThumbnailAsync();

        foreach (DecodingImageNavigationState cutOutState in state.CutOuts)
        {
            DecodingImageItem? cutOutItem = await RestoreDecodingImageItemAsync(cutOutState);
            if (cutOutItem is null)
                continue;

            folderFileItem.CutOuts.Add(cutOutItem);
            restoredImages[GetNavigationKey(cutOutState)] = cutOutItem;
        }

        return folderFileItem;
    }

    private async Task<DecodingImageItem?> RestoreDecodingImageItemAsync(DecodingImageNavigationState state)
    {
        string? imagePath = GetExistingImagePath(state);
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;

        MagickImage magickImage = await Task.Run(() =>
        {
            MagickImage restoredImage = new(imagePath);
            restoredImage.AutoOrient();
            return restoredImage;
        });

        Uri uri = new($"{imagePath}?tick={DateTimeOffset.Now.Ticks}");
        BitmapImage bitmapImage = new(uri)
        {
            CreateOptions = BitmapCreateOptions.IgnoreImageCache,
        };

        ObservableCollection<TextBorder> codeBorders = [];
        foreach (TextBorderNavigationState borderState in state.CodeBorders)
        {
            TextBorder textBorder = new(new TextBorderInfo(borderState.Text, borderState.BorderRect));
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            codeBorders.Add(textBorder);
        }

        return new DecodingImageItem
        {
            ImagePath = state.ImagePath,
            CachedBitmapPath = !string.IsNullOrWhiteSpace(state.CachedBitmapPath) ? state.CachedBitmapPath : imagePath,
            BitmapImage = bitmapImage,
            ImagePixelWidth = state.ImagePixelWidth > 0 ? state.ImagePixelWidth : (int)magickImage.Width,
            ImagePixelHeight = state.ImagePixelHeight > 0 ? state.ImagePixelHeight : (int)magickImage.Height,
            CodeBorders = codeBorders,
            IsNoCodesWarningDismissed = state.IsNoCodesWarningDismissed,
            OriginalMagickImage = magickImage,
            ProcessedMagickImage = (MagickImage)magickImage.Clone(),
            Label = state.Label,
            ParentFileName = state.ParentFileName,
        };
    }

    private static string? GetExistingImagePath(DecodingImageNavigationState state)
    {
        if (!string.IsNullOrWhiteSpace(state.CachedBitmapPath) && File.Exists(state.CachedBitmapPath))
            return state.CachedBitmapPath;

        if (!string.IsNullOrWhiteSpace(state.ImagePath) && File.Exists(state.ImagePath))
            return state.ImagePath;

        return null;
    }

    private static string GetNavigationKey(DecodingImageItem item)
    {
        return !string.IsNullOrWhiteSpace(item.CachedBitmapPath)
            ? item.CachedBitmapPath
            : item.ImagePath;
    }

    private static string GetNavigationKey(DecodingImageNavigationState state)
    {
        return !string.IsNullOrWhiteSpace(state.CachedBitmapPath)
            ? state.CachedBitmapPath
            : state.ImagePath;
    }

    private static HistoryItem? CloneHistoryItem(HistoryItem? source)
    {
        if (source is null)
            return null;

        return new HistoryItem
        {
            CodesContent = source.CodesContent,
            ContentKind = source.ContentKind,
            MultiLineCodeModeOverride = source.MultiLineCodeModeOverride,
            Foreground = source.Foreground,
            Background = source.Background,
            ErrorCorrection = source.ErrorCorrection,
            LogoImagePath = source.LogoImagePath,
            LogoEmoji = source.LogoEmoji,
            LogoEmojiStyle = source.LogoEmojiStyle,
            LogoSizePercentage = source.LogoSizePercentage,
            LogoPaddingPixels = source.LogoPaddingPixels,
        };
    }

    private static async Task<StorageFolder?> GetStorageFolderIfExistsAsync(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return null;

        return await StorageFolder.GetFolderFromPathAsync(folderPath);
    }

    [RelayCommand]
    private void CancelBatchOperation()
    {
        _batchCts?.Cancel();
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
            currentCachedImagePath = cachePath;
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
                await OpenAndDecodeStorageFiles(clipboardItems);
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
    private async Task OpenFolder()
    {
        FolderPicker folderPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        Window window = new();
        IntPtr windowHandle = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(folderPicker, windowHandle);

        StorageFolder? folder = await folderPicker.PickSingleFolderAsync();

        if (folder is null)
            return;

        IReadOnlyList<StorageFile> allFiles = await folder.GetFilesAsync();
        List<StorageFile> imageFiles = allFiles
            .Where(f => imageExtensions.Contains(Path.GetExtension(f.Path).ToLowerInvariant()))
            .ToList();

        if (imageFiles.Count > LargeFolderThreshold)
        {
            ContentDialog confirmDialog = new()
            {
                Title = "Large folder",
                Content = $"This folder contains {imageFiles.Count} image files. Batch operations like \"Decode All\" or \"Summary View\" may take a while. Continue?",
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.MainWindow.Content.XamlRoot,
            };
            ContentDialogResult result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;
        }

        currentFolderPaneFolder = folder;
        FolderPaneFolderName = folder.Name;
        FolderFiles.Clear();

        foreach (StorageFile file in imageFiles)
        {
            FolderFileItem item = new(file);
            FolderFiles.Add(item);
            _ = item.LoadThumbnailAsync();
        }

        IsFolderPaneOpen = true;
        OnPropertyChanged(nameof(HasFolderFiles));
        OnPropertyChanged(nameof(HasLeftPaneContent));
    }

    [RelayCommand]
    private void CloseFolderPane()
    {
        IsFolderPaneOpen = false;
    }

    [RelayCommand]
    private async Task OpenFolderFileItem(FolderFileItem item)
    {
        currentFolderFileItem = item;
        nextSourceKind = DecodingSourceKind.File;
        await OpenAndDecodeStorageFile(item.File);
    }

    [RelayCommand]
    private async Task DecodeAllAndExport()
    {
        if (currentFolderPaneFolder is null || FolderFiles.Count == 0)
            return;

        using CancellationTokenSource cts = new();
        _batchCts = cts;
        IsCancellableOperationRunning = true;
        IsLoading = true;

        try
        {
            List<FolderFileItem> snapshot = FolderFiles.ToList();
            int total = snapshot.Count;
            int current = 0;

            foreach (FolderFileItem fileItem in snapshot)
            {
                if (cts.Token.IsCancellationRequested)
                    break;

                current++;
                LoadingMessage = $"Processing {current} of {total}: {fileItem.FileName}";

                string outputText;
                try
                {
                    IEnumerable<(string, Result)> results =
                        await Task.Run(() => BarcodeHelpers.GetStringsFromImageFile(fileItem.File), cts.Token);

                    List<string> lines = results.Select(r => r.Item1).ToList();
                    outputText = lines.Count > 0
                        ? string.Join(Environment.NewLine, lines)
                        : "<no qr codes found>";
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to decode {fileItem.FileName}: {ex.Message}");
                    outputText = "<no qr codes found>";
                }

                try
                {
                    string txtFileName = Path.GetFileNameWithoutExtension(fileItem.FileName) + ".txt";
                    StorageFile txtFile = await currentFolderPaneFolder.CreateFileAsync(
                        txtFileName,
                        CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(txtFile, outputText);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to write txt for {fileItem.FileName}: {ex.Message}");
                }
            }
        }
        finally
        {
            _batchCts = null;
            IsCancellableOperationRunning = false;
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ShowFolderSummary()
    {
        if (FolderFiles.Count == 0)
            return;

        using CancellationTokenSource cts = new();
        _batchCts = cts;
        IsCancellableOperationRunning = true;
        IsLoading = true;

        List<FolderSummaryItem> summaryItems = [];

        try
        {
            List<FolderFileItem> snapshot = FolderFiles.ToList();
            int total = snapshot.Count;
            int current = 0;

            foreach (FolderFileItem fileItem in snapshot)
            {
                if (cts.Token.IsCancellationRequested)
                    break;

                current++;
                LoadingMessage = $"Processing {current} of {total}: {fileItem.FileName}";

                List<string> codes;
                try
                {
                    IEnumerable<(string, Result)> results =
                        await Task.Run(() => BarcodeHelpers.GetStringsFromImageFile(fileItem.File), cts.Token);
                    codes = results.Select(r => r.Item1).ToList();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to decode {fileItem.FileName}: {ex.Message}");
                    codes = [];
                }

                summaryItems.Add(new FolderSummaryItem
                {
                    FileName = fileItem.FileName,
                    FilePath = fileItem.File.Path,
                    QrCodeCount = codes.Count,
                    QrCodeContents = string.Join(", ", codes),
                });
            }
        }
        finally
        {
            _batchCts = null;
            IsCancellableOperationRunning = false;
            IsLoading = false;
            LoadingMessage = string.Empty;
        }

        if (summaryItems.Count > 0)
        {
            NavigationService.NavigateTo(typeof(FolderSummaryViewModel).FullName!, new FolderSummaryNavigationParameter
            {
                FolderName = FolderPaneFolderName,
                Items = summaryItems,
                BackNavigationState = new NavigationRestoreState
                {
                    PageKey = typeof(DecodingViewModel).FullName!,
                    Parameter = CreateNavigationState(),
                },
            });
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

        DecodingImageItems.Clear();
        DecodingImageItems.Add(decodingImage);
        OnPropertyChanged(nameof(IsMultiImagePanelVisible));
        CurrentDecodingItem = decodingImage;
    }

    public async Task OpenAndDecodeStorageFiles(IReadOnlyList<IStorageItem> pickedFiles)
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
        await Task.Yield();

        try
        {
            DecodingImageItem? decodedItem = await GetDecodingImageItemFromStorageFileAsync(storageFile);
            if (decodedItem is not null)
            {
                DecodingImageItems.Clear();
                DecodingImageItems.Add(decodedItem);
                OnPropertyChanged(nameof(IsMultiImagePanelVisible));
            OnPropertyChanged(nameof(HasLeftPaneContent));
                CurrentDecodingItem = decodedItem;
            }
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

        (MagickImage MagickImage, string CachePath, List<(string, Result)> Strings) decodeResult =
            await Task.Run(async () =>
            {
                MagickImage magickImage = await ImageProcessingHelper.LoadImageFromStorageFile(storageFile);

                // Convert the oriented MagickImage to a Bitmap for ZXing decoding
                // so that ResultPoints are in the same coordinate space as the displayed image.
                using Bitmap orientedBitmap = ImageProcessingHelper.ConvertToBitmap(magickImage);

                string cachePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{DateTimeOffset.Now.Ticks}.png");
                orientedBitmap.Save(cachePath);

                List<(string, Result)> strings = BarcodeHelpers.GetStringsFromBitmap(orientedBitmap).ToList();
                return (magickImage, cachePath, strings);
            });

        Uri uri = new($"{decodeResult.CachePath}?tick={DateTimeOffset.Now.Ticks}");
        BitmapImage thisPickedImage = new(uri)
        {
            CreateOptions = BitmapCreateOptions.IgnoreImageCache
        };

        ObservableCollection<TextBorder> codeBorders = [];
        foreach ((string, Result) item in decodeResult.Strings)
        {
            TextBorder textBorder = new(item.Item2);
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            codeBorders.Add(textBorder);
        }

        DecodingImageItem decodingImage = new()
        {
            ImagePath = storageFile.Path,
            CachedBitmapPath = decodeResult.CachePath,
            BitmapImage = thisPickedImage,
            ImagePixelWidth = (int)decodeResult.MagickImage.Width,
            ImagePixelHeight = (int)decodeResult.MagickImage.Height,
            CodeBorders = codeBorders,
            OriginalMagickImage = decodeResult.MagickImage,
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
