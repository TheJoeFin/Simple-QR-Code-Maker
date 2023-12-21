using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using ZXing.QrCode.Internal;
using static ZXing.Rendering.SvgRenderer;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    private string urlText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    private WriteableBitmap? qrCodeSource;

    public ObservableCollection<BarcodeImageItem> QrCodeBitmaps { get; set; } = new();

    [ObservableProperty]
    private bool showLengthError = false;

    public bool CanSaveImage { get => QrCodeSource is not null && !string.IsNullOrWhiteSpace(UrlText); }

    partial void OnUrlTextChanged(string value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private readonly DispatcherTimer debounceTimer = new();

    public MainViewModel()
    {
        debounceTimer.Interval = TimeSpan.FromMilliseconds(400);
        debounceTimer.Tick += DebounceTimer_Tick;
    }

    private void DebounceTimer_Tick(object? sender, object e)
    {
        debounceTimer.Stop();

        QrCodeSource = null;
        QrCodeBitmaps.Clear();

        if (string.IsNullOrWhiteSpace(UrlText))
            return;

        string textToEncode = UrlText;
        string[] lines = UrlText.Split();

        foreach ( string line in lines )
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                WriteableBitmap bitmap = BarcodeHelpers.GetQrCodeBitmapFromText(line, ErrorCorrectionLevel.M, System.Drawing.Color.Black, System.Drawing.Color.White);
                BarcodeImageItem barcodeImageItem = new()
                {
                    CodeAsBitmap = bitmap,
                    CodeAsText = line,
                };

                QrCodeBitmaps.Add(barcodeImageItem);
                ShowLengthError = false;
            }
            catch (ZXing.WriterException)
            {
                ShowLengthError = true;
            }
        }
    }

    [RelayCommand]
    private async Task SavePng()
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        savePicker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });
        savePicker.DefaultFileExtension = ".png";
        savePicker.SuggestedFileName = "QR Code";

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null || QrCodeSource is null)
            return;

        await QrCodeSource.SavePngToStorageFile(file);
    }

    [RelayCommand]
    private async Task SaveSvg()
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        savePicker.FileTypeChoices.Add("SVG Image", new List<string>() { ".svg" });
        savePicker.DefaultFileExtension = ".svg";
        savePicker.SuggestedFileName = "QR Code";

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

        StorageFile file = await savePicker.PickSaveFileAsync();

        SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(UrlText, ErrorCorrectionLevel.M);

        if (file is null || QrCodeSource is null || string.IsNullOrWhiteSpace(svgImage.Content))
            return;

        using IRandomAccessStream randomAccessStream = await file.OpenAsync(FileAccessMode.ReadWrite);
        DataWriter dataWriter = new(randomAccessStream);
        dataWriter.WriteString(svgImage.Content);
        await dataWriter.StoreAsync();
    }
}
