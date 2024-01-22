using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    private string urlText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    private ObservableCollection<BarcodeImageItem> qrCodeBitmaps = new();

    [ObservableProperty]
    private string placeholderText = "ex: http://www.example.com";

    private readonly string[] placeholdersList =
    {
        "http://example.com",
        "https://www.wikipedia.org",
        "https://www.JoeFinApps.com",
        "https://github.com",
        "https://github.com/TheJoeFin/Simple-QR-Code-Maker",
        "https://github.com/TheJoeFin/Text-Grab",
        "https://github.com/TheJoeFin/Simple-Icon-File-Maker"
    };

    [ObservableProperty]
    private bool showLengthError = false;

    [ObservableProperty]
    private Windows.UI.Color backgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);

    [ObservableProperty]
    private Windows.UI.Color foregroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);

    [ObservableProperty]
    private ErrorCorrectionOptions selectedOption = new("Medium 15%", ErrorCorrectionLevel.M);

    [ObservableProperty]
    private bool isHistoryPaneOpen = false;

    [ObservableProperty]
    private ObservableCollection<HistoryItem> historyItems = new();

    [ObservableProperty]
    private HistoryItem? selectedHistoryItem = null;

    private MultiLineCodeMode MultiLineCodeMode = MultiLineCodeMode.OneLineOneCode;
    private string BaseText = string.Empty;
    private bool WarnWhenNotUrl = true;

    [ObservableProperty]
    private bool canPasteText = false;

    partial void OnSelectedHistoryItemChanged(HistoryItem? value)
    {
        if (value is null)
            return;

        IsHistoryPaneOpen = false;
        UrlText = value.CodesContent;
        ForegroundColor = value.Foreground;
        BackgroundColor = value.Background;
        SelectedOption = ErrorCorrectionLevels.First(x => x.ErrorCorrectionLevel == value.ErrorCorrection);

        SelectedHistoryItem = null;
    }

    public List<ErrorCorrectionOptions> ErrorCorrectionLevels { get; } = new()
    {
        new("Low 7%", ErrorCorrectionLevel.L),
        new("Medium 15%", ErrorCorrectionLevel.M),
        new("Quarter 25%", ErrorCorrectionLevel.Q),
        new("High 30%", ErrorCorrectionLevel.H),
    };

    partial void OnSelectedOptionChanged(ErrorCorrectionOptions value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    partial void OnBackgroundColorChanged(Windows.UI.Color value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    partial void OnForegroundColorChanged(Windows.UI.Color value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    public bool CanSaveImage { get => !string.IsNullOrWhiteSpace(UrlText); }

    partial void OnUrlTextChanged(string value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private readonly DispatcherTimer debounceTimer = new();

    private readonly DispatcherTimer placeholderTextTimer = new();

    public INavigationService NavigationService { get; }

    public ILocalSettingsService LocalSettingsService { get; }

    public MainViewModel(INavigationService navigationService, ILocalSettingsService localSettingsService)
    {
        debounceTimer.Interval = TimeSpan.FromMilliseconds(600);
        debounceTimer.Tick -= DebounceTimer_Tick;
        debounceTimer.Tick += DebounceTimer_Tick;

        placeholderTextTimer.Interval = TimeSpan.FromSeconds(6);
        placeholderTextTimer.Tick -= PlaceholderTextTimer_Tick;
        placeholderTextTimer.Tick += PlaceholderTextTimer_Tick;
        placeholderTextTimer.Start();

        NavigationService = navigationService;
        LocalSettingsService = localSettingsService;

        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        Clipboard.ContentChanged += Clipboard_ContentChanged;
    }

    private void Clipboard_ContentChanged(object? sender, object e) => CheckCanPasteText();
    private void CheckCanPasteText()
    {
        var clipboardData = Clipboard.GetContent();

        if (clipboardData.Contains(StandardDataFormats.Text))
            CanPasteText = true;
        else
            CanPasteText = false;
    }

    ~MainViewModel()
    {
        placeholderTextTimer?.Stop();

        if (placeholderTextTimer is not null)
            placeholderTextTimer.Tick -= PlaceholderTextTimer_Tick;

        debounceTimer?.Stop();
        if (debounceTimer is not null)
            debounceTimer.Tick -= DebounceTimer_Tick;

        Clipboard.ContentChanged -= Clipboard_ContentChanged;
    }

    private void PlaceholderTextTimer_Tick(object? sender, object e)
    {
        Random random = new();
        PlaceholderText = $"ex: {placeholdersList[random.Next(placeholdersList.Length)]}";
    }


    private void DebounceTimer_Tick(object? sender, object e)
    {
        debounceTimer.Stop();

        QrCodeBitmaps.Clear();

        if (string.IsNullOrWhiteSpace(UrlText))
            return;

        if (MultiLineCodeMode == MultiLineCodeMode.OneLineOneCode)
        {
            string[] lines = UrlText.Split('\r');
            GenerateQrCodesFromLines(lines);
            return;
        }

        GenerateQrCodeFromOneLine(UrlText);
    }

    private void GenerateQrCodeFromOneLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        string textToUse = text.Trim();

        try
        {
            WriteableBitmap bitmap = BarcodeHelpers.GetQrCodeBitmapFromText(
                textToUse,
                SelectedOption.ErrorCorrectionLevel,
                ForegroundColor.ToSystemDrawingColor(),
                BackgroundColor.ToSystemDrawingColor());
            BarcodeImageItem barcodeImageItem = new()
            {
                CodeAsBitmap = bitmap,
                CodeAsText = textToUse,
                IsAppShowingUrlWarnings = WarnWhenNotUrl,
            };

            QrCodeBitmaps.Add(barcodeImageItem);
            ShowLengthError = false;
        }
        catch (ZXing.WriterException)
        {
            ShowLengthError = true;
        }
    }

    private void GenerateQrCodesFromLines(string[] lines)
    {
        foreach (string line in lines)
            GenerateQrCodeFromOneLine(line);
    }

    [RelayCommand]
    private async Task PasteTextIntoUrlText()
    {
        var clipboardContent = Clipboard.GetContent();
        if (clipboardContent.Contains(StandardDataFormats.Text))
        {
            string text = await clipboardContent.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.Trim();
                UrlText = text;
            }
        }
    }

    [RelayCommand]
    private async Task CopyPngToClipboard()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        SaveCurrentStateToHistory();

        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        List<StorageFile> files = new();
        foreach (BarcodeImageItem qrCodeItem in QrCodeBitmaps)
        {
            if (qrCodeItem.CodeAsBitmap is null)
                continue;

            string? imageNameFileName = $"{qrCodeItem.CodeAsText.ReplaceReservedCharacters()}.png";
            StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);
            bool success = await qrCodeItem.CodeAsBitmap.SavePngToStorageFile(file);

            if (!success)
                continue;

            files.Add(file);
        }

        if (files.Count == 0)
            return;

        DataPackage dataPackage = new();
        dataPackage.SetStorageItems(files);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });
    }

    [RelayCommand]
    private async Task CopySvgToClipboard()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        SaveCurrentStateToHistory();

        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        List<StorageFile> files = new();
        foreach (BarcodeImageItem qrCodeItem in QrCodeBitmaps)
        {
            if (qrCodeItem.CodeAsBitmap is null)
                continue;

            string? imageNameFileName = $"{qrCodeItem.CodeAsText.ReplaceReservedCharacters()}.svg";
            StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);

            bool success = await qrCodeItem.SaveCodeAsSvgFile(file, ForegroundColor.ToSystemDrawingColor(), BackgroundColor.ToSystemDrawingColor());
            if (!success)
                continue;

            files.Add(file);
        }

        if (files.Count == 0)
            return;

        DataPackage dataPackage = new();
        dataPackage.SetStorageItems(files);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });
    }

    [RelayCommand]
    private void ToggleHistoryPaneOpen()
    {
        IsHistoryPaneOpen = !IsHistoryPaneOpen;
    }

    [RelayCommand]
    private void OpenFile()
    {
        NavigationService.NavigateTo(typeof(DecodingViewModel).FullName!);
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

        SaveCurrentStateToHistory();

        if (QrCodeBitmaps.Count == 1)
        {
            await SaveSingle(FileKind.PNG, QrCodeBitmaps.First());
            return;
        }

        await SaveAllFiles(FileKind.PNG);
    }

    [RelayCommand]
    private void AddNewLine()
    {
        string stringToAdd = "https://";
        if (!string.IsNullOrWhiteSpace(BaseText))
            stringToAdd = BaseText;

        if (string.IsNullOrWhiteSpace(UrlText))
            UrlText = stringToAdd;

        UrlText += $"\r{stringToAdd}";
    }

    private async Task SaveSingle(FileKind kindOfFile, BarcodeImageItem imageItem)
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

    private async Task WriteImageToFile(BarcodeImageItem imageItem, StorageFile file, FileKind kindOfFile)
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
                    await imageItem.SaveCodeAsSvgFile(file, ForegroundColor.ToSystemDrawingColor(), BackgroundColor.ToSystemDrawingColor());
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

        SaveCurrentStateToHistory();

        if (QrCodeBitmaps.Count == 1)

        {
            await SaveSingle(FileKind.SVG, QrCodeBitmaps.First());
            return;
        }

        await SaveAllFiles(FileKind.SVG);
    }

    public async void OnNavigatedTo(object parameter)
    {
        await LoadHistory();
        MultiLineCodeMode = await LocalSettingsService.ReadSettingAsync<MultiLineCodeMode>(nameof(MultiLineCodeMode));
        BaseText = await LocalSettingsService.ReadSettingAsync<string>(nameof(BaseText)) ?? string.Empty;
        UrlText = BaseText;
        WarnWhenNotUrl = await LocalSettingsService.ReadSettingAsync<bool>(nameof(WarnWhenNotUrl));
    }

    public void OnNavigatedFrom()
    {
        if (!string.IsNullOrWhiteSpace(UrlText))
            SaveCurrentStateToHistory();
    }

    private void SaveCurrentStateToHistory()
    {
        HistoryItem historyItem = new()
        {
            CodesContent = UrlText,
            Foreground = ForegroundColor,
            Background = BackgroundColor,
            ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
        };

        if (HistoryItems.Contains(historyItem))
            HistoryItems.Remove(historyItem);
        HistoryItems.Insert(0, historyItem);

        LocalSettingsService.SaveSettingAsync(nameof(HistoryItems), HistoryItems);
    }

    private async Task LoadHistory()
    {
        ObservableCollection<HistoryItem>? prevHistory = null;

        try
        {
            prevHistory = await LocalSettingsService.ReadSettingAsync<ObservableCollection<HistoryItem>>(nameof(HistoryItems));
        }
        catch { }

        if (prevHistory is null || prevHistory.Count == 0)
            return;

        foreach (HistoryItem hisItem in prevHistory)
            HistoryItems.Add(hisItem);
    }
}
