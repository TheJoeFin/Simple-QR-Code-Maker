using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
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
    private bool canPasteImage = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private DecodingImageItem? currentDecodingItem = null;

    [ObservableProperty]
    private bool isAdvancedToolsVisible = false;

    public bool HasImage => CurrentDecodingItem is not null;

    private HistoryItem? navigationHistoryItem = null;

    private INavigationService NavigationService { get; }

    private readonly List<string> imageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
    ];

    public DecodingViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;

        CheckIfCanPaste();

        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        Clipboard.ContentChanged += Clipboard_ContentChanged;
    }

    ~DecodingViewModel()
    {
        Clipboard.ContentChanged -= Clipboard_ContentChanged;
    }

    private void Clipboard_ContentChanged(object? sender, object e) => CheckIfCanPaste();

    private void CheckIfCanPaste()
    {
        DataPackageView clipboardData = Clipboard.GetContent();

        if (clipboardData.Contains(StandardDataFormats.StorageItems)
            || clipboardData.Contains(StandardDataFormats.Bitmap))
            CanPasteImage = true;
        else
            CanPasteImage = false;
    }

    public void OnNavigatedTo(object parameter)
    {
        // Store the HistoryItem to pass back when returning to main page
        if (parameter is HistoryItem historyItem)
        {
            navigationHistoryItem = historyItem;
        }
        // For backward compatibility, also handle string parameter
        else if (parameter is string url)
        {
            navigationHistoryItem = new HistoryItem { CodesContent = url };
        }
    }

    [RelayCommand]
    private void ClearImages()
    {
        CurrentDecodingItem = null;
        IsInfoBarShowing = false;
        InfoBarMessage = string.Empty;
        IsAdvancedToolsVisible = false;
    }

    [RelayCommand]
    private void ToggleAdvancedTools()
    {
        IsAdvancedToolsVisible = !IsAdvancedToolsVisible;
    }

    [RelayCommand]
    private async Task ApplyAdvancedToolsAndRedecode(DecodingImageItem item)
    {
        if (item?.ProcessedMagickImage == null)
            return;

        var bitmap = ImageProcessingHelper.ConvertToBitmap(item.ProcessedMagickImage);

        string cachePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{DateTimeOffset.Now.Ticks}_processed.png");
        bitmap.Save(cachePath);

        Uri uri = new($"{cachePath}?tick={DateTimeOffset.Now.Ticks}");
        BitmapImage processedBitmapImage = new(uri)
        {
            CreateOptions = BitmapCreateOptions.IgnoreImageCache
        };

        IEnumerable<(string, Result)> strings = BarcodeHelpers.GetStringsFromBitmap(bitmap);

        ObservableCollection<TextBorder> codeBorders = [];
        foreach ((string, Result) resultItem in strings)
        {
            TextBorder textBorder = new(resultItem.Item2);
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            codeBorders.Add(textBorder);
        }

        item.ProcessedBitmapImage = processedBitmapImage;
        item.CodeBorders = codeBorders;
        item.ImagePixelWidth = (int)item.ProcessedMagickImage.Width;
        item.ImagePixelHeight = (int)item.ProcessedMagickImage.Height;
        item.BitmapImage = processedBitmapImage;
    }

    [RelayCommand]
    private async Task OpenFileFromClipboard()
    {
        CurrentDecodingItem = null;
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
            _ = await Launcher.LaunchUriAsync(uri);
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationService.NavigateTo(typeof(MainViewModel).FullName!, navigationHistoryItem);
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
        CurrentDecodingItem = null;
        IsInfoBarShowing = false;

        FileOpenPicker fileOpenPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
        };

        foreach (string extension in imageExtensions)
            fileOpenPicker.FileTypeFilter.Add(extension);

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(fileOpenPicker, windowHandleSave);

        StorageFile? pickedFile = await fileOpenPicker.PickSingleFileAsync();

        if (pickedFile is null)
            return;

        await OpenAndDecodeStorageFile(pickedFile);
    }

    [RelayCommand]
    private void EditQrCode(object parameter)
    {
        if (string.IsNullOrWhiteSpace(InfoBarMessage))
            return;

        // Create a new HistoryItem with the decoded text, preserving other state if available
        var editHistoryItem = navigationHistoryItem != null 
            ? new HistoryItem 
            {
                CodesContent = InfoBarMessage,
                Foreground = navigationHistoryItem.Foreground,
                Background = navigationHistoryItem.Background,
                ErrorCorrection = navigationHistoryItem.ErrorCorrection,
                LogoImagePath = navigationHistoryItem.LogoImagePath,
                LogoSizePercentage = navigationHistoryItem.LogoSizePercentage,
                LogoPaddingPixels = navigationHistoryItem.LogoPaddingPixels,
            }
            : new HistoryItem { CodesContent = InfoBarMessage };
            
        NavigationService.NavigateTo(typeof(MainViewModel).FullName!, editHistoryItem);
    }

    private void OpenAndDecodeBitmap(IRandomAccessStreamWithContentType streamWithContentType)
    {
        Bitmap bitmap = new(streamWithContentType.AsStreamForRead());
        string cachePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{DateTimeOffset.Now.Ticks}.png");
        bitmap.Save(cachePath);

        Uri uri = new($"{cachePath}?tick={DateTimeOffset.Now.Ticks}");
        BitmapImage thisPickedImage = new(uri)
        {
            CreateOptions = BitmapCreateOptions.IgnoreImageCache
        };

        IEnumerable<(string, Result)> strings = BarcodeHelpers.GetStringsFromBitmap(bitmap);

        ObservableCollection<TextBorder> codeBorders = [];
        foreach ((string, Result) item in strings)
        {
            TextBorder textBorder = new(item.Item2);
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            codeBorders.Add(textBorder);
        }

        // Load as MagickImage and apply EXIF orientation
        var magickImage = ImageProcessingHelper.LoadImageFromBitmap(bitmap);

        DecodingImageItem decodingImage = new()
        {
            ImagePath = cachePath,
            BitmapImage = thisPickedImage,
            ImagePixelWidth = (int)magickImage.Width,
            ImagePixelHeight = (int)magickImage.Height,
            CodeBorders = codeBorders,
            OriginalMagickImage = magickImage,
        };

        CurrentDecodingItem = decodingImage;
    }

    public async void OpenAndDecodeStorageFiles(IReadOnlyList<IStorageItem> pickedFiles)
    {
        IStorageItem? first = pickedFiles.FirstOrDefault();
        if (first is StorageFile storageFile)
            await OpenAndDecodeStorageFile(storageFile);
    }

    public async Task OpenAndDecodeStorageFile(StorageFile storageFile)
    {
        DecodingImageItem? decodedItem = await GetDecodingImageItemFromStorageFileAsync(storageFile);
        if (decodedItem is not null)
            CurrentDecodingItem = decodedItem;
    }

    private async Task<DecodingImageItem?> GetDecodingImageItemFromStorageFileAsync(StorageFile storageFile)
    {
        Uri uri = new($"{storageFile.Path}?tick={DateTimeOffset.Now.Ticks}");

        if (!imageExtensions.Contains(Path.GetExtension(storageFile.Path).ToLowerInvariant()))
            return null;

        BitmapImage thisPickedImage = new(uri)
        {
            CreateOptions = BitmapCreateOptions.IgnoreImageCache
        };

        IEnumerable<(string, Result)> strings = BarcodeHelpers.GetStringsFromImageFile(storageFile);

        ObservableCollection<TextBorder> codeBorders = [];
        foreach ((string, Result) item in strings)
        {
            TextBorder textBorder = new(item.Item2);
            textBorder.TextBorder_Click += TextBorder_TextBorder_Click;
            codeBorders.Add(textBorder);
        }

        // Load image using ImageProcessingHelper which handles EXIF orientation properly
        var magickImage = await ImageProcessingHelper.LoadImageFromStorageFile(storageFile);

        DecodingImageItem decodingImage = new()
        {
            ImagePath = storageFile.Path,
            BitmapImage = thisPickedImage,
            ImagePixelWidth = (int)magickImage.Width,
            ImagePixelHeight = (int)magickImage.Height,
            CodeBorders = codeBorders,
            OriginalMagickImage = magickImage,
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
        CurrentDecodingItem = null;
    }
}
