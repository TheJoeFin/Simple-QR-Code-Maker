using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Controls;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    public partial string UrlText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    public partial ObservableCollection<BarcodeImageItem> QrCodeBitmaps { get; set; } = [];

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
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    public partial int RequestedCodeCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMorePreviews))]
    [NotifyPropertyChangedFor(nameof(PreviewSummaryText))]
    [NotifyCanExecuteChangedFor(nameof(LoadMorePreviewsCommand))]
    public partial int LoadedPreviewCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkOperationStatusText))]
    public partial int BulkOperationCompletedItemCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkOperationStatusText))]
    public partial int BulkOperationTotalItemCount { get; set; }

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
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    public partial bool IsBulkOperationRunning { get; set; }

    [ObservableProperty]
    public partial string PlaceholderText { get; set; } = "ex: http://www.example.com";

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
    public partial Windows.UI.Color BackgroundColor { get; set; } = Windows.UI.Color.FromArgb(255, 255, 255, 255);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentEmojiPreviewForegroundBrush))]
    public partial Windows.UI.Color ForegroundColor { get; set; } = Windows.UI.Color.FromArgb(255, 0, 0, 0);

    [ObservableProperty]
    public partial ErrorCorrectionOptions SelectedOption { get; set; } = new("M", "Medium 15%", ErrorCorrectionLevel.M);

    [ObservableProperty]
    public partial double QrPaddingModules { get; set; } = 2.0;

    [ObservableProperty]
    public partial bool IsFaqPaneOpen { get; set; } = false;

    [ObservableProperty]
    public partial bool IsHistoryPaneOpen { get; set; } = false;

    [ObservableProperty]
    public partial ObservableCollection<HistoryItem> HistoryItems { get; set; } = [];

    [ObservableProperty]
    public partial HistoryItem? SelectedHistoryItem { get; set; } = null;

    [ObservableProperty]
    public partial ObservableCollection<BrandItem> BrandItems { get; set; } = [];

    [ObservableProperty]
    public partial BrandItem? SelectedBrand { get; set; } = null;
    public ObservableCollection<ColorPickerListItem> ForegroundBrandColorItems { get; } = [];

    public ObservableCollection<ColorPickerListItem> BackgroundBrandColorItems { get; } = [];

    public ObservableCollection<ColorPickerListItem> ForegroundRecentColorItems { get; } = [];

    public ObservableCollection<ColorPickerListItem> BackgroundRecentColorItems { get; } = [];

    private bool _isApplyingBrand = false;

    [ObservableProperty]
    public partial string NewBrandName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeForeground { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeBackground { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeUrl { get; set; }

    [ObservableProperty]
    public partial bool IncludeCenterImage { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeErrorCorrection { get; set; } = true;

    [ObservableProperty]
    public partial bool IsNewBrandFormVisible { get; set; }

    private MultiLineCodeMode MultiLineCodeMode = MultiLineCodeMode.OneLineOneCode;
    private string BaseText = string.Empty;
    private bool WarnWhenNotUrl = true;
    private bool HideMinimumSizeText = false;
    private string QuickSaveLocation = string.Empty;
    private QrContentKind currentContentKind = QrContentKind.PlainText;
    private MultiLineCodeMode? multiLineCodeModeOverride = null;

    [ObservableProperty]
    public partial bool ShowSaveBothButton { get; set; } = false;

    [ObservableProperty]
    public partial bool CanPasteText { get; set; } = false;

    [ObservableProperty]
    public partial bool CanPasteLogoImage { get; set; } = false;

    [ObservableProperty]
    public partial bool UseSingleCodeForVCardByDefault { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImageLogoPickerMode))]
    [NotifyPropertyChangedFor(nameof(IsEmojiLogoPickerMode))]
    public partial int LogoPickerModeIndex { get; set; } = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentEmojiFontFamily))]
    [NotifyPropertyChangedFor(nameof(IsColorEmojiFontEnabled))]
    [NotifyPropertyChangedFor(nameof(CurrentEmojiPreviewForegroundBrush))]
    public partial int EmojiStyleIndex { get; set; } = 0;

    [ObservableProperty]
    public partial EmojiLogoOption? SelectedEmojiOption { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImageLogoSelection))]
    [NotifyPropertyChangedFor(nameof(HasEmojiLogoSelection))]
    [NotifyPropertyChangedFor(nameof(CanRemoveLogoBackground))]
    public partial bool IsEmojiLogoSelected { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowCodeInfoBar { get; set; } = false;

    [ObservableProperty]
    public partial InfoBarSeverity CodeInfoBarSeverity { get; set; } = InfoBarSeverity.Success;

    [ObservableProperty]
    public partial string CodeInfoBarTitle { get; set; } = "QR Code copied to clipboard";

    [ObservableProperty]
    public partial string CodeInfoBarMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SavedFolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CopySharePopupOpen { get; set; } = false;

    [ObservableProperty]
    public partial System.Drawing.Bitmap? LogoImage { get; set; } = null;

    [ObservableProperty]
    public partial string? LogoSvgContent { get; set; } = null;

    [ObservableProperty]
    public partial bool HasLogo { get; set; } = false;

    [ObservableProperty]
    public partial BitmapImage? LogoPreviewImage { get; set; } = null;

    [ObservableProperty]
    public partial bool IsBackgroundRemovalAvailable { get; set; } = false;

    [ObservableProperty]
    public partial bool IsSpreadsheetImportAvailable { get; set; } = false;

    [ObservableProperty]
    public partial double LogoPaddingPixels { get; set; } = 4.0;

    [ObservableProperty]
    public partial double LogoSizePercentage { get; set; } = 15;

    [ObservableProperty]
    public partial double LogoSizeMaxPercentage { get; set; } = 20;

    [ObservableProperty]
    public partial string? CurrentLogoPath { get; set; } = null;

    private double MinSizeScanDistanceScaleFactor = 1;

    private readonly DispatcherTimer copyInfoBarTimer = new();
    private bool _suppressEmojiSelectionChanges = false;
    private int _emojiPreviewRefreshVersion = 0;

    public ObservableCollection<EmojiLogoOption> EmojiOptions { get; } =
    [
        .. EmojiLogoPresets.All.Select(static preset => new EmojiLogoOption(preset.Emoji, preset.Name)),
    ];

    partial void OnSelectedHistoryItemChanged(HistoryItem? value)
    {
        if (value is null)
            return;

        IsHistoryPaneOpen = false;
        ApplyDesignState(QrCodeDesignStateMapper.FromHistoryItem(value));

        SelectedHistoryItem = null;
    }

    private MultiLineCodeMode GetEffectiveMultiLineCodeMode()
    {
        return multiLineCodeModeOverride ?? MultiLineCodeMode;
    }

    private MultiLineCodeMode? GetDefaultVCardMultiLineOverride()
    {
        return UseSingleCodeForVCardByDefault
            ? MultiLineCodeMode.MultilineOneCode
            : null;
    }

    public bool UseSingleCodeForCurrentDocument
    {
        get
        {
            if (multiLineCodeModeOverride.HasValue)
                return multiLineCodeModeOverride.Value == MultiLineCodeMode.MultilineOneCode;

            return currentContentKind == QrContentKind.VCard && UseSingleCodeForVCardByDefault;
        }
    }

    private void SetCurrentDocumentMetadata(QrContentKind contentKind, MultiLineCodeMode? overrideMode)
    {
        currentContentKind = contentKind;
        multiLineCodeModeOverride = overrideMode;
        OnPropertyChanged(nameof(UseSingleCodeForCurrentDocument));
    }

    private void ApplyDocumentText(
        string text,
        QrContentKind contentKind = QrContentKind.PlainText,
        MultiLineCodeMode? overrideMode = null)
    {
        SetCurrentDocumentMetadata(contentKind, overrideMode);
        UrlText = text;
    }

    private void RefreshCodesFromMultiLineOptionChange()
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    partial void OnUseSingleCodeForVCardByDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(UseSingleCodeForCurrentDocument));
    }

    private async Task LoadLogoFromHistory(string logoPath)
    {
        try
        {
            if (!File.Exists(logoPath))
            {
                RemoveLogo();
                return;
            }
            StorageFile file = await StorageFile.GetFileFromPathAsync(logoPath);
            await LoadLogoFromStorageFileAsync(file);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load logo from history: {ex.Message}");
            LogoImage?.Dispose();
            LogoImage = null;
            LogoSvgContent = null;
            CurrentLogoPath = null;
            IsEmojiLogoSelected = false;
        }
    }

    private EmojiLogoStyle GetSelectedEmojiStyle()
    {
        return EmojiStyleIndex switch
        {
            1 => EmojiLogoStyle.ThreeDimensional,
            _ => EmojiLogoStyle.Monochrome,
        };
    }

    private static int GetEmojiStyleIndex(EmojiLogoStyle? style)
    {
        return style switch
        {
            EmojiLogoStyle.ThreeDimensional => 1,
            _ => 0,
        };
    }

    private EmojiLogoOption GetOrCreateEmojiOption(string emoji)
    {
        EmojiLogoOption? existing = EmojiOptions.FirstOrDefault(option => option.Emoji == emoji);
        if (existing is not null)
            return existing;

        EmojiLogoOption created = new(emoji, emoji);
        EmojiOptions.Insert(0, created);
        return created;
    }

    private async Task ApplyEmojiLogoAsync(EmojiLogoOption option, bool persistToDisk)
    {
        try
        {
            LogoImageResult emojiResult = await logoService.CreateEmojiLogoAsync(option.Emoji, GetSelectedEmojiStyle(), ForegroundColor);
            LogoImage?.Dispose();
            LogoSvgContent = emojiResult.SvgContent;
            LogoImage = emojiResult.LogoImage;
            IsEmojiLogoSelected = true;
            LogoPickerModeIndex = 1;
            CurrentLogoPath = null;

            if (persistToDisk)
                await SaveLogoImageToDisk();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to render emoji logo: {ex.Message}");
            ShowLogoLoadFailure("Failed to render the selected emoji");
        }
    }

    private async Task RestoreEmojiLogoAsync(string emoji, EmojiLogoStyle? style, string? logoPath)
    {
        _suppressEmojiSelectionChanges = true;
        EmojiStyleIndex = GetEmojiStyleIndex(style);
        SelectedEmojiOption = GetOrCreateEmojiOption(emoji);
        _suppressEmojiSelectionChanges = false;

        if (SelectedEmojiOption is null)
            return;

        await ApplyEmojiLogoAsync(SelectedEmojiOption, persistToDisk: false);

        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            CurrentLogoPath = logoPath;
        else
            await SaveLogoImageToDisk();
    }

    private async Task RestoreLogoAsync(string? logoPath, string? logoEmoji, EmojiLogoStyle? logoEmojiStyle, bool clearWhenMissing)
    {
        if (!string.IsNullOrWhiteSpace(logoEmoji))
        {
            await RestoreEmojiLogoAsync(logoEmoji, logoEmojiStyle, logoPath);
            return;
        }

        if (!string.IsNullOrWhiteSpace(logoPath))
        {
            await LoadLogoFromHistory(logoPath);
            return;
        }

        if (clearWhenMissing)
            RemoveLogo();
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
        if (LogoSizePercentage > LogoSizeMaxPercentage)
        {
            LogoSizePercentage = LogoSizeMaxPercentage;
        }
        OnPropertyChanged(nameof(LogoSizeMaxPercentage));

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

        _ = RefreshEmojiOptionPreviewsAsync();

        if (_suppressEmojiSelectionChanges || !IsEmojiLogoSelected || SelectedEmojiOption is null || EmojiStyleIndex != 0)
            return;

        _ = ApplyEmojiLogoAsync(SelectedEmojiOption, persistToDisk: true);
    }

    partial void OnLogoImageChanged(System.Drawing.Bitmap? value)
    {
        if (!_isApplyingBrand)
            SelectedBrand = null;
        HasLogo = value != null;
        OnPropertyChanged(nameof(HasImageLogoSelection));
        OnPropertyChanged(nameof(HasEmojiLogoSelection));
        OnPropertyChanged(nameof(CanRemoveLogoBackground));

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

    partial void OnCurrentLogoPathChanged(string? value)
    {
        if (!_isApplyingBrand)
            debounceTimer.Start();
    }

    private async Task UpdateLogoPreviewImageAsync(System.Drawing.Bitmap? bitmap)
    {
        try
        {
            LogoPreviewImage = await logoService.CreateBitmapImageAsync(bitmap);
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

    public bool IsSvgLogo => LogoSvgContent != null;

    public bool CanRemoveLogoBackground => IsBackgroundRemovalAvailable && !IsSvgLogo && HasImageLogoSelection;

    public bool IsImageLogoPickerMode => LogoPickerModeIndex == 0;

    public bool IsEmojiLogoPickerMode => LogoPickerModeIndex == 1;

    public bool HasImageLogoSelection => HasLogo && !IsEmojiLogoSelected;

    public bool HasEmojiLogoSelection => HasLogo && IsEmojiLogoSelected;

    public FontFamily CurrentEmojiFontFamily => new(EmojiLogoHelper.GetFontFamilyName(GetSelectedEmojiStyle()));

    public bool IsColorEmojiFontEnabled => EmojiLogoHelper.IsColorFontEnabled(GetSelectedEmojiStyle());

    public Brush CurrentEmojiPreviewForegroundBrush => new SolidColorBrush(ForegroundColor);

    public string SelectedEmojiStyleLabel => EmojiStyleIndex switch
    {
        1 => "Selected style: 3D",
        _ => "Selected style: Black/White",
    };

    partial void OnLogoPickerModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsImageLogoPickerMode));
        OnPropertyChanged(nameof(IsEmojiLogoPickerMode));
    }

    partial void OnEmojiStyleIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentEmojiFontFamily));
        OnPropertyChanged(nameof(IsColorEmojiFontEnabled));
        OnPropertyChanged(nameof(CurrentEmojiPreviewForegroundBrush));
        OnPropertyChanged(nameof(SelectedEmojiStyleLabel));
        _ = RefreshEmojiOptionPreviewsAsync();

        if (_suppressEmojiSelectionChanges || !IsEmojiLogoSelected || SelectedEmojiOption is null)
            return;

        _ = ApplyEmojiLogoAsync(SelectedEmojiOption, persistToDisk: true);
    }

    partial void OnSelectedEmojiOptionChanged(EmojiLogoOption? value)
    {
        if (_suppressEmojiSelectionChanges || value is null)
            return;

        _ = ApplyEmojiLogoAsync(value, persistToDisk: true);
    }

    private async Task RefreshEmojiOptionPreviewsAsync()
    {
        int refreshVersion = Interlocked.Increment(ref _emojiPreviewRefreshVersion);
        EmojiLogoStyle style = GetSelectedEmojiStyle();

        foreach (EmojiLogoOption option in EmojiOptions)
        {
            BitmapImage previewImage = await logoService.RenderEmojiPreviewAsync(option.Emoji, style, ForegroundColor, 96);

            if (refreshVersion != _emojiPreviewRefreshVersion)
                return;

            option.PreviewImage = previewImage;
        }
    }

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
        OnPropertyChanged(nameof(LogoSizeMaxPercentage));

        // Ensure current logo size doesn't exceed the new maximum
        if (LogoSizePercentage > LogoSizeMaxPercentage)
        {
            LogoSizePercentage = LogoSizeMaxPercentage;
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

    private readonly IBrandService brandService;
    private readonly IHistoryService historyService;
    private readonly ILogoService logoService;
    private readonly IQrExportService qrExportService;
    private readonly IPrintService printService;

    public MainViewModel(
        INavigationService navigationService,
        ILocalSettingsService localSettingsService,
        IBrandService brandService,
        IHistoryService historyService,
        ILogoService logoService,
        IQrExportService qrExportService,
        IPrintService printService)
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
        this.brandService = brandService;
        this.historyService = historyService;
        this.logoService = logoService;
        this.qrExportService = qrExportService;
        this.printService = printService;

        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        Clipboard.ContentChanged += Clipboard_ContentChanged;

        _ = RefreshClipboardCapabilitiesAsync();

        HistoryItems.CollectionChanged += HistoryItems_CollectionChanged;
        BrandItems.CollectionChanged += BrandItems_CollectionChanged;
        RefreshColorPickerListItems();
        _ = RefreshEmojiOptionPreviewsAsync();

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
        IReadOnlyList<ColorPickerListItem> brandColorItems = ColorPickerListBuilder.FromBrands(BrandItems);
        ColorPickerListBuilder.ReplaceItems(ForegroundBrandColorItems, brandColorItems);
        ColorPickerListBuilder.ReplaceItems(BackgroundBrandColorItems, brandColorItems);
    }

    private void RefreshRecentColorPickerListItems()
    {
        IReadOnlyList<ColorPickerListItem> recentColorItems = ColorPickerListBuilder.FromHistory(HistoryItems);
        ColorPickerListBuilder.ReplaceItems(ForegroundRecentColorItems, recentColorItems);
        ColorPickerListBuilder.ReplaceItems(BackgroundRecentColorItems, recentColorItems);
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

    private async void Clipboard_ContentChanged(object? sender, object e) => await RefreshClipboardCapabilitiesAsync();

    private async Task RefreshClipboardCapabilitiesAsync()
    {
        try
        {
            DataPackageView clipboardData = Clipboard.GetContent();
            CanPasteText = clipboardData.Contains(StandardDataFormats.Text);
            CanPasteLogoImage = clipboardData.Contains(StandardDataFormats.Bitmap)
                || await logoService.ClipboardContainsSupportedLogoFileAsync(clipboardData);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to check clipboard: {ex.Message}");
            CanPasteText = false;
            CanPasteLogoImage = false;
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
        if (GetEffectiveMultiLineCodeMode() != MultiLineCodeMode.OneLineOneCode)
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
                ApplyDocumentText(text);
            }
        }
    }

    [RelayCommand]
    private async Task OpenUrlBuilder()
    {
        UrlBuilderDialog dialog = new(UrlText)
        {
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.ResultText is not null)
            ApplyDocumentText(dialog.ResultText);
    }

    [RelayCommand]
    private async Task OpenVCardBuilder()
    {
        VCardBuilderDialog dialog = new(UrlText)
        {
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.ResultText is not null)
        {
            ApplyDocumentText(
                dialog.ResultText,
                QrContentKind.VCard,
                GetDefaultVCardMultiLineOverride());
        }
    }

    [RelayCommand]
    private async Task OpenWifiBuilder()
    {
        WifiBuilderDialog dialog = new(UrlText)
        {
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.ResultText is not null)
        {
            ApplyDocumentText(
                dialog.ResultText,
                QrContentKind.WiFi,
                MultiLineCodeMode.MultilineOneCode);
        }
    }

    [RelayCommand]
    private async Task SetVCardSingleCodeDefault(bool isEnabled)
    {
        multiLineCodeModeOverride = isEnabled
            ? MultiLineCodeMode.MultilineOneCode
            : null;
        UseSingleCodeForVCardByDefault = isEnabled;
        await LocalSettingsService.SaveSettingAsync(nameof(UseSingleCodeForVCardByDefault), isEnabled);
        OnPropertyChanged(nameof(UseSingleCodeForCurrentDocument));
        RefreshCodesFromMultiLineOptionChange();
    }

    [RelayCommand]
    private async Task PasteLogoFromClipboard()
    {
        try
        {
            DataPackageView clipboardData = Clipboard.GetContent();

            if (clipboardData.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> clipboardItems = await clipboardData.GetStorageItemsAsync();
                StorageFile? imageFile = clipboardItems.OfType<StorageFile>().FirstOrDefault(logoService.IsSupportedLogoFile);

                if (imageFile is not null)
                {
                    await LoadLogoFromStorageFileAsync(imageFile);
                    return;
                }
            }

            if (clipboardData.Contains(StandardDataFormats.Bitmap))
            {
                RandomAccessStreamReference bitmapStreamReference = await clipboardData.GetBitmapAsync();
                using IRandomAccessStreamWithContentType stream = await bitmapStreamReference.OpenReadAsync();
                await LoadRasterLogoFromStreamAsync(stream, null);
                await SaveLogoImageToDisk();
                return;
            }

            ShowLogoLoadFailure("Clipboard does not contain an image");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to paste logo image: {ex.Message}");
            ShowLogoLoadFailure("Failed to load the image from the clipboard");
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
                string updatedText = string.IsNullOrWhiteSpace(UrlText)
                    ? text
                    : UrlText + "\r" + text;
                ApplyDocumentText(updatedText);
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

    [RelayCommand]
    private async Task OpenSpreadsheetFile()
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".tsv");
        if (IsSpreadsheetImportAvailable)
        {
            picker.FileTypeFilter.Add(".xlsx");
            picker.FileTypeFilter.Add(".xls");
        }

        InitializeWithWindow.Initialize(picker, App.MainWindow.GetWindowHandle());

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        List<List<string>> rows;
        try
        {
            rows = await ExcelSpreadsheetHelper.ReadRowsAsync(file.Path);
        }
        catch (InvalidOperationException ex) when (!IsSpreadsheetImportAvailable)
        {
            Debug.WriteLine($"Excel workbook import unavailable: {ex.Message}");
            CodeInfoBarMessage = "CSV and TSV imports are available. Install Microsoft Excel to import Excel workbooks.";
            CodeInfoBarTitle = "Excel workbooks unavailable";
            CodeInfoBarSeverity = InfoBarSeverity.Warning;
            ShowCodeInfoBar = true;
            return;
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

    private RequestedQrCodeItem[] GetRequestedCodeSnapshot() => [.. requestedQrCodes];

    private bool CanRunBulkSaveOperation() => RequestedCodeCount > 0 && !IsBulkOperationRunning;

    private bool CanRunBulkClipboardOperation() => RequestedCodeCount > 0 && !IsBulkOperationRunning && !IsLargeBatchPreview;

    private QrRenderSettingsSnapshot CreateRenderSettingsSnapshot()
    {
        return QrRenderSettingsSnapshot.Create(
            SelectedOption.ErrorCorrectionLevel,
            ForegroundColor.ToSystemDrawingColor(),
            BackgroundColor.ToSystemDrawingColor(),
            LogoImage,
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
            using QrRenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            IReadOnlyList<StorageFile> files = await qrExportService.CreateFilesAsync(folder, requestedCodes, renderSettings, FileKind.PNG);

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
            using QrRenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            IReadOnlyList<StorageFile> files = await qrExportService.CreateFilesAsync(folder, requestedCodes, renderSettings, FileKind.SVG);

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
            using QrRenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            IReadOnlyList<string> textStrings = await qrExportService.RenderSvgTextsAsync(requestedCodes, renderSettings);

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
    [RequiresUnreferencedCode("Calls BrandService persistence methods")]
    private async Task CreateNewBrand()
    {
        if (string.IsNullOrWhiteSpace(NewBrandName))
            return;

        string? logoPath = IncludeCenterImage && LogoImage != null
            ? (GetCurrentLogoPath() ?? await SaveLogoImageToDisk())
            : null;

        BrandItem brand = QrCodeDesignStateMapper.ToBrandItem(
            CreateCurrentDesignState(logoPath),
            NewBrandName,
            new BrandCreationOptions
            {
                IncludeForeground = IncludeForeground,
                IncludeBackground = IncludeBackground,
                IncludeUrl = IncludeUrl,
                IncludeCenterImage = IncludeCenterImage,
                IncludeErrorCorrection = IncludeErrorCorrection,
            });

        await brandService.AddOrReplaceAndSaveAsync(BrandItems, brand);
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
                ApplyDocumentText(brand.UrlContent);

            if (brand.ErrorCorrectionLevelAsString is not null)
            {
                ErrorCorrectionOptions? match = ErrorCorrectionLevels
                    .FirstOrDefault(x => x.ErrorCorrectionLevel.ToString() == brand.ErrorCorrectionLevelAsString);
                if (match is not null)
                    SelectedOption = match.Value;
            }

            if (brand.LogoImagePath is not null || brand.LogoEmoji is not null)
                await RestoreLogoAsync(brand.LogoImagePath, brand.LogoEmoji, brand.LogoEmojiStyle, clearWhenMissing: false);

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
    [RequiresUnreferencedCode("Calls BrandService persistence methods")]
    private async Task DeleteBrand(BrandItem? brand)
    {
        if (brand is null)
            return;

        await brandService.DeleteAndSaveAsync(BrandItems, brand);

        if (SelectedBrand is not null && SelectedBrand.Equals(brand))
            SelectedBrand = null;
    }

    [RelayCommand]
    [RequiresUnreferencedCode("Calls BrandService persistence methods")]
    private async Task SetDefaultBrand(BrandItem? brand)
    {
        if (brand is null)
            return;

        await brandService.SetDefaultAndSaveAsync(BrandItems, brand);
    }

    [RelayCommand]
    [RequiresUnreferencedCode("Calls BrandService persistence methods")]
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
        bool replaced = await brandService.ReplaceAndSaveAsync(BrandItems, brand, edited);
        if (!replaced)
            return;

        if (SelectedBrand is not null && SelectedBrand.Equals(brand))
            SelectedBrand = edited;

        WeakReferenceMessenger.Default.Send(
            new RequestShowMessage("Brand updated", $"\"{edited.Name}\" has been updated", InfoBarSeverity.Success));
    }

    [RelayCommand(CanExecute = nameof(CanRunBulkSaveOperation))]
    private async Task Print()
    {
        RequestedQrCodeItem[] requestedCodes = GetRequestedCodeSnapshot();
        if (requestedCodes.Length == 0)
            return;

        PrintJobSettings initialSettings = new()
        {
            CodesPerPage = await LocalSettingsService.ReadSettingAsync<int?>("PrintCodesPerPage") ?? 4,
            MarginMm = await LocalSettingsService.ReadSettingAsync<double?>("PrintMarginMm") ?? 10,
            ShowLabels = await LocalSettingsService.ReadSettingAsync<bool?>("PrintShowLabels") ?? true,
        };

        using QrRenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();

        Controls.PrintSettingsDialog dialog = new(printService, requestedCodes, renderSettings, initialSettings)
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        PrintJobSettings settings = dialog.ResultSettings;
        await LocalSettingsService.SaveSettingAsync("PrintCodesPerPage", settings.CodesPerPage);
        await LocalSettingsService.SaveSettingAsync("PrintMarginMm", settings.MarginMm);
        await LocalSettingsService.SaveSettingAsync("PrintShowLabels", settings.ShowLabels);
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
        return QrCodeDesignStateMapper.ToHistoryItem(CreateCurrentDesignState(LogoImage != null ? GetCurrentLogoPath() : null));
    }

    private string? GetCurrentLogoPath() => CurrentLogoPath;

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
            using QrRenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            IReadOnlyList<StorageFile> files = await qrExportService.CreateFilesAsync(folder, requestedCodes, renderSettings, FileKind.PNG, FileKind.SVG);

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
        SetCurrentDocumentMetadata(QrContentKind.PlainText, null);

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
        LogoPickerModeIndex = 0;

        FileOpenPicker openPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        foreach (string fileType in logoService.SupportedLogoFileTypes)
        {
            openPicker.FileTypeFilter.Add(fileType);
        }

        Window window = new();
        IntPtr windowHandle = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(openPicker, windowHandle);

        StorageFile file = await openPicker.PickSingleFileAsync();

        if (file is null)
            return;

        try
        {
            await LoadLogoFromStorageFileAsync(file);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load logo image: {ex.Message}");
            ShowLogoLoadFailure("Failed to load the selected image");
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        LogoImage?.Dispose();
        LogoImage = null;
        LogoSvgContent = null;
        CurrentLogoPath = null;
        IsEmojiLogoSelected = false;
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

            // Persist the background-removed logo to disk so CurrentLogoPath
            // is valid for SaveHistoryOnShutdown and CreateCurrentStateHistoryItem.
            await SaveLogoImageToDisk();
        }
    }

    private async Task LoadLogoFromStorageFileAsync(StorageFile file)
    {
        LogoImage?.Dispose();
        IsEmojiLogoSelected = false;
        LogoPickerModeIndex = 0;

        LogoImageResult loadedLogo = await logoService.LoadFromStorageFileAsync(file);
        LogoSvgContent = loadedLogo.SvgContent;
        LogoImage = loadedLogo.LogoImage;
        CurrentLogoPath = loadedLogo.LogoPath;
    }

    private async Task LoadRasterLogoFromStreamAsync(IRandomAccessStreamWithContentType stream, string? logoPath)
    {
        LogoImageResult rasterLogo = await logoService.LoadRasterFromStreamAsync(stream, logoPath);
        LogoSvgContent = rasterLogo.SvgContent;
        IsEmojiLogoSelected = false;
        LogoPickerModeIndex = 0;
        LogoImage = rasterLogo.LogoImage;
        CurrentLogoPath = rasterLogo.LogoPath;
    }

    private void ShowLogoLoadFailure(string message)
    {
        CodeInfoBarMessage = message;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Error;
        CodeInfoBarTitle = "Error loading logo";
    }

    /// <summary>
    /// Saves the current logo to the local LogoImages folder and updates <see cref="CurrentLogoPath"/>.
    /// SVG logos are saved as .svg; raster logos are saved as .png.
    /// </summary>
    private async Task<string?> SaveLogoImageToDisk()
    {
        try
        {
            string? savedLogoPath = await logoService.SaveLogoImageToDiskAsync(LogoImage, LogoSvgContent);
            if (savedLogoPath is not null)
                CurrentLogoPath = savedLogoPath;

            return savedLogoPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save logo image: {ex.Message}");
            return null;
        }
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
        BeginBulkSaveOperation(requestedCodes.Length);

        try
        {
            using QrRenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            return await qrExportService.SaveFilesAsync(
                requestedCodes,
                renderSettings,
                QuickSaveLocation,
                overrideFolderPath,
                UpdateBulkSaveProgress,
                fileKinds);
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
        BeginBulkSaveOperation(requestedCodes.Length);

        try
        {
            using QrRenderSettingsSnapshot renderSettings = CreateRenderSettingsSnapshot();
            return await qrExportService.SaveFilesAsZipAsync(requestedCodes, renderSettings, UpdateBulkSaveProgress, fileKinds);
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

    public async void OnNavigatedTo(object parameter)
    {
        await historyService.LoadAsync(HistoryItems);
        await brandService.LoadAsync(BrandItems);

        // Force property change notification to refresh UI bindings after history loads
        OnPropertyChanged(nameof(HistoryItems));
        OnPropertyChanged(nameof(BrandItems));

        await RefreshClipboardCapabilitiesAsync();
        _ = CheckBackgroundRemovalAvailability();
        _ = CheckSpreadsheetImportAvailability();
        MultiLineCodeMode = await LocalSettingsService.ReadSettingAsync<MultiLineCodeMode>(nameof(MultiLineCodeMode));
        UseSingleCodeForVCardByDefault = await LocalSettingsService.ReadSettingAsync<bool>(nameof(UseSingleCodeForVCardByDefault));
        BaseText = await LocalSettingsService.ReadSettingAsync<string>(nameof(BaseText)) ?? string.Empty;
        ApplyDocumentText(BaseText);
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
            ApplyDocumentText(textParam);
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
        ApplyDesignState(QrCodeDesignStateMapper.FromHistoryItem(historyItem));
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
            HistoryItem historyItem = QrCodeDesignStateMapper.ToHistoryItem(CreateCurrentDesignState(CurrentLogoPath));
            historyService.SaveSnapshotOnShutdown(HistoryItems, historyItem);
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

        HistoryItem historyItem = QrCodeDesignStateMapper.ToHistoryItem(CreateCurrentDesignState(logoImagePath ?? CurrentLogoPath));
        await historyService.AddOrReplaceAndSaveAsync(HistoryItems, historyItem);
    }

    private QrCodeDesignState CreateCurrentDesignState(string? logoImagePath = null)
    {
        return new QrCodeDesignState
        {
            CodesContent = UrlText,
            ContentKind = currentContentKind,
            MultiLineCodeModeOverride = multiLineCodeModeOverride,
            Foreground = ForegroundColor,
            Background = BackgroundColor,
            ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
            LogoImagePath = logoImagePath,
            LogoEmoji = HasEmojiLogoSelection ? SelectedEmojiOption?.Emoji : null,
            LogoEmojiStyle = HasEmojiLogoSelection ? GetSelectedEmojiStyle() : null,
            LogoSizePercentage = LogoSizePercentage,
            LogoPaddingPixels = LogoPaddingPixels,
        };
    }

    private void ApplyDesignState(QrCodeDesignState state)
    {
        SetCurrentDocumentMetadata(state.ContentKind, state.MultiLineCodeModeOverride);
        UrlText = state.CodesContent;
        ForegroundColor = state.Foreground;
        BackgroundColor = state.Background;
        SelectedOption = ErrorCorrectionLevels.First(x => x.ErrorCorrectionLevel == state.ErrorCorrection);
        _ = RestoreLogoAsync(state.LogoImagePath, state.LogoEmoji, state.LogoEmojiStyle, clearWhenMissing: true);
        LogoSizePercentage = state.LogoSizePercentage;
        LogoPaddingPixels = state.LogoPaddingPixels;
    }
}
