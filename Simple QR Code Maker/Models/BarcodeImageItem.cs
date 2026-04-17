using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
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

    public QRCode QRCodeDetails => Encoder.encode(CodeAsText, ErrorCorrection);

    private QrCodeSizeRecommendation SizeRecommendation => BarcodeHelpers.GetSizeRecommendation(32 * MaxSizeScaleFactor, QRCodeDetails.Version.DimensionForVersion, ForegroundColor, BackgroundColor, QrPaddingModules);

    public string ToolTipText
    {
        get
        {
            QrCodeSizeRecommendation sizeRecommendation = SizeRecommendation;

            if (!BarcodeHelpers.IsSizeRecommendationAvailableForPadding(QrPaddingModules))
                return CodeAsText;

            if (sizeRecommendation.Kind == QrCodeSizeRecommendationKind.TransparencyDependent)
                return CodeAsText;

            return sizeRecommendation.IsExact
                ? $"Smallest recommended size {sizeRecommendation.Text}, {CodeAsText}"
                : $"{sizeRecommendation.Text} {CodeAsText}";
        }
    }

    [ObservableProperty]
    public partial Visibility SizeTextVisible { get; set; } = Visibility.Visible;
    public double MaxSizeScaleFactor { get; set; }

    public string SizeRecommendationTitle => SizeRecommendation.IsExact ? "Minimum size" : "Print sizing unavailable";

    public string SizeRecommendationText => SizeRecommendation.Text;

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
            SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(CodeAsText, correctionLevel, foreground, background, LogoImage, LogoSizePercentage, LogoPaddingPixels, LogoSvgContent, QrPaddingModules);
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
            SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(CodeAsText, correctionLevel, foreground, background, LogoImage, LogoSizePercentage, LogoPaddingPixels, LogoSvgContent, QrPaddingModules);
            return svgImage.Content;
        }
        catch
        {
            return string.Empty;
        }
    }

    [RelayCommand]
    private void FaqButton()
    {
        WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, "Size"));
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
                    "Exact minimum size unavailable",
                    "Print sizing is unavailable because padding outside 1-4 modules makes borderless and heavy-border QR Codes scan too variably for reliable prediction.",
                    InfoBarSeverity.Informational),
                QrCodeSizeRecommendationKind.TransparencyDependent => (
                    "Exact minimum size unavailable",
                    "Transparent QR colors depend on the final print surface, so there is no exact minimum size to copy.",
                    InfoBarSeverity.Informational),
                QrCodeSizeRecommendationKind.LowContrast => (
                    "Exact minimum size unavailable",
                    "Color contrast is too low to produce a reliable minimum size recommendation.",
                    InfoBarSeverity.Warning),
                QrCodeSizeRecommendationKind.Error => (
                    "Exact minimum size unavailable",
                    "The selected maximum scan distance does not produce a reliable minimum size recommendation.",
                    InfoBarSeverity.Warning),
                _ => (
                    "Exact minimum size unavailable",
                    "There is no exact minimum size to copy for this QR Code.",
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
        WeakReferenceMessenger.Default.Send(new RequestShowMessage("QR Code size copied to the clipboard", string.Empty, InfoBarSeverity.Success));
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
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Failed to copy QR Code to the clipboard", "No QR Code to copy to the clipboard", InfoBarSeverity.Error));
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
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Failed to copy QR Code to the clipboard", "No QR Code to copy to the clipboard", InfoBarSeverity.Error));
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetStorageItems(files);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

        WeakReferenceMessenger.Default.Send(new RequestShowMessage("PNG QR Code copied to the clipboard", string.Empty, InfoBarSeverity.Success));
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
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Failed to copy QR Code to the clipboard", "No QR Code to copy to the clipboard", InfoBarSeverity.Error));
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetStorageItems(files);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

        WeakReferenceMessenger.Default.Send(new RequestShowMessage("SVG QR Code copied to the clipboard", string.Empty, InfoBarSeverity.Success));
        WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
    }

    [RelayCommand]
    private async Task ShareCodeContext()
    {
        if (CodeAsBitmap is null)
        {
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Failed to share QR Code", "No QR Code to share", InfoBarSeverity.Error));
            return;
        }

        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        string imageNameFileName = $"{CodeAsText.ToSafeFileName()}.png";
        StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);

        if (!await CodeAsBitmap.SavePngToStorageFile(file))
        {
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Failed to share QR Code", "Could not prepare PNG for sharing", InfoBarSeverity.Error));
            return;
        }

        IntPtr hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        DataTransferManager dtm = ShareHelper.GetForWindow(hwnd);

        TypedEventHandler<DataTransferManager, DataRequestedEventArgs>? handler = null;
        handler = (sender, args) =>
        {
            DataRequest request = args.Request;
            request.Data.Properties.Title = "QR Code";
            request.Data.Properties.Description = CodeAsText;
            request.Data.SetStorageItems(new[] { file });
            request.Data.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
            sender.DataRequested -= handler;
        };
        dtm.DataRequested += handler;

        ShareHelper.ShowShareUIForWindow(hwnd);
        WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
    }

    [RelayCommand]
    private void CopyCodeSvgTextContext()
    {
        string svgAsText = GetCodeAsSvgText(ForegroundColor.ToSystemDrawingColor(), BackgroundColor.ToSystemDrawingColor(), ErrorCorrection);

        if (string.IsNullOrWhiteSpace(svgAsText))
        {
            WeakReferenceMessenger.Default.Send(new RequestShowMessage("Failed to copy QR Code to the clipboard", "No QR Code to copy to the clipboard", InfoBarSeverity.Error));
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetText(svgAsText);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

        WeakReferenceMessenger.Default.Send(new RequestShowMessage("SVG Text of QR Code copied to the clipboard", string.Empty, InfoBarSeverity.Success));
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
                savePicker.FileTypeChoices.Add("PNG Image", [".png"]);
                savePicker.DefaultFileExtension = ".png";
                break;
            case FileKind.SVG:
                savePicker.FileTypeChoices.Add("SVG Image", [".svg"]);
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
                WeakReferenceMessenger.Default.Send(new RequestShowMessage("PNG QR Code Saved!", string.Empty, InfoBarSeverity.Success));
                WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
                break;
            case FileKind.SVG:
                await SaveCodeAsSvgFile(file,
                    ForegroundColor.ToSystemDrawingColor(),
                    BackgroundColor.ToSystemDrawingColor(),
                    ErrorCorrection);
                WeakReferenceMessenger.Default.Send(new RequestShowMessage("PNG QR Code Saved!", string.Empty, InfoBarSeverity.Success));
                WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
                break;
            default:
                break;
        }
    }
}
