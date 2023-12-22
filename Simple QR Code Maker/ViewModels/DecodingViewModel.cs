using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Controls;
using Simple_QR_Code_Maker.Helpers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
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

    public ObservableCollection<TextBorder> CodeBorders { get; set; } = new();

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
    private async Task TryLaunchLink()
    {
        bool success = Uri.TryCreate(InfoBarMessage, UriKind.Absolute, out Uri? uri);

        if (success && uri is not null)
        {
            _ = await Launcher.LaunchUriAsync(uri);
        }
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
        CodeBorders.Clear();
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

        StorageFile pickedFile = await fileOpenPicker.PickSingleFileAsync();

        if (pickedFile is null)
            return;

        Uri uri = new(pickedFile.Path);
        PickedImage = new(uri);

        var strings = BarcodeHelpers.GetStringsFromImageFile(pickedFile);

        if (!strings.Any())
        {
            InfoBarMessage = "Could not read any codes. Could be there are none present or content failed to read.\rIf you believe this is an issue with the app, please email joe@joefinapps.com";
            IsInfoBarShowing = true;
            return;
        }

        foreach ((string, Result) item in strings)
        {
            TextBorder textBorder = new(item.Item2);
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            CodeBorders.Add(textBorder);
        }
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

    }
}
