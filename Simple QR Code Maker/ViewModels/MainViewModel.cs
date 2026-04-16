using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{
    private const int LargeBatchThreshold = 24;
    private const int PreviewBatchSize = LargeBatchThreshold;
    private const int MaxBulkSaveConcurrency = 4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    private string urlText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    private ObservableCollection<BarcodeImageItem> qrCodeBitmaps = [];

    private readonly List<RequestedQrCodeItem> requestedQrCodes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRequestedCodes))]
    [NotifyPropertyChangedFor(nameof(IsLargeBatchPreview))]
    [NotifyPropertyChangedFor(nameof(IsBulkClipboardEnabled))]
    [NotifyPropertyChangedFor(nameof(IsBulkClipboardDisabled))]
    [NotifyPropertyChangedFor(nameof(BulkClipboardRestrictionText))]
    [NotifyPropertyChangedFor(nameof(CanLoadMorePreviews))]
    [NotifyPropertyChangedFor(nameof(PreviewSummaryText))]
    [NotifyCanExecuteChangedFor(nameof(LoadMorePreviewsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SavePngCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSvgCommand))]
    [NotifyCanExecuteChangedFor(nameof(SavePngAsZipCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSvgAsZipCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveBothCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveBothAsZipCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyPngToClipboardCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySvgToClipboardCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySvgTextToClipboardCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyBothToClipboardCommand))]
    private int requestedCodeCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMorePreviews))]
    [NotifyPropertyChangedFor(nameof(PreviewSummaryText))]
    [NotifyCanExecuteChangedFor(nameof(LoadMorePreviewsCommand))]
    private int loadedPreviewCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkOperationStatusText))]
    private int bulkOperationCompletedItemCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkOperationStatusText))]
    private int bulkOperationTotalItemCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBulkClipboardEnabled))]
    [NotifyPropertyChangedFor(nameof(BulkOperationStatusText))]
    [NotifyCanExecuteChangedFor(nameof(SavePngCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSvgCommand))]
    [NotifyCanExecuteChangedFor(nameof(SavePngAsZipCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSvgAsZipCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveBothCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveBothAsZipCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyPngToClipboardCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySvgToClipboardCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySvgTextToClipboardCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyBothToClipboardCommand))]
    private bool isBulkOperationRunning;

    [ObservableProperty]
    private string placeholderText = "ex: http://www.example.com";

    private readonly string[] placeholdersList =
    [
        "http://example.com",
        "https://www.wikipedia.org",
        "https://www.JoeFinApps.com",
        "https://github.com",
        "https://github.com/TheJoeFin/Simple-QR-Code-Maker",
        "https://github.com/TheJoeFin/Text-Grab",
        "https://github.com/TheJoeFin/Simple-Icon-File-Maker"
    ];

    [ObservableProperty]
    private Windows.UI.Color backgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);

    [ObservableProperty]
    private Windows.UI.Color foregroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);

    [ObservableProperty]
    private ErrorCorrectionOptions selectedOption = new("M", "Medium 15%", ErrorCorrectionLevel.M);

    [ObservableProperty]
    private double qrPaddingModules = 2.0;

    [ObservableProperty]
    private bool isFaqPaneOpen = false;

    [ObservableProperty]
    private bool isHistoryPaneOpen = false;

    [ObservableProperty]
    private ObservableCollection<HistoryItem> historyItems = [];

    [ObservableProperty]
    private HistoryItem? selectedHistoryItem = null;

    [ObservableProperty]
    private ObservableCollection<BrandItem> brandItems = [];

    [ObservableProperty]
    private BrandItem? selectedBrand = null;

    public ObservableCollection<ColorPickerListItem> ForegroundBrandColorItems { get; } = [];

    public ObservableCollection<ColorPickerListItem> BackgroundBrandColorItems { get; } = [];

    public ObservableCollection<ColorPickerListItem> ForegroundRecentColorItems { get; } = [];

    public ObservableCollection<ColorPickerListItem> BackgroundRecentColorItems { get; } = [];

    private bool _isApplyingBrand = false;

    [ObservableProperty]
    private string newBrandName = string.Empty;

    [ObservableProperty]
    private bool includeForeground = true;

    [ObservableProperty]
    private bool includeBackground = true;

    [ObservableProperty]
    private bool includeUrl;

    [ObservableProperty]
    private bool includeCenterImage = true;

    [ObservableProperty]
    private bool includeErrorCorrection = true;

    [ObservableProperty]
    private bool isNewBrandFormVisible;

    private MultiLineCodeMode MultiLineCodeMode = MultiLineCodeMode.OneLineOneCode;
    private string BaseText = string.Empty;
    private bool WarnWhenNotUrl = true;
    private bool HideMinimumSizeText = false;
    private string QuickSaveLocation = string.Empty;

    [ObservableProperty]
    private bool showSaveBothButton = false;

    [ObservableProperty]
    private bool canPasteText = false;

    [ObservableProperty]
    private bool showCodeInfoBar = false;

    [ObservableProperty]
    private InfoBarSeverity codeInfoBarSeverity = InfoBarSeverity.Success;

    [ObservableProperty]
    private string codeInfoBarTitle = "QR Code copied to clipboard";

    [ObservableProperty]
    private string codeInfoBarMessage = string.Empty;

    [ObservableProperty]
    private string savedFolderPath = string.Empty;

    [ObservableProperty]
    private bool copySharePopupOpen = false;

    [ObservableProperty]
    private System.Drawing.Bitmap? logoImage = null;

    [ObservableProperty]
    private string? logoSvgContent = null;

    [ObservableProperty]
    private bool hasLogo = false;

    [ObservableProperty]
    private BitmapImage? logoPreviewImage = null;

    [ObservableProperty]
    private bool isBackgroundRemovalAvailable = false;

    [ObservableProperty]
    private bool isSpreadsheetImportAvailable = false;

    [ObservableProperty]
    private double logoPaddingPixels = 4.0;

    [ObservableProperty]
    private double logoSizePercentage = 15;

    [ObservableProperty]
    private double logoSizeMaxPercentage = 20;

    private string? currentLogoPath = null;

    private double MinSizeScanDistanceScaleFactor = 1;

    private readonly DispatcherTimer copyInfoBarTimer = new();

    partial void OnSelectedHistoryItemChanged(HistoryItem? value)
    {
        if (value is null)
            return;

        IsHistoryPaneOpen = false;
        UrlText = value.CodesContent;
        ForegroundColor = value.Foreground;
        BackgroundColor = value.Background;
        SelectedOption = ErrorCorrectionLevels.First(x => x.ErrorCorrectionLevel == value.ErrorCorrection);

        // Restore logo image and size if available
        if (!string.IsNullOrEmpty(value.LogoImagePath))
        {
            _ = LoadLogoFromHistory(value.LogoImagePath);
        }
        else
        {
            // Clear logo if history item has no logo
            LogoImage?.Dispose();
            LogoImage = null;
        }

        LogoSizePercentage = value.LogoSizePercentage;
        LogoPaddingPixels = value.LogoPaddingPixels;

        SelectedHistoryItem = null;
    }

    private async Task LoadLogoFromHistory(string logoPath)
    {
        try
        {
            if (!File.Exists(logoPath))
            {
                return;
            }
            // Dispose old logo first
            LogoImage?.Dispose();

            if (logoPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                string svgContent = await File.ReadAllTextAsync(logoPath);
                LogoSvgContent = svgContent;
                LogoImage = BarcodeHelpers.RasterizeSvgToBitmap(svgContent, 512, 512);
                currentLogoPath = logoPath;
                return;
            }

            // Raster logo: clear any stale SVG content
            LogoSvgContent = null;
            StorageFile file = await StorageFile.GetFileFromPathAsync(logoPath);
            using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
            // GDI+ keeps an internal reference to the original stream; create an independent
            // copy before the stream is disposed so that later Save() calls don't fail.
            using System.Drawing.Bitmap tmp = new(stream.AsStreamForRead());
            LogoImage = new System.Drawing.Bitmap(tmp);
            currentLogoPath = logoPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load logo from history: {ex.Message}");
            LogoImage?.Dispose();
            LogoImage = null;
            LogoSvgContent = null;
            currentLogoPath = null;
        }
    }

    public ObservableCollection<ErrorCorrectionOptions> ErrorCorrectionLevels { get; set; } = new(allCorrectionLevels);

    private static readonly List<ErrorCorrectionOptions> allCorrectionLevels =
    [
        new("L", "Low 7%", ErrorCorrectionLevel.L),
        new("M", "Medium 15%", ErrorCorrectionLevel.M),
        new("Q", "Quarter 25%", ErrorCorrectionLevel.Q),
        new("H", "High 30%", ErrorCorrectionLevel.H),
    ];

    partial void OnSelectedOptionChanged(ErrorCorrectionOptions value)
    {
        if (!_isApplyingBrand)
            SelectedBrand = null;

        // Ensure logo size doesn't exceed the new error correction level's maximum
        if (logoSizePercentage > logoSizeMaxPercentage)
        {
            logoSizePercentage = logoSizeMaxPercentage;
        }
        OnPropertyChanged(nameof(logoSizeMaxPercentage));

        LogoSizeMaxPercentage = BarcodeHelpers.GetMaxLogoSizePercentage(value.ErrorCorrectionLevel);

        if (!_isApplyingBrand)
        {
            debounceTimer.Stop();
            debounceTimer.Start();
        }
    }

    partial void OnBackgroundColorChanged(Windows.UI.Color value)
    {
        if (!_isApplyingBrand)
        {
            SelectedBrand = null;
            debounceTimer.Stop();
            debounceTimer.Start();
        }
    }

    partial void OnForegroundColorChanged(Windows.UI.Color value)
    {
        if (!_isApplyingBrand)
        {
            SelectedBrand = null;
            debounceTimer.Stop();
            debounceTimer.Start();
        }
    }

    partial void OnLogoImageChanged(System.Drawing.Bitmap? value)
    {
        if (!_isApplyingBrand)
            SelectedBrand = null;
        HasLogo = value != null;

        if (HasLogo)
        {
            if (SelectedOption.ErrorCorrectionLevel == ErrorCorrectionLevel.L && ErrorCorrectionLevels.Count > 1)
            {
                SelectedOption = ErrorCorrectionLevels[1];
            }

            if (ErrorCorrectionLevels.Count > 0
                && ErrorCorrectionLevels[0].ErrorCorrectionLevel == ErrorCorrectionLevel.L)
                ErrorCorrectionLevels.RemoveAt(0);
        }
        else
        {
            if (ErrorCorrectionLevels.Count == 3)
                ErrorCorrectionLevels.Insert(0, new("L", "Low 7%", ErrorCorrectionLevel.L));
        }

        _ = UpdateLogoPreviewImageAsync(value);

        if (!_isApplyingBrand)
        {
            debounceTimer.Stop();
            debounceTimer.Start();
        }
    }

    private async Task UpdateLogoPreviewImageAsync(System.Drawing.Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            LogoPreviewImage = null;
            return;
        }

        try
        {
            using MemoryStream ms = new();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            BitmapImage bitmapImage = new();
            using InMemoryRandomAccessStream randomAccessStream = new();
            await randomAccessStream.WriteAsync(ms.ToArray().AsBuffer());
            randomAccessStream.Seek(0);
            await bitmapImage.SetSourceAsync(randomAccessStream);

            LogoPreviewImage = bitmapImage;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create logo preview: {ex.Message}");
            LogoPreviewImage = null;
        }
    }

    partial void OnLogoSvgContentChanged(string? value)
    {
        OnPropertyChanged(nameof(IsSvgLogo));
        OnPropertyChanged(nameof(CanRemoveLogoBackground));
    }

    partial void OnIsBackgroundRemovalAvailableChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRemoveLogoBackground));
    }

    partial void OnIsSpreadsheetImportAvailableChanged(bool value)
    {
        OpenSpreadsheetFileCommand.NotifyCanExecuteChanged();
    }

    public bool IsSvgLogo => LogoSvgContent != null;

    public bool CanRemoveLogoBackground => IsBackgroundRemovalAvailable && !IsSvgLogo;

    partial void OnLogoSizePercentageChanged(double value)
    {
        if (!_isApplyingBrand)
        {
            SelectedBrand = null;
            debounceTimer.Stop();
            debounceTimer.Start();
        }
    }

    partial void OnLogoPaddingPixelsChanged(double value)
    {
        if (!_isApplyingBrand)
        {
            SelectedBrand = null;
            debounceTimer.Stop();
            debounceTimer.Start();
        }
    }

    public bool CanSaveImage => !string.IsNullOrWhiteSpace(UrlText);

    public bool HasRequestedCodes => RequestedCodeCount > 0;

    public bool CanLoadMorePreviews => LoadedPreviewCount < RequestedCodeCount;

    public bool IsLargeBatchPreview => RequestedCodeCount > LargeBatchThreshold;

    public bool IsBulkClipboardEnabled => HasRequestedCodes && !IsLargeBatchPreview && !IsBulkOperationRunning;

    public bool IsBulkClipboardDisabled => HasRequestedCodes && IsLargeBatchPreview;

    public string BulkClipboardRestrictionText => IsBulkClipboardDisabled
        ? $"Bulk clipboard is disabled when more than {LargeBatchThreshold} QR Codes are requested. Save to files or ZIP instead."
        : string.Empty;

    public string PreviewSummaryText
    {
        get
        {
            if (RequestedCodeCount == 0)
                return string.Empty;

            if (LoadedPreviewCount >= RequestedCodeCount)
            {
                return RequestedCodeCount == 1
                    ? "Showing 1 preview."
                    : $"Showing all {RequestedCodeCount} previews.";
            }

            return $"Showing {LoadedPreviewCount} of {RequestedCodeCount} previews.";
        }
    }

    public string BulkOperationStatusText
    {
        get
        {
            if (!IsBulkOperationRunning || BulkOperationTotalItemCount == 0)
                return string.Empty;

            return $"Saving {BulkOperationCompletedItemCount} of {BulkOperationTotalItemCount} QR Codes...";
        }
    }

    partial void OnQrPaddingModulesChanged(double value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private bool UrlMatchesBrand(string url)
    {
        if (SelectedBrand is null)
            return false;
        if (SelectedBrand.UrlContent is null)
            return true;
        return url.StartsWith(SelectedBrand.UrlContent, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnUrlTextChanged(string value)
    {
        if (!_isApplyingBrand && !UrlMatchesBrand(value))
            SelectedBrand = null;

        // Update max logo size when text changes (affects QR version/density)
        OnPropertyChanged(nameof(logoSizeMaxPercentage));

        // Ensure current logo size doesn't exceed the new maximum
        if (logoSizePercentage > logoSizeMaxPercentage)
        {
            logoSizePercentage = logoSizeMaxPercentage;
        }

        if (!_isApplyingBrand)
        {
            debounceTimer.Stop();
            debounceTimer.Start();
        }
    }

    private readonly DispatcherTimer debounceTimer = new();

    private readonly DispatcherTimer placeholderTextTimer = new();

    public INavigationService NavigationService { get; }

    public ILocalSettingsService LocalSettingsService { get; }

    public MainViewModel(INavigationService navigationService, ILocalSettingsService localSettingsService)
    {
        debounceTimer.Interval = TimeSpan.FromMilliseconds(600);
        debounceTimer.Tick -= DebounceTimer_Tick;
        debounceTimer.Tick += DebounceTimer_Tick;

        copyInfoBarTimer.Interval = TimeSpan.FromSeconds(10);
        copyInfoBarTimer.Tick -= CopyInfoBarTimer_Tick;
        copyInfoBarTimer.Tick += CopyInfoBarTimer_Tick;

        placeholderTextTimer.Interval = TimeSpan.FromSeconds(6);
        placeholderTextTimer.Tick -= PlaceholderTextTimer_Tick;
        placeholderTextTimer.Tick += PlaceholderTextTimer_Tick;
        placeholderTextTimer.Start();

        NavigationService = navigationService;
        LocalSettingsService = localSettingsService;

        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        Clipboard.ContentChanged += Clipboard_ContentChanged;

        HistoryItems.CollectionChanged += HistoryItems_CollectionChanged;
        BrandItems.CollectionChanged += BrandItems_CollectionChanged;
        RefreshColorPickerListItems();

        WeakReferenceMessenger.Default.Register<RequestShowMessage>(this, OnRequestShowMessage);
        WeakReferenceMessenger.Default.Register<SaveHistoryMessage>(this, OnSaveHistoryMessage);
        WeakReferenceMessenger.Default.Register<RequestPaneChange>(this, OnRequestPaneChange);
    }

    private void BrandItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshBrandColorPickerListItems();
    }

    private void HistoryItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshRecentColorPickerListItems();
    }

    private void RefreshColorPickerListItems()
    {
        RefreshBrandColorPickerListItems();
        RefreshRecentColorPickerListItems();
    }

    private void RefreshBrandColorPickerListItems()
    {
        IReadOnlyList<ColorPickerListItem> brandColorItems = [.. GetBrandColorPickerListItems()];
        ReplaceColorPickerListItems(ForegroundBrandColorItems, brandColorItems);
        ReplaceColorPickerListItems(BackgroundBrandColorItems, brandColorItems);
    }

    private void RefreshRecentColorPickerListItems()
    {
        IReadOnlyList<ColorPickerListItem> recentColorItems = [.. GetHistoryColorPickerListItems()];
        ReplaceColorPickerListItems(ForegroundRecentColorItems, recentColorItems);
        ReplaceColorPickerListItems(BackgroundRecentColorItems, recentColorItems);
    }

    private IEnumerable<ColorPickerListItem> GetBrandColorPickerListItems()
    {
        foreach (BrandItem brand in BrandItems)
        {
            if (brand.Foreground is Windows.UI.Color foreground)
                yield return new ColorPickerListItem(foreground, BuildColorPickerListLabel("Foreground", brand.Name));

            if (brand.Background is Windows.UI.Color background)
                yield return new ColorPickerListItem(background, BuildColorPickerListLabel("Background", brand.Name));
        }
    }

    private IEnumerable<ColorPickerListItem> GetHistoryColorPickerListItems()
    {
        foreach (HistoryItem historyItem in HistoryItems)
        {
            yield return new ColorPickerListItem(
                historyItem.Foreground,
                BuildColorPickerListLabel("Foreground", historyItem.CodesContent));

            yield return new ColorPickerListItem(
                historyItem.Background,
                BuildColorPickerListLabel("Background", historyItem.CodesContent));
        }
    }

    private static void ReplaceColorPickerListItems(
        ObservableCollection<ColorPickerListItem> target,
        IEnumerable<ColorPickerListItem> source)
    {
        target.Clear();

        foreach (ColorPickerListItem item in source)
        {
            target.Add(item);
        }
    }

    private static string BuildColorPickerListLabel(string prefix, string source)
    {
        return $"{prefix}, {NormalizeColorPickerListSource(source)}";
    }

    private static string NormalizeColorPickerListSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "(empty)";

        string normalized = source
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();

        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.Length == 0 ? "(empty)" : normalized;
    }

    private void OnRequestPaneChange(object recipient, RequestPaneChange message)
    {
        switch (message.Pane)
        {
            case MainViewPanes.History:
                switch (message.RequestState)
                {
                    case PaneState.Open:
                        IsHistoryPaneOpen = true;
                        break;
                    case PaneState.Close:
                        IsFaqPaneOpen = false;
                        break;
                    default:
                        break;
                }
                break;
            case MainViewPanes.Faq:
                switch (message.RequestState)
                {
                    case PaneState.Open:
                        IsFaqPaneOpen = true;
                        break;
                    case PaneState.Close:
                        IsFaqPaneOpen = false;
                        break;
                    default:
                        break;
                }
                break;
            default:
                break;
        }
    }

    private async void OnSaveHistoryMessage(object recipient, SaveHistoryMessage message) => await SaveCurrentStateToHistory();

    private void OnRequestShowMessage(object recipient, RequestShowMessage rsm)
    {
        CodeInfoBarMessage = rsm.Message;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = rsm.Severity;
        CodeInfoBarTitle = rsm.Title;

        if (rsm.Severity == InfoBarSeverity.Success)
        {
            copyInfoBarTimer.Start();
        }
    }

    private void Clipboard_ContentChanged(object? sender, object e) => CheckCanPasteText();

    private void CheckCanPasteText()
    {
        try
        {
            DataPackageView clipboardData = Clipboard.GetContent();

            if (clipboardData.Contains(StandardDataFormats.Text))
                CanPasteText = true;
            else
                CanPasteText = false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to check clipboard: {ex.Message}");
            CanPasteText = false;
        }
    }

    private async Task CheckBackgroundRemovalAvailability()
    {
        try
        {
            IsBackgroundRemovalAvailable = await BackgroundRemovalHelper.CheckIsAvailableAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Background removal check failed: {ex.Message}");
            IsBackgroundRemovalAvailable = false;
        }
    }

    private async Task CheckSpreadsheetImportAvailability()
    {
        try
        {
            IsSpreadsheetImportAvailable = await ExcelSpreadsheetHelper.CheckIsAvailableAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Spreadsheet import availability check failed: {ex.Message}");
            IsSpreadsheetImportAvailable = false;
        }
    }

    private void PlaceholderTextTimer_Tick(object? sender, object e)
    {
        Random random = new();
        PlaceholderText = $"ex: {placeholdersList[random.Next(placeholdersList.Length)]}";
    }

    private void DebounceTimer_Tick(object? sender, object e)
    {
        debounceTimer.Stop();

        ResetRequestedCodeState();

        if (string.IsNullOrWhiteSpace(UrlText))
            return;

        bool skippedAnyInvalidCodes = false;
        foreach (string text in EnumerateRequestedCodeTexts())
        {
            try
            {
                _ = Encoder.encode(text, SelectedOption.ErrorCorrectionLevel);
                requestedQrCodes.Add(new RequestedQrCodeItem(text));
            }
            catch (ZXing.WriterException)
            {
                skippedAnyInvalidCodes = true;
            }
        }

        RequestedCodeCount = requestedQrCodes.Count;

        if (RequestedCodeCount > 0)
        {
            MaterializePreviewBatch(Math.Min(PreviewBatchSize, RequestedCodeCount));
            ShowCodeInfoBar = false;
            CodeInfoBarSeverity = InfoBarSeverity.Informational;
        }

        if (skippedAnyInvalidCodes)
        {
            ShowCodeInfoBar = true;
            CodeInfoBarSeverity = InfoBarSeverity.Error;
            CodeInfoBarTitle = "Error creating QR Code";
            CodeInfoBarMessage = RequestedCodeCount == 0
                ? "The text you entered is too long for a QR Code. Please try a shorter text."
                : "Some text entries were too long for a QR Code and were skipped.";
        }
    }

    private void ResetRequestedCodeState()
    {
        QrCodeBitmaps.Clear();
        requestedQrCodes.Clear();
        RequestedCodeCount = 0;
        LoadedPreviewCount = 0;
        SavedFolderPath = string.Empty;
    }

    private IEnumerable<string> EnumerateRequestedCodeTexts()
    {
        if (MultiLineCodeMode != MultiLineCodeMode.OneLineOneCode)
        {
            string singleCode = UrlText.Trim();
            if (!string.IsNullOrWhiteSpace(singleCode))
                yield return singleCode;

            yield break;
        }

        foreach (string line in UrlText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            string textToUse = line.Trim();
            if (!string.IsNullOrWhiteSpace(textToUse))
                yield return textToUse;
        }
    }

    private void MaterializePreviewBatch(int additionalCount)
    {
        if (additionalCount <= 0)
            return;

        int targetCount = Math.Min(RequestedCodeCount, LoadedPreviewCount + additionalCount);
        for (int index = LoadedPreviewCount; index < targetCount; index++)
        {
            BarcodeImageItem previewItem = CreatePreviewItem(requestedQrCodes[index]);
            QrCodeBitmaps.Add(previewItem);
        }

        LoadedPreviewCount = targetCount;
    }

    private BarcodeImageItem CreatePreviewItem(RequestedQrCodeItem requestedCode)
    {
        WriteableBitmap bitmap = BarcodeHelpers.GetQrCodeBitmapFromText(
            requestedCode.CodeAsText,
            SelectedOption.ErrorCorrectionLevel,
            ForegroundColor.ToSystemDrawingColor(),
            BackgroundColor.ToSystemDrawingColor(),
            LogoImage,
            LogoSizePercentage,
            LogoPaddingPixels,
            QrPaddingModules);
        BarcodeImageItem barcodeImageItem = new()
        {
            CodeAsBitmap = bitmap,
            CodeAsText = requestedCode.CodeAsText,
            IsAppShowingUrlWarnings = WarnWhenNotUrl,
            SizeTextVisible = (HideMinimumSizeText
                || LogoImage != null
                || ForegroundColor.A < 255
                || BackgroundColor.A < 255
                || !BarcodeHelpers.IsSizeRecommendationAvailableForPadding(QrPaddingModules))
                ? Visibility.Collapsed
                : Visibility.Visible,
            ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
            ForegroundColor = ForegroundColor,
            BackgroundColor = BackgroundColor,
            MaxSizeScaleFactor = MinSizeScanDistanceScaleFactor,
            QrPaddingModules = QrPaddingModules,
            LogoImage = LogoImage,
            LogoSizePercentage = LogoSizePercentage,
            LogoPaddingPixels = LogoPaddingPixels,
            LogoSvgContent = LogoSvgContent,
        };

        double ratio = barcodeImageItem.ColorContrastRatio;
        Debug.WriteLine($"Contrast ratio: {ratio}");

        return barcodeImageItem;
    }

    private bool CanLoadMorePreviewBatch() => CanLoadMorePreviews;

    [RelayCommand(CanExecute = nameof(CanLoadMorePreviewBatch))]
    private void LoadMorePreviews()
    {
        MaterializePreviewBatch(PreviewBatchSize);
    }

    [RelayCommand]
    private async Task PasteTextIntoUrlText()
    {
        DataPackageView clipboardContent = Clipboard.GetContent();
        if (clipboardContent.Contains(StandardDataFormats.Text))
        {
            string text = await clipboardContent.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.Trim();
                UrlText = text;
            }
        }
    }

    [RelayCommand]
    private async Task OpenTextFile()
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add("*");

        InitializeWithWindow.Initialize(picker, App.MainWindow.GetWindowHandle());

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        try
        {
            string text = await FileIO.ReadTextAsync(file);
            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.TrimEnd();
                UrlText = string.IsNullOrWhiteSpace(UrlText)
                    ? text
                    : UrlText + "\r" + text;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read text file: {ex.Message}");
            CodeInfoBarMessage = "Could not read the selected file";
            CodeInfoBarTitle = "Error reading file";
            CodeInfoBarSeverity = InfoBarSeverity.Error;
            ShowCodeInfoBar = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenSpreadsheetFile))]
    private async Task OpenSpreadsheetFile()
    {
        if (!IsSpreadsheetImportAvailable)
        {
            CodeInfoBarMessage = "Install Microsoft Excel to import spreadsheet files right now.";
            CodeInfoBarTitle = "Spreadsheet import unavailable";
            CodeInfoBarSeverity = InfoBarSeverity.Warning;
            ShowCodeInfoBar = true;
            return;
        }

        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".tsv");
        picker.FileTypeFilter.Add(".xlsx");
        picker.FileTypeFilter.Add(".xls");

        InitializeWithWindow.Initialize(picker, App.MainWindow.GetWindowHandle());

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        List<List<string>> rows;
        try
        {
            rows = await ExcelSpreadsheetHelper.ReadRowsAsync(file.Path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read spreadsheet file: {ex.Message}");
            CodeInfoBarMessage = "Could not read the selected file";
            CodeInfoBarTitle = "Error reading spreadsheet";
            CodeInfoBarSeverity = InfoBarSeverity.Error;
            ShowCodeInfoBar = true;
            return;
        }

        if (rows.Count == 0)
        {
            CodeInfoBarMessage = "The selected spreadsheet did not contain any data.";
            CodeInfoBarTitle = "Nothing to import";
            CodeInfoBarSeverity = InfoBarSeverity.Warning;
            ShowCodeInfoBar = true;
            return;
        }

        NavigationService.NavigateTo(
            typeof(SpreadsheetImportViewModel).FullName!,
            new SpreadsheetImportNavigationData
            {
                ReturnState = CreateCurrentStateHistoryItem(),
                Rows = rows.Select(row => (IReadOnlyList<string>)[.. row]).ToList(),
                SourceFileName = file.Name,
            });
    }

    private bool CanOpenSpreadsheetFile()
    {
        return IsSpreadsheetImportAvailable;
    }

    private RequestedQrCodeItem[] GetRequestedCodeSnapshot() => [.. requestedQrCodes];

    private bool CanRunBulkSaveOperation() => RequestedCodeCount > 0 && !IsBulkOperationRunning;

    private bool CanRunBulkClipboardOperation() => RequestedCodeCount > 0 && !IsBulkOperationRunning && !IsLargeBatchPreview;

    private RenderSettingsSnapshot CreateRenderSettingsSnapshot()
    {
        return new RenderSettingsSnapshot(
            SelectedOption.ErrorCorrectionLevel,
            ForegroundColor.ToSystemDrawingColor(),
            BackgroundColor.ToSystemDrawingColor(),
            LogoImage is null ? null : new System.Drawing.Bitmap(LogoImage),
            LogoSizePercentage,
            LogoPaddingPixels,
            LogoSvgContent,
            QrPaddingModules);
    }

    private void ShowClipboardDisabledInfoBar()
    {
        CodeInfoBarTitle = "Bulk clipboard disabled";
        CodeInfoBarMessage = BulkClipboardRestrictionText;
        CodeInfoBarSeverity = InfoBarSeverity.Informational;
        ShowCodeInfoBar = true;
    }

    private void ShowClipboardCopyFailureInfoBar(string message)
    {
        CodeInfoBarMessage = message;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Error;
        CodeInfoBarTitle = "Failed to copy QR Codes to the clipboard";
    }

    private void ShowBulkOperationFailureInfoBar(string title, string message)
    {
        CodeInfoBarMessage = message;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Error;
        CodeInfoBarTitle = title;
    }

    private void ShowClipboardCopySuccessInfoBar(string title)
    {
        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        CodeInfoBarTitle = title;
        copyInfoBarTimer.Start();
    }

    private static async Task<byte[]> RenderRequestedCodePngBytesAsync(RequestedQrCodeItem requestedCode, RenderSettingsSnapshot renderSettings)
    {
        return await Task.Run(() =>
        {
            using MemoryStream ms = new();
            BarcodeHelpers.SaveQrCodePngToStream(
                ms,
                requestedCode.CodeAsText,
                renderSettings.ErrorCorrectionLevel,
                renderSettings.ForegroundColor,
                renderSettings.BackgroundColor,
                renderSettings.LogoImage,
                renderSettings.LogoSizePercentage,
                renderSettings.LogoPaddingPixels,
                renderSettings.QrPaddingModules);
            return ms.ToArray();
        });
    }

    private static async Task<string> GetRequestedCodeAsSvgTextAsync(RequestedQrCodeItem requestedCode, RenderSettingsSnapshot renderSettings)
    {
        return await Task.Run(() =>
            BarcodeHelpers.GetSvgQrCodeForText(
                requestedCode.CodeAsText,
                renderSettings.ErrorCorrectionLevel,
                renderSettings.ForegroundColor,
                renderSettings.BackgroundColor,
                renderSettings.LogoImage,
                renderSettings.LogoSizePercentage,
                renderSettings.LogoPaddingPixels,
                renderSettings.LogoSvgContent,
                renderSettings.QrPaddingModules).Content);
    }

    private static async Task WriteRequestedCodeToFileAsync(RequestedQrCodeItem requestedCode, StorageFile file, FileKind kindOfFile, RenderSettingsSnapshot renderSettings)
    {
        switch (kindOfFile)
        {
            case FileKind.None:
                return;
            case FileKind.PNG:
                {
                    byte[] pngBytes = await RenderRequestedCodePngBytesAsync(requestedCode, renderSettings);
                    using IRandomAccessStream outputStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                    outputStream.Size = 0;
                    using Stream output = outputStream.AsStreamForWrite();
                    await output.WriteAsync(pngBytes);
                    await output.FlushAsync();
                    break;
                }
            case FileKind.SVG:
                {
                    string svgText = await GetRequestedCodeAsSvgTextAsync(requestedCode, renderSettings);
                    using IRandomAccessStream outputStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                    outputStream.Size = 0;
                    using Stream output = outputStream.AsStreamForWrite();
                    using StreamWriter writer = new(output, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true);
                    await writer.WriteAsync(svgText);
                    await writer.FlushAsync();
                    break;
                }
            default:
                return;
        }
    }

    private static async Task WriteRequestedCodeToZipEntryAsync(RequestedQrCodeItem requestedCode, Stream entryStream, FileKind kindOfFile, RenderSettingsSnapshot renderSettings)
    {
        switch (kindOfFile)
        {
            case FileKind.None:
                return;
            case FileKind.PNG:
                {
                    byte[] pngBytes = await RenderRequestedCodePngBytesAsync(requestedCode, renderSettings);
                    await entryStream.WriteAsync(pngBytes);
                    await entryStream.FlushAsync();
                    break;
                }
            case FileKind.SVG:
                {
                    string svgText = await GetRequestedCodeAsSvgTextAsync(requestedCode, renderSettings);
                    using StreamWriter writer = new(entryStream, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true);
                    await writer.WriteAsync(svgText);
                    await writer.FlushAsync();
                    break;
                }
            default:
                return;
        }
    }

    private static async Task<List<StorageFile>> CreateRequestedCodeFilesAsync(StorageFolder folder, RequestedQrCodeItem[] requestedCodes, RenderSettingsSnapshot renderSettings, params FileKind[] fileKinds)
    {
        List<StorageFile> files = [];

        foreach (RequestedQrCodeItem requestedCode in requestedCodes)
        {
            if (string.IsNullOrWhiteSpace(requestedCode.SafeFileNameBase))
                continue;

            foreach (FileKind kindOfFile in fileKinds)
            {
                StorageFile file = await folder.CreateFileAsync(
                    requestedCode.SafeFileNameBase + GetFileExtension(kindOfFile),
                    CreationCollisionOption.ReplaceExisting);
                await WriteRequestedCodeToFileAsync(requestedCode, file, kindOfFile, renderSettings);
                files.Add(file);
            }
        }

        return files;
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkClipboardOperation))]
    private async Task CopyPngToClipboard()
    {
        if (IsBulkClipboardDisabled)
        {
            ShowClipboardDisabledInfoBar();
            return;
        }

        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        try
        {
            StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
            using RenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            List<StorageFile> files = await CreateRequestedCodeFilesAsync(folder, requestedCodes, renderSettings, FileKind.PNG);

            if (files.Count == 0)
            {
                ShowClipboardCopyFailureInfoBar("No QR Codes to copy to the clipboard");
                return;
            }

            DataPackage dataPackage = new();
            dataPackage.SetStorageItems(files);
            Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

            ShowClipboardCopySuccessInfoBar(
                requestedCodes.Length == 1
                    ? "PNG QR Code copied to the clipboard"
                    : $"{requestedCodes.Length} PNG QR Codes copied to the clipboard");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy PNG QR Codes to the clipboard: {ex.Message}");
            ShowClipboardCopyFailureInfoBar("Could not prepare the PNG QR Codes for the clipboard");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkClipboardOperation))]
    private async Task CopySvgToClipboard()
    {
        if (IsBulkClipboardDisabled)
        {
            ShowClipboardDisabledInfoBar();
            return;
        }

        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        try
        {
            StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
            using RenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            List<StorageFile> files = await CreateRequestedCodeFilesAsync(folder, requestedCodes, renderSettings, FileKind.SVG);

            if (files.Count == 0)
            {
                ShowClipboardCopyFailureInfoBar("No QR Codes to copy to the clipboard");
                return;
            }

            DataPackage dataPackage = new();
            dataPackage.SetStorageItems(files);
            Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

            ShowClipboardCopySuccessInfoBar(
                requestedCodes.Length == 1
                    ? "SVG QR Code copied to the clipboard"
                    : $"{requestedCodes.Length} SVG QR Codes copied to the clipboard");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy SVG QR Codes to the clipboard: {ex.Message}");
            ShowClipboardCopyFailureInfoBar("Could not prepare the SVG QR Codes for the clipboard");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkClipboardOperation))]
    private async Task CopySvgTextToClipboard()
    {
        if (IsBulkClipboardDisabled)
        {
            ShowClipboardDisabledInfoBar();
            return;
        }

        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        try
        {
            using RenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            List<string> textStrings = [];
            foreach (RequestedQrCodeItem requestedCode in requestedCodes)
            {
                string svgText = await GetRequestedCodeAsSvgTextAsync(requestedCode, renderSettings);
                if (!string.IsNullOrWhiteSpace(svgText))
                    textStrings.Add(svgText);
            }

            if (textStrings.Count == 0)
            {
                ShowClipboardCopyFailureInfoBar("No QR Codes to copy to the clipboard");
                return;
            }

            DataPackage dataPackage = new();
            dataPackage.SetText(string.Join(Environment.NewLine, textStrings));
            Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

            ShowClipboardCopySuccessInfoBar(
                textStrings.Count == 1
                    ? "SVG QR Code copied to the clipboard"
                    : $"{textStrings.Count} Text of SVGs QR Codes copied to the clipboard");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy SVG text QR Codes to the clipboard: {ex.Message}");
            ShowClipboardCopyFailureInfoBar("Could not prepare the SVG text for the clipboard");
        }
    }

    private void CopyInfoBarTimer_Tick(object? sender, object e)
    {
        copyInfoBarTimer.Stop();
        CodeInfoBarMessage = string.Empty;
        SavedFolderPath = string.Empty;
        ShowCodeInfoBar = false;
        CodeInfoBarSeverity = InfoBarSeverity.Informational;
        CodeInfoBarTitle = "Copy infoBar title";
    }

    [RelayCommand]
    private async Task OpenSavedFolder()
    {
        if (string.IsNullOrWhiteSpace(SavedFolderPath))
            return;

        await Windows.System.Launcher.LaunchFolderPathAsync(SavedFolderPath);
    }

    [RelayCommand]
    private void ToggleFaqPaneOpen() => IsFaqPaneOpen = !IsFaqPaneOpen;

    [RelayCommand]
    private void ToggleHistoryPaneOpen() => IsHistoryPaneOpen = !IsHistoryPaneOpen;

    [RelayCommand]
    private void ShareApp() => CopySharePopupOpen = !CopySharePopupOpen;

    [RelayCommand]
    private void SelectErrorCorrectionLevel(ErrorCorrectionOptions option) => SelectedOption = option;

    [RelayCommand]
    [RequiresUnreferencedCode("Calls BrandStorageHelper.SaveBrandsAsync")]
    private async Task CreateNewBrand()
    {
        if (string.IsNullOrWhiteSpace(NewBrandName))
            return;

        BrandItem brand = new()
        {
            Name = NewBrandName.Trim(),
            Foreground = IncludeForeground ? ForegroundColor : null,
            Background = IncludeBackground ? BackgroundColor : null,
            UrlContent = IncludeUrl ? UrlText : null,
            ErrorCorrectionLevelAsString = IncludeErrorCorrection ? SelectedOption.ErrorCorrectionLevel.ToString() : null,
            LogoImagePath = IncludeCenterImage && LogoImage != null ? GetCurrentLogoPath() : null,
            LogoSizePercentage = IncludeCenterImage ? LogoSizePercentage : null,
            LogoPaddingPixels = IncludeCenterImage ? LogoPaddingPixels : null,
        };

        BrandItems.Remove(brand);
        BrandItems.Insert(0, brand);
        await BrandStorageHelper.SaveBrandsAsync(BrandItems);
        NewBrandName = string.Empty;
        IsNewBrandFormVisible = false;

        WeakReferenceMessenger.Default.Send(
            new RequestShowMessage("Brand saved", $"\"{brand.Name}\" has been saved", InfoBarSeverity.Success));
    }

    [RelayCommand]
    private async Task ApplyBrand(BrandItem? brand)
    {
        if (brand is null)
            return;

        _isApplyingBrand = true;
        try
        {
            if (brand.Foreground.HasValue)
                ForegroundColor = brand.Foreground.Value;

            if (brand.Background.HasValue)
                BackgroundColor = brand.Background.Value;

            if (brand.UrlContent is not null)
                UrlText = brand.UrlContent;

            if (brand.ErrorCorrectionLevelAsString is not null)
            {
                ErrorCorrectionOptions? match = ErrorCorrectionLevels
                    .FirstOrDefault(x => x.ErrorCorrectionLevel.ToString() == brand.ErrorCorrectionLevelAsString);
                if (match is not null)
                    SelectedOption = match.Value;
            }

            if (brand.LogoImagePath is not null)
                await LoadLogoFromHistory(brand.LogoImagePath);

            if (brand.LogoSizePercentage.HasValue)
                LogoSizePercentage = brand.LogoSizePercentage.Value;

            if (brand.LogoPaddingPixels.HasValue)
                LogoPaddingPixels = brand.LogoPaddingPixels.Value;

            SelectedBrand = brand;
        }
        finally
        {
            _isApplyingBrand = false;
            debounceTimer.Stop();
            debounceTimer.Start();
        }
    }

    [RelayCommand]
    private void ApplyBrandForeground(BrandItem? brand)
    {
        if (brand?.Foreground is not null)
            ForegroundColor = brand.Foreground.Value;
    }

    [RelayCommand]
    private void ApplyBrandBackground(BrandItem? brand)
    {
        if (brand?.Background is not null)
            BackgroundColor = brand.Background.Value;
    }

    [RelayCommand]
    [RequiresUnreferencedCode("Calls BrandStorageHelper.SaveBrandsAsync")]
    private async Task DeleteBrand(BrandItem? brand)
    {
        if (brand is null)
            return;

        BrandItems.Remove(brand);

        if (SelectedBrand is not null && SelectedBrand.Equals(brand))
            SelectedBrand = null;

        await BrandStorageHelper.SaveBrandsAsync(BrandItems);
    }

    [RelayCommand]
    [RequiresUnreferencedCode("Calls BrandStorageHelper.SaveBrandsAsync")]
    private async Task SetDefaultBrand(BrandItem? brand)
    {
        if (brand is null)
            return;

        BrandItem? previousDefault = BrandItems.FirstOrDefault(b => b.IsDefault);
        bool isAlreadyDefault = brand.IsDefault;

        foreach (BrandItem item in BrandItems)
            item.IsDefault = !isAlreadyDefault && item.Equals(brand);

        if (previousDefault is not null && !previousDefault.Equals(brand))
            RefreshBrandItemInList(previousDefault);
        RefreshBrandItemInList(brand);

        await BrandStorageHelper.SaveBrandsAsync(BrandItems);
    }

    [RelayCommand]
    [RequiresUnreferencedCode("Calls BrandStorageHelper.SaveBrandsAsync")]
    private async Task EditBrand(BrandItem? brand)
    {
        if (brand is null)
            return;

        Controls.BrandEditDialog dialog = new(brand)
        {
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary || dialog.EditedItem is null)
            return;

        BrandItem edited = dialog.EditedItem;
        int index = BrandItems.IndexOf(brand);
        if (index < 0)
            return;

        BrandItems.RemoveAt(index);
        BrandItems.Insert(index, edited);

        if (SelectedBrand is not null && SelectedBrand.Equals(brand))
            SelectedBrand = edited;

        await BrandStorageHelper.SaveBrandsAsync(BrandItems);

        WeakReferenceMessenger.Default.Send(
            new RequestShowMessage("Brand updated", $"\"{edited.Name}\" has been updated", InfoBarSeverity.Success));
    }

    private void RefreshBrandItemInList(BrandItem brand)
    {
        int index = BrandItems.IndexOf(brand);
        if (index < 0)
            return;
        BrandItems.RemoveAt(index);
        BrandItems.Insert(index, brand);
    }

    [RelayCommand]
    private void OpenFile() => NavigationService.NavigateTo(typeof(DecodingViewModel).FullName!, CreateCurrentStateHistoryItem());

    [RelayCommand]
    private void GoToSettings() =>
        // pass the current state as a HistoryItem to the settings page
        // so when coming back it can be fully restored
        NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!, CreateCurrentStateHistoryItem());

    private HistoryItem CreateCurrentStateHistoryItem()
    {
        return new HistoryItem
        {
            CodesContent = UrlText,
            Foreground = ForegroundColor,
            Background = BackgroundColor,
            ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
            LogoImagePath = LogoImage != null ? GetCurrentLogoPath() : null,
            LogoSizePercentage = LogoSizePercentage,
            LogoPaddingPixels = LogoPaddingPixels,
        };
    }

    private string? GetCurrentLogoPath() => currentLogoPath;

    [RelayCommand(CanExecute = nameof(CanRunBulkSaveOperation))]
    private async Task SavePng()
    {
        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        string? savedFolder = await SaveAllFiles(requestedCodes, null, FileKind.PNG);

        if (savedFolder is null)
            return;

        ShowSaveSuccessInfoBar(
            requestedCodes.Length == 1 ? "PNG QR Code saved!" : $"{requestedCodes.Length} PNG QR Codes saved!",
            savedFolder);
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkSaveOperation))]
    private async Task SaveSvg()
    {
        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        string? savedFolder = await SaveAllFiles(requestedCodes, null, FileKind.SVG);

        if (savedFolder is null)
            return;

        ShowSaveSuccessInfoBar(
            requestedCodes.Length == 1 ? "SVG QR Code saved!" : $"{requestedCodes.Length} SVG QR Codes saved!",
            savedFolder);
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkSaveOperation))]
    private async Task SavePngAsZip()
    {
        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        bool saved = await SaveAllFilesAsZip(requestedCodes, FileKind.PNG);
        if (!saved)
            return;

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (requestedCodes.Length == 1)
            CodeInfoBarTitle = "PNG QR Code saved to zip!";
        else
            CodeInfoBarTitle = $"{requestedCodes.Length} PNG QR Codes saved to zip!";
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkSaveOperation))]
    private async Task SaveSvgAsZip()
    {
        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        bool saved = await SaveAllFilesAsZip(requestedCodes, FileKind.SVG);
        if (!saved)
            return;

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (requestedCodes.Length == 1)
            CodeInfoBarTitle = "SVG QR Code saved to zip!";
        else
            CodeInfoBarTitle = $"{requestedCodes.Length} SVG QR Codes saved to zip!";
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkSaveOperation))]
    private async Task SaveBoth()
    {
        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        string? savedFolder = await SaveAllFiles(requestedCodes, null, FileKind.PNG, FileKind.SVG);
        if (savedFolder is null)
            return;

        ShowSaveSuccessInfoBar(
            requestedCodes.Length == 1 ? "PNG and SVG QR Code saved!" : $"{requestedCodes.Length} PNG and SVG QR Codes saved!",
            savedFolder);
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkSaveOperation))]
    private async Task SaveBothAsZip()
    {
        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        bool saved = await SaveAllFilesAsZip(requestedCodes, FileKind.PNG, FileKind.SVG);
        if (!saved)
            return;

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (requestedCodes.Length == 1)
            CodeInfoBarTitle = "PNG and SVG QR Code saved to zip!";
        else
            CodeInfoBarTitle = $"{requestedCodes.Length} PNG and SVG QR Codes saved to zip!";
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkClipboardOperation))]
    private async Task CopyBothToClipboard()
    {
        if (IsBulkClipboardDisabled)
        {
            ShowClipboardDisabledInfoBar();
            return;
        }

        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        await SaveCurrentStateToHistory();

        try
        {
            StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
            using RenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            List<StorageFile> files = await CreateRequestedCodeFilesAsync(folder, requestedCodes, renderSettings, FileKind.PNG, FileKind.SVG);

            if (files.Count == 0)
            {
                ShowClipboardCopyFailureInfoBar("No QR Codes to copy to the clipboard");
                return;
            }

            DataPackage dataPackage = new();
            dataPackage.SetStorageItems(files);
            Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

            ShowClipboardCopySuccessInfoBar(
                requestedCodes.Length == 1
                    ? "PNG and SVG QR Code copied to the clipboard"
                    : $"{requestedCodes.Length} PNG and SVG QR Codes copied to the clipboard");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy PNG and SVG QR Codes to the clipboard: {ex.Message}");
            ShowClipboardCopyFailureInfoBar("Could not prepare the QR Codes for the clipboard");
        }
    }

    [RelayCommand]
    private void AddNewLine()
    {
        string stringToAdd = "https://";
        if (!string.IsNullOrWhiteSpace(BaseText))
            stringToAdd = BaseText;

        if (string.IsNullOrWhiteSpace(UrlText))
            UrlText = stringToAdd;

        UrlText += $"\r{stringToAdd}";
    }

    [RelayCommand]
    private async Task SelectLogo()
    {
        FileOpenPicker openPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        openPicker.FileTypeFilter.Add(".png");
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".bmp");
        openPicker.FileTypeFilter.Add(".gif");
        openPicker.FileTypeFilter.Add(".svg");

        Window window = new();
        IntPtr windowHandle = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(openPicker, windowHandle);

        StorageFile file = await openPicker.PickSingleFileAsync();

        if (file is null)
            return;

        try
        {
            // Dispose of the old logo if it exists
            LogoImage?.Dispose();

            if (file.FileType.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                // SVG logo: store raw content for lossless SVG export; rasterize for preview and PNG export
                string svgContent = await FileIO.ReadTextAsync(file);
                LogoSvgContent = svgContent;
                LogoImage = BarcodeHelpers.RasterizeSvgToBitmap(svgContent, 512, 512);
            }
            else
            {
                // Raster logo: clear any previous SVG content, load bitmap directly
                LogoSvgContent = null;
                using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
                // GDI+ keeps an internal reference to the original stream; create an independent
                // copy before the stream is disposed so that later Save() calls don't fail.
                using System.Drawing.Bitmap tmp = new(stream.AsStreamForRead());
                LogoImage = new System.Drawing.Bitmap(tmp);
            }

            // Store the selected file path so it can be saved to history
            currentLogoPath = file.Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load logo image: {ex.Message}");
            CodeInfoBarMessage = "Failed to load the selected image";
            ShowCodeInfoBar = true;
            CodeInfoBarSeverity = InfoBarSeverity.Error;
            CodeInfoBarTitle = "Error loading logo";
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        LogoImage?.Dispose();
        LogoImage = null;
        LogoSvgContent = null;
        currentLogoPath = null;
    }

    [RelayCommand]
    private async Task RemoveLogoBackground()
    {
        if (LogoImage is null)
            return;

        Controls.RemoveBackgroundDialog dialog = new(LogoImage)
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && dialog.ResultBitmap is not null)
        {
            LogoImage?.Dispose();
            LogoImage = dialog.ResultBitmap;

            // Persist the background-removed logo to disk so currentLogoPath
            // is valid for SaveHistoryOnShutdown and CreateCurrentStateHistoryItem.
            await SaveLogoImageToDisk();
        }
    }

    /// <summary>
    /// Saves the current logo to the local LogoImages folder and updates <see cref="currentLogoPath"/>.
    /// SVG logos are saved as .svg; raster logos are saved as .png.
    /// </summary>
    private async Task<string?> SaveLogoImageToDisk()
    {
        if (LogoImage is null)
            return null;

        try
        {
            StorageFolder logoFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("LogoImages", CreationCollisionOption.OpenIfExists);

            if (LogoSvgContent != null)
            {
                string svgFileName = $"logo_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.svg";
                StorageFile svgFile = await logoFolder.CreateFileAsync(svgFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(svgFile, LogoSvgContent);
                currentLogoPath = svgFile.Path;
                return svgFile.Path;
            }

            string fileName = $"logo_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.png";
            StorageFile logoFile = await logoFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            using IRandomAccessStream stream = await logoFile.OpenAsync(FileAccessMode.ReadWrite);
            using IOutputStream outputStream = stream.GetOutputStreamAt(0);
            using DataWriter dataWriter = new(outputStream);
            using MemoryStream ms = new();
            LogoImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] bytes = ms.ToArray();
            dataWriter.WriteBytes(bytes);
            await dataWriter.StoreAsync();

            currentLogoPath = logoFile.Path;
            return logoFile.Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save logo image: {ex.Message}");
            return null;
        }
    }

    private static string GetFileExtension(FileKind kindOfFile)
    {
        return $".{kindOfFile.ToString().ToLowerInvariant()}";
    }

    private async Task<StorageFolder?> PickSaveFolderAsync(string? overrideFolderPath)
    {
        if (overrideFolderPath is not null)
            return await StorageFolder.GetFolderFromPathAsync(overrideFolderPath);

        if (!string.IsNullOrWhiteSpace(QuickSaveLocation) && Directory.Exists(QuickSaveLocation))
            return await StorageFolder.GetFolderFromPathAsync(QuickSaveLocation);

        FolderPicker folderPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(folderPicker, windowHandleSave);

        return await folderPicker.PickSingleFolderAsync();
    }

    private static async Task<StorageFile?> PickSaveZipFileAsync()
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = $"QR Codes {DateTime.Now:yyyy-MM-dd}",
        };
        savePicker.FileTypeChoices.Add("ZIP Archive", [".zip"]);

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

        return await savePicker.PickSaveFileAsync();
    }

    private void BeginBulkSaveOperation(int totalItemCount)
    {
        BulkOperationTotalItemCount = totalItemCount;
        BulkOperationCompletedItemCount = 0;
        IsBulkOperationRunning = true;
        SavedFolderPath = string.Empty;
    }

    private void EndBulkSaveOperation()
    {
        IsBulkOperationRunning = false;
        BulkOperationCompletedItemCount = 0;
        BulkOperationTotalItemCount = 0;
    }

    private void UpdateBulkSaveProgress(int completedItemCount)
    {
        if (App.MainWindow.DispatcherQueue.HasThreadAccess)
        {
            BulkOperationCompletedItemCount = completedItemCount;
            return;
        }

        App.MainWindow.DispatcherQueue.TryEnqueue(() => BulkOperationCompletedItemCount = completedItemCount);
    }

    public async Task<string?> SaveAllFiles(RequestedQrCodeItem[] requestedCodes, string? overrideFolderPath = null, params FileKind[] fileKinds)
    {
        StorageFolder? folder = await PickSaveFolderAsync(overrideFolderPath);
        if (folder is null)
            return null;

        using RenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
        int completedItemCount = 0;
        int maxConcurrency = renderSettings.LogoImage is null ? MaxBulkSaveConcurrency : 1;
        using SemaphoreSlim semaphore = new(maxConcurrency, maxConcurrency);
        List<Task> tasks = [];

        BeginBulkSaveOperation(requestedCodes.Length);

        try
        {
            foreach (RequestedQrCodeItem requestedCode in requestedCodes)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(requestedCode.SafeFileNameBase))
                        {
                            foreach (FileKind kindOfFile in fileKinds)
                            {
                                StorageFile file = await folder.CreateFileAsync(
                                    requestedCode.SafeFileNameBase + GetFileExtension(kindOfFile),
                                    CreationCollisionOption.ReplaceExisting);
                                await WriteRequestedCodeToFileAsync(requestedCode, file, kindOfFile, renderSettings);
                            }
                        }
                    }
                    finally
                    {
                        int completed = Interlocked.Increment(ref completedItemCount);
                        UpdateBulkSaveProgress(completed);
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return folder.Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save QR Codes to files: {ex.Message}");
            ShowBulkOperationFailureInfoBar("Failed to save QR Codes", "Could not save the requested QR Codes to files.");
            return null;
        }
        finally
        {
            EndBulkSaveOperation();
        }
    }

    private void ShowSaveSuccessInfoBar(string title, string folderPath)
    {
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        CodeInfoBarTitle = title;
        CodeInfoBarMessage = $"Saved to {folderPath}";
        SavedFolderPath = folderPath;
        ShowCodeInfoBar = true;
        copyInfoBarTimer.Start();
    }

    public async Task<bool> SaveAllFilesAsZip(RequestedQrCodeItem[] requestedCodes, params FileKind[] fileKinds)
    {
        StorageFile? zipFile = await PickSaveZipFileAsync();
        if (zipFile is null)
            return false;

        using RenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
        BeginBulkSaveOperation(requestedCodes.Length);

        try
        {
            using IRandomAccessStream outputStream = await zipFile.OpenAsync(FileAccessMode.ReadWrite);
            outputStream.Size = 0;
            using Stream output = outputStream.AsStreamForWrite();
            using ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true);

            for (int index = 0; index < requestedCodes.Length; index++)
            {
                RequestedQrCodeItem requestedCode = requestedCodes[index];

                if (!string.IsNullOrWhiteSpace(requestedCode.SafeFileNameBase))
                {
                    foreach (FileKind kindOfFile in fileKinds)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(
                            requestedCode.SafeFileNameBase + GetFileExtension(kindOfFile),
                            CompressionLevel.Optimal);
                        using Stream entryStream = entry.Open();
                        await WriteRequestedCodeToZipEntryAsync(requestedCode, entryStream, kindOfFile, renderSettings);
                    }
                }

                BulkOperationCompletedItemCount = index + 1;
            }

            await output.FlushAsync();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save QR Codes to zip: {ex.Message}");
            ShowBulkOperationFailureInfoBar("Failed to save QR Codes", "Could not save the requested QR Codes to a ZIP archive.");
            return false;
        }
        finally
        {
            EndBulkSaveOperation();
        }
    }

    private sealed partial class RenderSettingsSnapshot(
        ErrorCorrectionLevel errorCorrectionLevel,
        System.Drawing.Color foregroundColor,
        System.Drawing.Color backgroundColor,
        System.Drawing.Bitmap? logoImage,
        double logoSizePercentage,
        double logoPaddingPixels,
        string? logoSvgContent,
        double qrPaddingModules) : IDisposable
    {
        public ErrorCorrectionLevel ErrorCorrectionLevel { get; } = errorCorrectionLevel;

        public System.Drawing.Color ForegroundColor { get; } = foregroundColor;

        public System.Drawing.Color BackgroundColor { get; } = backgroundColor;

        public System.Drawing.Bitmap? LogoImage { get; } = logoImage;

        public double LogoSizePercentage { get; } = logoSizePercentage;

        public double LogoPaddingPixels { get; } = logoPaddingPixels;

        public string? LogoSvgContent { get; } = logoSvgContent;

        public double QrPaddingModules { get; } = qrPaddingModules;

        public void Dispose()
        {
            LogoImage?.Dispose();
        }
    }

    public async void OnNavigatedTo(object parameter)
    {
        await LoadHistory();
        await LoadBrands();

        // Force property change notification to refresh UI bindings after history loads
        OnPropertyChanged(nameof(HistoryItems));
        OnPropertyChanged(nameof(BrandItems));

        CheckCanPasteText();
        _ = CheckBackgroundRemovalAvailability();
        _ = CheckSpreadsheetImportAvailability();
        MultiLineCodeMode = await LocalSettingsService.ReadSettingAsync<MultiLineCodeMode>(nameof(MultiLineCodeMode));
        BaseText = await LocalSettingsService.ReadSettingAsync<string>(nameof(BaseText)) ?? string.Empty;
        UrlText = BaseText;
        WarnWhenNotUrl = await LocalSettingsService.ReadSettingAsync<bool>(nameof(WarnWhenNotUrl));
        HideMinimumSizeText = await LocalSettingsService.ReadSettingAsync<bool>(nameof(HideMinimumSizeText));
        ShowSaveBothButton = await LocalSettingsService.ReadSettingAsync<bool>(nameof(ShowSaveBothButton));
        QuickSaveLocation = await LocalSettingsService.ReadSettingAsync<string>(nameof(QuickSaveLocation)) ?? string.Empty;
        MinSizeScanDistanceScaleFactor = await LocalSettingsService.ReadSettingAsync<double>(nameof(MinSizeScanDistanceScaleFactor));
        QrPaddingModules = BarcodeHelpers.NormalizeQrPaddingModules(
            await LocalSettingsService.ReadSettingAsync<double?>(nameof(QrPaddingModules)) ?? 2.0);
        if (MinSizeScanDistanceScaleFactor < 0.35)
        {
            MinSizeScanDistanceScaleFactor = 1;
            // reset to 1 if the value is too small, this can happen when settings are reset
            await LocalSettingsService.SaveSettingAsync(nameof(MinSizeScanDistanceScaleFactor), MinSizeScanDistanceScaleFactor);
        }

        if (parameter is TitleBarSearchResult searchResult)
        {
            await ApplyTitleBarSearchNavigationAsync(searchResult);
        }
        // Check if parameter is a HistoryItem with full state restoration
        else if (parameter is HistoryItem historyItem)
        {
            RestoreFromHistoryItem(historyItem);
        }
        // Otherwise check for text rehydration from other pages
        else if (parameter is string textParam && !string.IsNullOrWhiteSpace(textParam))
        {
            UrlText = textParam;
        }
        else
        {
            // Apply default brand last, after all settings are loaded
            BrandItem? defaultBrand = BrandItems.FirstOrDefault(b => b.IsDefault);
            if (defaultBrand is not null)
                _ = ApplyBrand(defaultBrand);
        }
    }

    private async Task ApplyTitleBarSearchNavigationAsync(TitleBarSearchResult searchResult)
    {
        switch (searchResult.Kind)
        {
            case TitleBarSearchResultKind.Brand when searchResult.BrandItem is not null:
                BrandItem brandToApply = BrandItems
                    .FirstOrDefault(item => item.Equals(searchResult.BrandItem))
                    ?? searchResult.BrandItem;
                await ApplyBrand(brandToApply);
                break;

            case TitleBarSearchResultKind.History when searchResult.HistoryItem is not null:
                RestoreFromHistoryItem(searchResult.HistoryItem);
                break;

            case TitleBarSearchResultKind.Faq:
                IsFaqPaneOpen = true;
                WeakReferenceMessenger.Default.Send(new RequestPaneChange(
                    MainViewPanes.Faq,
                    PaneState.Open,
                    searchResult.SearchText));
                break;
        }
    }

    private void RestoreFromHistoryItem(HistoryItem historyItem)
    {
        UrlText = historyItem.CodesContent;
        ForegroundColor = historyItem.Foreground;
        BackgroundColor = historyItem.Background;
        SelectedOption = ErrorCorrectionLevels.First(x => x.ErrorCorrectionLevel == historyItem.ErrorCorrection);

        // Restore logo image and settings if available
        if (!string.IsNullOrEmpty(historyItem.LogoImagePath))
        {
            _ = LoadLogoFromHistory(historyItem.LogoImagePath);
        }
        else
        {
            // Clear logo if history item has no logo
            LogoImage?.Dispose();
            LogoImage = null;
            currentLogoPath = null;
        }

        LogoSizePercentage = historyItem.LogoSizePercentage;
        LogoPaddingPixels = historyItem.LogoPaddingPixels;
    }

    public void OnNavigatedFrom()
    {
        try
        {
            placeholderTextTimer.Stop();
            placeholderTextTimer.Tick -= PlaceholderTextTimer_Tick;

            debounceTimer.Stop();
            debounceTimer.Tick -= DebounceTimer_Tick;

            copyInfoBarTimer.Stop();
            copyInfoBarTimer.Tick -= CopyInfoBarTimer_Tick;

            Clipboard.ContentChanged -= Clipboard_ContentChanged;

            WeakReferenceMessenger.Default.UnregisterAll(this);

            if (!string.IsNullOrWhiteSpace(UrlText))
                SaveHistoryOnShutdown();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ OnNavigatedFrom error: {ex}");
        }
        finally
        {
            LogoImage?.Dispose();
        }
    }

    private void SaveHistoryOnShutdown()
    {
        try
        {
            HistoryItem historyItem = new()
            {
                CodesContent = UrlText,
                Foreground = ForegroundColor,
                Background = BackgroundColor,
                ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
                LogoImagePath = currentLogoPath,
                LogoSizePercentage = LogoSizePercentage,
                LogoPaddingPixels = LogoPaddingPixels,
            };

            // Build an unbound snapshot list to avoid modifying the
            // ObservableCollection that is bound to the XAML ListView.
            // Modifying a bound collection during Window.Closed causes
            // a native WinRT stowed exception (0xc000027b).
            ObservableCollection<HistoryItem> snapshot = new(
                HistoryItems.Where(h => !h.Equals(historyItem)));
            snapshot.Insert(0, historyItem);

            _ = HistoryStorageHelper.SaveHistoryAsync(snapshot);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ SaveHistoryOnShutdown error: {ex}");
        }
    }

    [RelayCommand]
    public async Task SaveCurrentStateToHistory()
    {
        // Save logo image to local app storage if present
        string? logoImagePath = await SaveLogoImageToDisk();

        HistoryItem historyItem = new()
        {
            CodesContent = UrlText,
            Foreground = ForegroundColor,
            Background = BackgroundColor,
            ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
            LogoImagePath = logoImagePath ?? currentLogoPath, // Use saved path or current path
            LogoSizePercentage = LogoSizePercentage,
            LogoPaddingPixels = LogoPaddingPixels,
        };

        HistoryItems.Remove(historyItem);
        HistoryItems.Insert(0, historyItem);

        await SaveHistoryToFile();
    }

    [RequiresUnreferencedCode("Calls HistoryStorageHelper.SaveHistoryAsync")]
    private async Task SaveHistoryToFile()
    {
        try
        {
            await HistoryStorageHelper.SaveHistoryAsync(HistoryItems);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to save history: {ex.Message}");
        }
    }

    [RequiresUnreferencedCode("Calls HistoryStorageHelper.LoadHistoryAsync")]
    private async Task LoadHistory()
    {
        ObservableCollection<HistoryItem> loadedHistory = await HistoryStorageHelper.LoadHistoryAsync();

        foreach (HistoryItem item in loadedHistory)
        {
            HistoryItems.Add(item);
        }
    }

    [RequiresUnreferencedCode("Calls BrandStorageHelper.LoadBrandsAsync")]
    private async Task LoadBrands()
    {
        ObservableCollection<BrandItem> loadedBrands = await BrandStorageHelper.LoadBrandsAsync();

        foreach (BrandItem item in loadedBrands)
        {
            BrandItems.Add(item);
        }
    }
}
