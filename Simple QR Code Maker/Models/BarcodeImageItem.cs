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
            SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(CodeAsText, correctionLevel, foreground, background);
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
            SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(CodeAsText, correctionLevel, foreground, background);
            return svgImage.Content;
        }
        catch
        {
            return string.Empty;
        }
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
    private void CopyCodeSvgTextContext()
    {
        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;

        string? imageNameFileName = $"{CodeAsText.ToSafeFileName()}.svg";
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
