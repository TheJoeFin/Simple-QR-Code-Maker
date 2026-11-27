using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using ZXing.QrCode.Internal;
using static ZXing.Rendering.SvgRenderer;

namespace Simple_QR_Code_Maker.Models;

public partial class BarcodeImageItem : ObservableRecipient
{
    public string CodeAsText { get; set; } = string.Empty;

    public bool IsCodeUrl => Uri.IsWellFormedUriString(CodeAsText, UriKind.Absolute);

    public bool IsAppShowingUrlWarnings { get; set; } = true;

    public bool UrlWarning => !IsCodeUrl && IsAppShowingUrlWarnings;

    public WriteableBitmap? CodeAsBitmap { get; set; }

    public ErrorCorrectionLevel ErrorCorrection { get; set; } = ErrorCorrectionLevel.M;

    public Windows.UI.Color ForegroundColor { get; set; }

    public Windows.UI.Color BackgroundColor { get; set; }

    public System.Drawing.Bitmap? LogoImage { get; set; }

    public double LogoSizePercentage { get; set; } = 20.0;

    public double LogoPaddingPixels { get; set; } = 8.0;

    public double QrPaddingModules { get; set; } = 2.0;

    public string? LogoSvgContent { get; set; }

    public QrFramePreset FramePreset { get; set; } = QrFramePreset.None;

    public string? FrameText { get; set; }

    public QrCodeAppearance Appearance { get; set; } = new();

    public bool IsAutoBranded { get; set; } = false;

    public bool IsCommonBranded { get; set; }

    public bool RecommendUsingBrandThemes { get; set; } = true;

    public CommonBrandPreset? CommonBrand => CommonBrandCatalog.FindMatching(CodeAsText);

    public bool HasCommonBrand => CommonBrand is not null;

    public bool CanApplyCommonBrand => HasCommonBrand && !IsCommonBranded;

    public bool CanRecommendCommonBrand => RecommendUsingBrandThemes && CanApplyCommonBrand;

    public bool ShowCommonBrandAction => IsCommonBranded || CanRecommendCommonBrand;

    public bool CanUseCurrentStyle => IsAutoBranded || IsCommonBranded;

    public string CommonBrandName => CommonBrand?.Name ?? string.Empty;

    public Uri? CommonBrandLogoUri => CommonBrand is CommonBrandPreset brand
        ? new Uri(brand.LogoPath)
        : null;

    public string CommonBrandActionName => IsCommonBranded
        ? "Preview_RestoreStyle".FormatLocalized(CommonBrandName)
        : "Preview_UseBrand".FormatLocalized(CommonBrandName);

    public string CommonBrandToolTip => IsCommonBranded
        ? "Preview_RestoreStyleHelp".FormatLocalized(CommonBrand?.Name)
        : "Preview_UseBrandHelp".FormatLocalized(CommonBrand?.Name);

    public QRCode QRCodeDetails => BarcodeHelpers.EncodeQrCode(CodeAsText, ErrorCorrection);

    private QrCodeSizeRecommendation SizeRecommendation => BarcodeHelpers.GetSizeRecommendation(32 * MaxSizeScaleFactor, QRCodeDetails.Version.DimensionForVersion, ForegroundColor, BackgroundColor, QrPaddingModules);

    public string ToolTipText
    {
        get
        {
            QrCodeSizeRecommendation sizeRecommendation = SizeRecommendation;

            if (!BarcodeHelpers.IsSizeRecommendationAvailableForPadding(QrPaddingModules)
                || Appearance.ModuleStyle != QrModuleStyle.Square
                || Appearance.EyeStyle != QrEyeStyle.Square)
                return CodeAsText;

            if (sizeRecommendation.Kind == QrCodeSizeRecommendationKind.TransparencyDependent)
                return CodeAsText;

            string sizeText = LocalizedDisplay.SizeRecommendation(sizeRecommendation);
            return sizeRecommendation.IsExact
                ? "Preview_MinimumSizeAndContent".FormatLocalized(sizeText, CodeAsText)
                : "Preview_MetadataAndContent".FormatLocalized(sizeText, CodeAsText);
        }
    }

    [ObservableProperty]
    public partial Visibility SizeTextVisible { get; set; } = Visibility.Visible;
    public double MaxSizeScaleFactor { get; set; }

    public string SizeRecommendationTitle => SizeRecommendation.IsExact ? "Preview_MinimumSize".GetLocalized() : "Preview_SizingUnavailable".GetLocalized();

    public string SizeRecommendationText => LocalizedDisplay.SizeRecommendation(SizeRecommendation);

    public bool CanCopySizeText => SizeRecommendation.IsExact;

    // The contrast ratio between the selected colors before any transparent QR output
    // is composited onto the real print surface.
    // A value of 1:1 is the minimum, and 21:1 is the maximum.
    // The higher the value, the better the contrast.
    // anything less than 2 will be illegible for most applications
    public double ColorContrastRatio => ColorHelpers.GetContrastRatio(ForegroundColor, BackgroundColor);

    public async Task<bool> SaveCodeAsPngFile(StorageFile file)
    {
        if (CodeAsBitmap is null)
            return false;

        return await CodeAsBitmap.SavePngToStorageFile(file);
    }

    public async Task<bool> SaveCodeAsSvgFile(StorageFile file, System.Drawing.Color foreground, System.Drawing.Color background, ErrorCorrectionLevel correctionLevel)
    {
        try
        {
            SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(CodeAsText, correctionLevel, foreground, background, LogoImage, LogoSizePercentage, LogoPaddingPixels, LogoSvgContent, QrPaddingModules, FramePreset, FrameText, Appearance);
            using IRandomAccessStream randomAccessStream = await file.OpenAsync(FileAccessMode.ReadWrite);
            DataWriter dataWriter = new(randomAccessStream);
            dataWriter.WriteString(svgImage.Content);
            await dataWriter.StoreAsync();
        }
        catch
        {
            return false;
        }

        return true;
    }

    public string GetCodeAsSvgText(System.Drawing.Color foreground, System.Drawing.Color background, ErrorCorrectionLevel correctionLevel)
    {
        try
        {
            SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(CodeAsText, correctionLevel, foreground, background, LogoImage, LogoSizePercentage, LogoPaddingPixels, LogoSvgContent, QrPaddingModules, FramePreset, FrameText, Appearance);
            return svgImage.Content;
        }
        catch
        {
            return string.Empty;
        }
    }

    [RelayCommand]
    private void UseCommonBrandContext()
    {
        WeakReferenceMessenger.Default.Send(new UseCommonBrandMessage(this));
    }

    [RelayCommand]
    private void IgnoreAutoBrandContext()
    {
        WeakReferenceMessenger.Default.Send(new IgnoreAutoBrandMessage(this));
    }

    [RelayCommand]
    private void FaqButton()
    {
        WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, topic: FaqTopic.Size));
    }

    [RelayCommand]
    private void CopySizeText()
    {
        if (!CanCopySizeText)
        {
            QrCodeSizeRecommendation sizeRecommendation = SizeRecommendation;

            (string title, string message, InfoBarSeverity severity) = sizeRecommendation.Kind switch
            {
                QrCodeSizeRecommendationKind.PaddingDependent => (
                    "Preview_MinimumSizeCopyFailed".GetLocalized(),
                    "Preview_PaddingSizingHelp".GetLocalized(),
                    InfoBarSeverity.Informational),
                QrCodeSizeRecommendationKind.TransparencyDependent => (
                    "Preview_MinimumSizeCopyFailed".GetLocalized(),
                    "Preview_TransparencySizingHelp".GetLocalized(),
                    InfoBarSeverity.Informational),
                QrCodeSizeRecommendationKind.LowContrast => (
                    "Preview_MinimumSizeCopyFailed".GetLocalized(),
                    "Preview_ContrastSizingHelp".GetLocalized(),
                    InfoBarSeverity.Warning),
                QrCodeSizeRecommendationKind.Error => (
                    "Preview_MinimumSizeCopyFailed".GetLocalized(),
                    "Preview_DistanceSizingHelp".GetLocalized(),
                    InfoBarSeverity.Warning),
                _ => (
                    "Preview_MinimumSizeCopyFailed".GetLocalized(),
                    "Preview_NoSizingHelp".GetLocalized(),
                    InfoBarSeverity.Informational)
            };

            WeakReferenceMessenger.Default.Send(new RequestShowMessage(
                title,
                message,
                severity));
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetText(SizeRecommendationText);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });
        WeakReferenceMessenger.Default.Send(new RequestShowMessage("Preview_SizeCopied".GetLocalized(), string.Empty, InfoBarSeverity.Success));
    }

    [RelayCommand]
    private void HideSizeText()
    {
        SizeTextVisible = Visibility.Collapsed;
    }

    [RelayCommand]
    private async Task SaveCodePngContext()
    {
        await SaveSingle(FileKind.PNG);
    }

    [RelayCommand]
    private async Task SaveCodeSvgContext()
    {
        await SaveSingle(FileKind.SVG);
    }

    [RelayCommand]
    private async Task CopyCodePngContext()
    {
        if (CodeAsBitmap is null)
        {
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Preview_CopyFailed".GetLocalized(), "Preview_NothingToCopy".GetLocalized(), InfoBarSeverity.Error));
            return;
        }

        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        List<StorageFile> files = [];

        string? imageNameFileName = $"{CodeAsText.ToSafeFileName()}.png";
        StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);
        _ = await CodeAsBitmap.SavePngToStorageFile(file);

        files.Add(file);

        if (files.Count == 0)
        {
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Preview_CopyFailed".GetLocalized(), "Preview_NothingToCopy".GetLocalized(), InfoBarSeverity.Error));
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetStorageItems(files);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

        WeakReferenceMessenger.Default.Send(new RequestShowMessage("Main_PngCopied".GetLocalized(), string.Empty, InfoBarSeverity.Success));
        WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
    }

    [RelayCommand]
    private async Task CopyCodeSvgContext()
    {
        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        List<StorageFile> files = [];

        string? imageNameFileName = $"{CodeAsText.ToSafeFileName()}.svg";
        StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);

        _ = await SaveCodeAsSvgFile(file, ForegroundColor.ToSystemDrawingColor(), BackgroundColor.ToSystemDrawingColor(), ErrorCorrection);

        files.Add(file);

        if (files.Count == 0)
        {
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Preview_CopyFailed".GetLocalized(), "Preview_NothingToCopy".GetLocalized(), InfoBarSeverity.Error));
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetStorageItems(files);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

        WeakReferenceMessenger.Default.Send(new RequestShowMessage("Main_SvgCopied".GetLocalized(), string.Empty, InfoBarSeverity.Success));
        WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
    }

    [RelayCommand]
    private async Task ShareCodeContext()
    {
        try
        {
            await ShareHelper.ShareQrCodesAsync([this]);
            WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to share QR Code: {ex}");
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Preview_ShareFailed".GetLocalized(), "Preview_ShareFailedHelp".GetLocalized(), InfoBarSeverity.Error));
        }
    }

    [RelayCommand]
    private void CopyCodeSvgTextContext()
    {
        string svgAsText = GetCodeAsSvgText(ForegroundColor.ToSystemDrawingColor(), BackgroundColor.ToSystemDrawingColor(), ErrorCorrection);

        if (string.IsNullOrWhiteSpace(svgAsText))
        {
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Preview_CopyFailed".GetLocalized(), "Preview_NothingToCopy".GetLocalized(), InfoBarSeverity.Error));
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetText(svgAsText);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

        WeakReferenceMessenger.Default.Send(new RequestShowMessage("Preview_SvgTextCopied".GetLocalized(), string.Empty, InfoBarSeverity.Success));
        WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
    }

    private async Task SaveSingle(FileKind kindOfFile)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        switch (kindOfFile)
        {
            case FileKind.None:
                break;
            case FileKind.PNG:
                savePicker.FileTypeChoices.Add("FileType_Png".GetLocalized(), [".png"]);
                savePicker.DefaultFileExtension = ".png";
                break;
            case FileKind.SVG:
                savePicker.FileTypeChoices.Add("FileType_Svg".GetLocalized(), [".svg"]);
                savePicker.DefaultFileExtension = ".svg";
                break;
            default:
                break;
        }
        savePicker.SuggestedFileName = CodeAsText.ToSafeFileName();

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null || CodeAsBitmap is null)
            return;

        switch (kindOfFile)
        {
            case FileKind.None:
                break;
            case FileKind.PNG:
                if (CodeAsBitmap is null)
                    return;

                await CodeAsBitmap.SavePngToStorageFile(file);
                WeakReferenceMessenger.Default.Send(new RequestShowMessage("Preview_PngSaved".GetLocalized(), string.Empty, InfoBarSeverity.Success));
                WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
                break;
            case FileKind.SVG:
                await SaveCodeAsSvgFile(file,
                    ForegroundColor.ToSystemDrawingColor(),
                    BackgroundColor.ToSystemDrawingColor(),
                    ErrorCorrection);
                WeakReferenceMessenger.Default.Send(new RequestShowMessage("Preview_PngSaved".GetLocalized(), string.Empty, InfoBarSeverity.Success));
                WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
                break;
            default:
                break;
        }
    }
}