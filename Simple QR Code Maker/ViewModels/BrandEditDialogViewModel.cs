using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_QR_Code_Maker.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;
using ZXing.QrCode.Internal;

using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.ViewModels;

public sealed partial class BrandEditDialogViewModel : ObservableRecipient
{
    private BrandItem _original = new();

    public List<ErrorCorrectionOptions> AllCorrectionLevels { get; } =
    [
        new("L", "Correction_Low".GetLocalized(), ErrorCorrectionLevel.L),
        new("M", "Correction_Medium".GetLocalized(), ErrorCorrectionLevel.M),
        new("Q", "Correction_Quarter".GetLocalized(), ErrorCorrectionLevel.Q),
        new("H", "Correction_High".GetLocalized(), ErrorCorrectionLevel.H),
    ];

    public List<QrFramePresetOption> AllFramePresets { get; } = [.. QrFramePresetCatalog.All.Select(option => LocalizedDisplay.FrameOption(option.Preset))];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNameValid))]
    public partial string EditName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeForeground { get; set; }

    [ObservableProperty]
    public partial Windows.UI.Color ForegroundColor { get; set; }

    [ObservableProperty]
    public partial bool IncludeBackground { get; set; }

    [ObservableProperty]
    public partial Windows.UI.Color BackgroundColor { get; set; }

    [ObservableProperty]
    public partial bool IncludeErrorCorrection { get; set; }

    [ObservableProperty]
    public partial int SelectedCorrectionIndex { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFrameTextVisible))]
    [NotifyPropertyChangedFor(nameof(FrameTextHeader))]
    [NotifyPropertyChangedFor(nameof(FrameTextPlaceholder))]
    public partial bool IncludeFrame { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFrameTextVisible))]
    [NotifyPropertyChangedFor(nameof(FrameTextHeader))]
    [NotifyPropertyChangedFor(nameof(FrameTextPlaceholder))]
    public partial int SelectedFramePresetIndex { get; set; }

    [ObservableProperty]
    public partial string EditFrameText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeUrl { get; set; }

    [ObservableProperty]
    public partial string UrlContent { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogoPath))]
    public partial bool IncludeLogo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogoPath))]
    public partial string? LogoPath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogoSizeDescription))]
    public partial double LogoSize { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogoPaddingDescription))]
    public partial double LogoPadding { get; set; }

    public bool IsNameValid => !string.IsNullOrWhiteSpace(EditName);
    public bool HasLogoPath => IncludeLogo && LogoPath is not null;
    public string LogoSizeDescription => "Logo_SizePercent".FormatLocalized(LogoSize);
    public string LogoPaddingDescription => "Logo_PaddingPixels".FormatLocalized(LogoPadding);
    public bool IsFrameTextVisible => IncludeFrame && SelectedFramePresetOption.Preset != QrFramePreset.None;

    public string FrameTextHeader => _original.FrameTextSource == QrFrameTextSource.ContentSummary
        ? "Frame_FallbackLabel".GetLocalized()
        : SelectedFramePresetOption.Preset switch
        {
            QrFramePreset.RoundedFrame => "Frame_BannerText".GetLocalized(),
            QrFramePreset.CornerCallout => "Frame_CalloutText".GetLocalized(),
            QrFramePreset.BottomLabel => "Frame_LabelText".GetLocalized(),
            _ => "Frame_Caption".GetLocalized(),
        };

    public string FrameTextDescription => _original.FrameTextSource == QrFrameTextSource.ContentSummary
        ? "Frame_FallbackHelp".GetLocalized()
        : string.Empty;

    public string FrameTextPlaceholder => SelectedFramePresetOption.DefaultText;

    private QrFramePresetOption SelectedFramePresetOption => GetFramePresetOption(SelectedFramePresetIndex);

    public void Initialize(BrandItem original)
    {
        _original = original;
        EditName = original.Name;

        IncludeForeground = original.Foreground.HasValue;
        ForegroundColor = original.Foreground ?? Windows.UI.Color.FromArgb(255, 0, 0, 0);

        IncludeBackground = original.Background.HasValue;
        BackgroundColor = original.Background ?? Windows.UI.Color.FromArgb(255, 255, 255, 255);

        IncludeErrorCorrection = original.ErrorCorrectionLevelAsString is not null;
        int correctionIdx = AllCorrectionLevels.FindIndex(x => x.ErrorCorrectionLevel.ToString() == original.ErrorCorrectionLevelAsString);
        SelectedCorrectionIndex = correctionIdx >= 0 ? correctionIdx : 1;

        IncludeFrame = original.FramePreset.HasValue;
        int framePresetIndex = original.FramePreset.HasValue
            ? AllFramePresets.FindIndex(option => option.Preset == original.FramePreset.Value)
            : 0;
        SelectedFramePresetIndex = framePresetIndex >= 0 ? framePresetIndex : 0;
        EditFrameText = original.FramePreset is QrFramePreset preset && preset != QrFramePreset.None
            ? (string.IsNullOrWhiteSpace(original.FrameText)
                ? LocalizedDisplay.FrameOption(preset).DefaultText
                : original.FrameText.Trim())
            : string.Empty;

        IncludeUrl = original.UrlContent is not null;
        UrlContent = original.UrlContent ?? string.Empty;

        IncludeLogo = original.LogoImagePath is not null || original.LogoEmoji is not null;
        LogoPath = original.LogoImagePath;
        LogoSize = original.LogoSizePercentage ?? 15;
        LogoPadding = original.LogoPaddingPixels ?? 4;
    }

    public BrandItem? TryCreateEditedItem()
    {
        if (!IsNameValid)
            return null;

        QrFramePreset selectedFramePreset = SelectedFramePresetOption.Preset;

        return new BrandItem
        {
            Name = EditName.Trim(),
            CreatedDateTime = _original.CreatedDateTime,
            IsDefault = _original.IsDefault,
            Foreground = IncludeForeground ? ForegroundColor : null,
            Background = IncludeBackground ? BackgroundColor : null,
            UrlContent = IncludeUrl && !string.IsNullOrWhiteSpace(UrlContent) ? UrlContent.Trim() : null,
            ContentKind = _original.ContentKind,
            MultiLineCodeModeOverride = _original.MultiLineCodeModeOverride,
            ErrorCorrectionLevelAsString = IncludeErrorCorrection ? AllCorrectionLevels[SelectedCorrectionIndex].ErrorCorrectionLevel.ToString() : null,
            LogoImagePath = IncludeLogo ? LogoPath : null,
            LogoEmoji = IncludeLogo && string.Equals(LogoPath, _original.LogoImagePath, StringComparison.OrdinalIgnoreCase)
                ? _original.LogoEmoji
                : null,
            LogoEmojiStyle = IncludeLogo && string.Equals(LogoPath, _original.LogoImagePath, StringComparison.OrdinalIgnoreCase)
                ? _original.LogoEmojiStyle
                : null,
            LogoSizePercentage = IncludeLogo ? LogoSize : null,
            LogoPaddingPixels = IncludeLogo ? LogoPadding : null,
            FramePreset = IncludeFrame ? selectedFramePreset : null,
            Appearance = IncludeFrame ? _original.Appearance : null,
            QrPaddingModules = IncludeFrame ? _original.QrPaddingModules : null,
            FrameTextSource = _original.FrameTextSource,
            FrameText = IncludeFrame && selectedFramePreset != QrFramePreset.None
                ? NormalizeFrameText(EditFrameText)
                : null,
        };
    }

    [RelayCommand]
    private async Task SelectLogo()
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".svg");

        IntPtr windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, windowHandle);

        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
            LogoPath = file.Path;
    }

    [RelayCommand]
    private void ClearLogo()
    {
        LogoPath = null;
    }

    partial void OnSelectedFramePresetIndexChanged(int oldValue, int newValue)
    {
        QrFramePresetOption previousOption = GetFramePresetOption(oldValue);
        QrFramePresetOption currentOption = GetFramePresetOption(newValue);
        string normalizedFrameText = EditFrameText.Trim();
        bool shouldReplaceFrameText = string.IsNullOrWhiteSpace(normalizedFrameText)
            || (previousOption.Preset != QrFramePreset.None
                && string.Equals(normalizedFrameText, previousOption.DefaultText, StringComparison.Ordinal));

        if (currentOption.Preset != QrFramePreset.None && shouldReplaceFrameText)
            EditFrameText = currentOption.DefaultText;
    }

    private QrFramePresetOption GetFramePresetOption(int index)
    {
        if (index >= 0 && index < AllFramePresets.Count)
            return AllFramePresets[index];

        return AllFramePresets[0];
    }

    private static string? NormalizeFrameText(string value)
    {
        string normalizedValue = value.Trim();
        return string.IsNullOrWhiteSpace(normalizedValue)
            ? null
            : normalizedValue;
    }
}
