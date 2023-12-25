using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Controls;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Drawing;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using WinRT.Interop;
using ZXing;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class DecodingViewModel : ObservableRecipient, INavigationAware
{
    [ObservableProperty]
    private string infoBarMessage = string.Empty;

    [ObservableProperty]
    private bool isInfoBarShowing = false;

    [ObservableProperty]
    private BitmapImage? pickedImage;

    [ObservableProperty]
    private ObservableCollection<DecodingImageItem> decodingImageItems = new();

    private INavigationService NavigationService { get; }

    public DecodingViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
    }

    public async void OnNavigatedTo(object parameter)
    {
        await OpenNewFileCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearImages()
    {
        DecodingImageItems.Clear();
    }

    [RelayCommand]
    private async Task OpenFileFromClipboard()
    {
        DecodingImageItems.Clear();
        DataPackageView clipboardData = Clipboard.GetContent();

        if (clipboardData.Contains(StandardDataFormats.StorageItems))
        {
            IReadOnlyList<IStorageItem> clipboardItems = await clipboardData.GetStorageItemsAsync();
            if (clipboardItems.Count == 0)
                return;

            OpenAndDecodeStorageFiles(clipboardItems);
        }
        else if (clipboardData.Contains(StandardDataFormats.Bitmap))
        {
            RandomAccessStreamReference bitmapStreamRef = await clipboardData.GetBitmapAsync();

            IRandomAccessStreamWithContentType stream = await bitmapStreamRef.OpenReadAsync();

            if (stream is not null)
                OpenAndDecodeBitmap(stream);
        }

    }

    [RelayCommand]
    private async Task TryLaunchLink()
    {
        bool success = Uri.TryCreate(InfoBarMessage, UriKind.Absolute, out Uri? uri);

        if (success && uri is not null)
        {
            _ = await Launcher.LaunchUriAsync(uri);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (NavigationService.CanGoBack)
            NavigationService.GoBack();
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
        PickedImage = null;
        DecodingImageItems.Clear();
        IsInfoBarShowing = false;

        FileOpenPicker fileOpenPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
        };
        fileOpenPicker.FileTypeFilter.Add(".png");
        fileOpenPicker.FileTypeFilter.Add(".jpg");
        fileOpenPicker.FileTypeFilter.Add(".jpeg");
        fileOpenPicker.FileTypeFilter.Add(".bmp");

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(fileOpenPicker, windowHandleSave);

        IReadOnlyList<StorageFile>? pickedFiles = await fileOpenPicker.PickMultipleFilesAsync();

        if (pickedFiles is null || pickedFiles.Count == 0)
            return;

        OpenAndDecodeStorageFiles(pickedFiles);
    }

    private void OpenAndDecodeBitmap(IRandomAccessStreamWithContentType streamWithContentType)
    {
        Bitmap bitmap = new(streamWithContentType.AsStreamForRead());
        string cachePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{DateTimeOffset.Now.Ticks}.png");
        bitmap.Save(cachePath);

        Uri uri = new(cachePath);
        BitmapImage thisPickedImage = new(uri);

        IEnumerable<(string, Result)> strings = BarcodeHelpers.GetStringsFromBitmap(bitmap);

        if (!strings.Any())
        {
            InfoBarMessage = "Could not read any codes. Could be there are none present or content failed to read.\rIf you believe this is an issue with the app, please email joe@joefinapps.com";
            IsInfoBarShowing = true;
            return;
        }

        ObservableCollection<TextBorder> codeBorders = new();
        foreach ((string, Result) item in strings)
        {
            TextBorder textBorder = new(item.Item2);
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            codeBorders.Add(textBorder);
        }

        DecodingImageItem decodingImage = new()
        {
            Name = cachePath,
            BitmapImage = thisPickedImage,
            CodeBorders = codeBorders,
        };

        DecodingImageItems.Add(decodingImage);
        // try { File.Delete(cachePath); } catch { };
    }

    private void OpenAndDecodeStorageFiles(IReadOnlyList<IStorageItem> pickedFiles)
    {
        foreach (var file in pickedFiles)
        {
            if (file is not StorageFile storageFile)
                continue;

            var decodedItem = GetDecodingImageItemFromStorageFile(storageFile);
            if (decodedItem is not null)
                DecodingImageItems.Add(decodedItem);
        }

    }

    private DecodingImageItem? GetDecodingImageItemFromStorageFile(StorageFile storageFile)
    {
        Uri uri = new(storageFile.Path);
        BitmapImage thisPickedImage = new(uri);

        IEnumerable<(string, Result)> strings = BarcodeHelpers.GetStringsFromImageFile(storageFile);

        if (!strings.Any())
        {
            InfoBarMessage = $"Could not read codes on {Path.GetFileName(storageFile.Path)}.\rCould be there are none present or content failed to read.\rIf you believe this is an issue with the app, please email joe@joefinapps.com";
            IsInfoBarShowing = true;
        }

        ObservableCollection<TextBorder> codeBorders = new();
        foreach ((string, Result) item in strings)
        {
            TextBorder textBorder = new(item.Item2);
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            codeBorders.Add(textBorder);
        }

        DecodingImageItem decodingImage = new()
        {
            Name = storageFile.Path,
            BitmapImage = thisPickedImage,
            CodeBorders = codeBorders,
        };

        return decodingImage;
    }

    private void TextBorder_TextBorder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBorder textBorder)
            return;

        InfoBarMessage = textBorder.BorderInfo.Text;
        IsInfoBarShowing = true;
    }

    public void OnNavigatedFrom()
    {
        IsInfoBarShowing = false;
        PickedImage = null;
    }
}
