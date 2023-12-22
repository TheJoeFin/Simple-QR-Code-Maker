using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using ZXing.QrCode.Internal;
using static Simple_QR_Code_Maker.Enums;
using static ZXing.Rendering.SvgRenderer;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    private string urlText = "";

    public ObservableCollection<BarcodeImageItem> QrCodeBitmaps { get; set; } = new();

    [ObservableProperty]
    private bool showLengthError = false;

    public bool CanSaveImage { get => !string.IsNullOrWhiteSpace(UrlText); }

    partial void OnUrlTextChanged(string value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private readonly DispatcherTimer debounceTimer = new();

    public INavigationService NavigationService
    {
        get;
    }

    public MainViewModel(INavigationService navigationService)
    {
        debounceTimer.Interval = TimeSpan.FromMilliseconds(600);
        debounceTimer.Tick += DebounceTimer_Tick;

        NavigationService = navigationService;
    }

    private void DebounceTimer_Tick(object? sender, object e)
    {
        debounceTimer.Stop();

        QrCodeBitmaps.Clear();

        if (string.IsNullOrWhiteSpace(UrlText))
            return;

        string[] lines = UrlText.Split('\r');

        foreach ( string line in lines )
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string textToUse = line.Trim();

            try
            {
                WriteableBitmap bitmap = BarcodeHelpers.GetQrCodeBitmapFromText(textToUse, ErrorCorrectionLevel.M, System.Drawing.Color.Black, System.Drawing.Color.White);
                BarcodeImageItem barcodeImageItem = new()
                {
                    CodeAsBitmap = bitmap,
                    CodeAsText = textToUse,
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
    private void GoToSettings()
    {
        NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
    }

    [RelayCommand]
    private async Task SavePng()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        if (QrCodeBitmaps.Count == 1)
        {
            await SaveSingle(FileKind.PNG, QrCodeBitmaps.First());
            return;
        }

        await SaveAllFiles(FileKind.PNG);
    }

    private static async Task SaveSingle(FileKind kindOfFile, BarcodeImageItem imageItem)
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
                savePicker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });
                savePicker.DefaultFileExtension = ".png";
                break;
            case FileKind.SVG:
                savePicker.FileTypeChoices.Add("SVG Image", new List<string>() { ".svg" });
                savePicker.DefaultFileExtension = ".svg";
                break;
            default:
                break;
        }
        savePicker.SuggestedFileName = imageItem.CodeAsText.ReplaceReservedCharacters();

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null || imageItem.CodeAsBitmap is null)
            return;

        await WriteImageToFile(imageItem, file, kindOfFile);
    }

    private static async Task WriteImageToFile(BarcodeImageItem imageItem, StorageFile file, FileKind kindOfFile)
    {
        switch (kindOfFile)
        {
            case FileKind.None:
                break;
            case FileKind.PNG:
                if (imageItem.CodeAsBitmap is null)
                    return;

                await imageItem.CodeAsBitmap.SavePngToStorageFile(file);
                break;
            case FileKind.SVG:
                {
                    SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(imageItem.CodeAsText, ErrorCorrectionLevel.M);
                    using IRandomAccessStream randomAccessStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                    DataWriter dataWriter = new(randomAccessStream);
                    dataWriter.WriteString(svgImage.Content);
                    await dataWriter.StoreAsync();
                }
                break;
            default:
                break;
        }
    }

    public async Task SaveAllFiles(FileKind kindOfFile)
    {
        FolderPicker folderPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };


        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(folderPicker, windowHandleSave);

        StorageFolder folder = await folderPicker.PickSingleFolderAsync();

        if (folder is null)
            return;

        string extension = $".{kindOfFile.ToString().ToLower()}";

        foreach (BarcodeImageItem imageItem in QrCodeBitmaps)
        {
            string fileName = imageItem.CodeAsText.ReplaceReservedCharacters();

            if (string.IsNullOrWhiteSpace(fileName) || imageItem.CodeAsBitmap is null)
                continue;

            fileName += extension;

            StorageFile file = await folder.CreateFileAsync(fileName);
            await WriteImageToFile(imageItem, file, kindOfFile);
        }
    }


    [RelayCommand]
    private async Task SaveSvg()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        if (QrCodeBitmaps.Count == 1)
        {
            await SaveSingle(FileKind.SVG, QrCodeBitmaps.First());
            return;
        }

        await SaveAllFiles(FileKind.SVG);
    }
}
