using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private ObservableCollection<BarcodeImageItem> qrCodeBitmaps = [];

    [ObservableProperty]
    private string placeholderText = "ex: http://www.example.com";

    private readonly string[] placeholdersList =
    [
        "http://example.com",
        "https://www.wikipedia.org",
        "https://www.JoeFinApps.com",
        "https://github.com",
        "https://github.com/TheJoeFin/Simple-QR-Code-Maker",
        "https://github.com/TheJoeFin/Text-Grab",
        "https://github.com/TheJoeFin/Simple-Icon-File-Maker"
    ];

    [ObservableProperty]
    private Windows.UI.Color backgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);

    [ObservableProperty]
    private Windows.UI.Color foregroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);

    [ObservableProperty]
    private ErrorCorrectionOptions selectedOption = new("Medium 15%", ErrorCorrectionLevel.M);

    [ObservableProperty]
    private bool isFaqPaneOpen = false;

    [ObservableProperty]
    private bool isHistoryPaneOpen = false;

    [ObservableProperty]
    private ObservableCollection<HistoryItem> historyItems = [];

    [ObservableProperty]
    private HistoryItem? selectedHistoryItem = null;

    private MultiLineCodeMode MultiLineCodeMode = MultiLineCodeMode.OneLineOneCode;
    private string BaseText = string.Empty;
    private bool WarnWhenNotUrl = true;
    private bool HideMinimumSizeText = false;

    [ObservableProperty]
    private bool canPasteText = false;

    [ObservableProperty]
    private bool showCodeInfoBar = false;

    [ObservableProperty]
    private InfoBarSeverity codeInfoBarSeverity = InfoBarSeverity.Success;

    [ObservableProperty]
    private string codeInfoBarTitle = "QR Code copied to clipboard";

    [ObservableProperty]
    private string codeInfoBarMessage = string.Empty;

    [ObservableProperty]
    private bool copySharePopupOpen = false;

    [ObservableProperty]
    private System.Drawing.Bitmap? logoImage = null;

    [ObservableProperty]
    private bool hasLogo = false;

    [ObservableProperty]
    private double logoSizePercentage = 20.0; // Default 20% of QR code size

    public double MaxLogoSizePercentage => GetMaxLogoSizeForErrorCorrection();

    private double MinSizeScanDistanceScaleFactor = 1;

    private readonly DispatcherTimer copyInfoBarTimer = new();

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

    public List<ErrorCorrectionOptions> ErrorCorrectionLevels { get; } =
    [
        new("Low 7%", ErrorCorrectionLevel.L),
        new("Medium 15%", ErrorCorrectionLevel.M),
        new("Quarter 25%", ErrorCorrectionLevel.Q),
        new("High 30%", ErrorCorrectionLevel.H),
    ];

    partial void OnSelectedOptionChanged(ErrorCorrectionOptions value)
    {
        // Ensure logo size doesn't exceed the new error correction level's maximum
        if (LogoSizePercentage > MaxLogoSizePercentage)
        {
            LogoSizePercentage = MaxLogoSizePercentage;
        }
        OnPropertyChanged(nameof(MaxLogoSizePercentage));
        
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

    partial void OnLogoImageChanged(System.Drawing.Bitmap? value)
    {
        HasLogo = value != null;
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    partial void OnLogoSizePercentageChanged(double value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private double GetMaxLogoSizeForErrorCorrection()
    {
        // Error correction allows us to obscure a percentage of the QR code
        // We use a conservative estimate (80% of the theoretical maximum)
        // to ensure reliable scanning
        return SelectedOption.ErrorCorrectionLevel switch
        {
            ErrorCorrectionLevel.L => 7.0 * 0.8,   // ~5.6% max
            ErrorCorrectionLevel.M => 15.0 * 0.8,  // ~12% max
            ErrorCorrectionLevel.Q => 25.0 * 0.8,  // ~20% max
            ErrorCorrectionLevel.H => 30.0 * 0.8,  // ~24% max
            _ => 20.0
        };
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

        copyInfoBarTimer.Interval = TimeSpan.FromSeconds(10);
        copyInfoBarTimer.Tick -= CopyInfoBarTimer_Tick;
        copyInfoBarTimer.Tick += CopyInfoBarTimer_Tick;

        placeholderTextTimer.Interval = TimeSpan.FromSeconds(6);
        placeholderTextTimer.Tick -= PlaceholderTextTimer_Tick;
        placeholderTextTimer.Tick += PlaceholderTextTimer_Tick;
        placeholderTextTimer.Start();

        NavigationService = navigationService;
        LocalSettingsService = localSettingsService;

        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        Clipboard.ContentChanged += Clipboard_ContentChanged;

        WeakReferenceMessenger.Default.Register<RequestShowMessage>(this, OnRequestShowMessage);
        WeakReferenceMessenger.Default.Register<SaveHistoryMessage>(this, OnSaveHistoryMessage);
        WeakReferenceMessenger.Default.Register<RequestPaneChange>(this, OnRequestPaneChange);
    }

    private void OnRequestPaneChange(object recipient, RequestPaneChange message)
    {
        switch (message.Pane)
        {
            case MainViewPanes.History:
                switch (message.RequestState)
                {
                    case PaneState.Open:
                        IsHistoryPaneOpen = true;
                        break;
                    case PaneState.Close:
                        IsFaqPaneOpen = false;
                        break;
                    default:
                        break;
                }
                break;
            case MainViewPanes.Faq:
                switch (message.RequestState)
                {
                    case PaneState.Open:
                        IsFaqPaneOpen = true;
                        break;
                    case PaneState.Close:
                        IsFaqPaneOpen = false;
                        break;
                    default:
                        break;
                }
                break;
            default:
                break;
        }
    }

    private void OnSaveHistoryMessage(object recipient, SaveHistoryMessage message)
    {
        SaveCurrentStateToHistory();
    }

    private void OnRequestShowMessage(object recipient, RequestShowMessage rsm)
    {
        CodeInfoBarMessage = rsm.Message;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = rsm.Severity;
        CodeInfoBarTitle = rsm.Title;

        if (rsm.Severity == InfoBarSeverity.Success)
        {
            copyInfoBarTimer.Start();
        }
    }

    private void Clipboard_ContentChanged(object? sender, object e) => CheckCanPasteText();

    private void CheckCanPasteText()
    {
        DataPackageView clipboardData = Clipboard.GetContent();

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
        
        // Dispose of the logo image
        LogoImage?.Dispose();
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
                BackgroundColor.ToSystemDrawingColor(),
                LogoImage,
                LogoSizePercentage);
            BarcodeImageItem barcodeImageItem = new()
            {
                CodeAsBitmap = bitmap,
                CodeAsText = textToUse,
                IsAppShowingUrlWarnings = WarnWhenNotUrl,
                SizeTextVisible = HideMinimumSizeText ? Visibility.Collapsed : Visibility.Visible,
                ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
                ForegroundColor = ForegroundColor,
                BackgroundColor = BackgroundColor,
                MaxSizeScaleFactor = MinSizeScanDistanceScaleFactor,
                LogoImage = LogoImage,
                LogoSizePercentage = LogoSizePercentage,
            };

            double ratio = barcodeImageItem.ColorContrastRatio;
            System.Diagnostics.Debug.WriteLine($"Contrast ratio: {ratio}");

            QrCodeBitmaps.Add(barcodeImageItem);
            ShowCodeInfoBar = false;
            CodeInfoBarSeverity = InfoBarSeverity.Informational;
        }
        catch (ZXing.WriterException)
        {
            ShowCodeInfoBar = true;
            CodeInfoBarSeverity = InfoBarSeverity.Error;
            CodeInfoBarTitle = "Error creating QR Code";
            CodeInfoBarMessage = "The text you entered is too long for a QR Code. Please try a shorter text.";
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
        DataPackageView clipboardContent = Clipboard.GetContent();
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
        List<StorageFile> files = [];
        foreach (BarcodeImageItem qrCodeItem in QrCodeBitmaps)
        {
            if (qrCodeItem.CodeAsBitmap is null)
                continue;

            string? imageNameFileName = $"{qrCodeItem.CodeAsText.ToSafeFileName()}.png";
            StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);
            bool success = await qrCodeItem.CodeAsBitmap.SavePngToStorageFile(file);

            if (!success)
                continue;

            files.Add(file);
        }

        if (files.Count == 0)
        {
            CodeInfoBarMessage = "No QR Codes to copy to the clipboard";
            ShowCodeInfoBar = true;
            CodeInfoBarSeverity = InfoBarSeverity.Error;
            CodeInfoBarTitle = "Failed to copy QR Codes to the clipboard";
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetStorageItems(files);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (files.Count == 1)
            CodeInfoBarTitle = "PNG QR Code copied to the clipboard";
        else
            CodeInfoBarTitle = $"{files.Count} PNG QR Codes copied to the clipboard";

        copyInfoBarTimer.Start();
    }

    [RelayCommand]
    private async Task CopySvgToClipboard()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        SaveCurrentStateToHistory();

        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        List<StorageFile> files = [];
        foreach (BarcodeImageItem qrCodeItem in QrCodeBitmaps)
        {
            if (qrCodeItem.CodeAsBitmap is null)
                continue;

            string? imageNameFileName = $"{qrCodeItem.CodeAsText.ToSafeFileName()}.svg";
            StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);

            bool success = await qrCodeItem.SaveCodeAsSvgFile(file, ForegroundColor.ToSystemDrawingColor(), BackgroundColor.ToSystemDrawingColor(), SelectedOption.ErrorCorrectionLevel);
            if (!success)
                continue;

            files.Add(file);
        }

        if (files.Count == 0)
        {
            CodeInfoBarMessage = "No QR Codes to copy to the clipboard";
            ShowCodeInfoBar = true;
            CodeInfoBarSeverity = InfoBarSeverity.Error;
            CodeInfoBarTitle = "Failed to copy QR Codes to the clipboard";
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetStorageItems(files);
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (files.Count == 1)
            CodeInfoBarTitle = "SVG QR Code copied to the clipboard";
        else
            CodeInfoBarTitle = $"{files.Count} SVG QR Codes copied to the clipboard";

        copyInfoBarTimer.Start();
    }

    [RelayCommand]
    private void CopySvgTextToClipboard()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        SaveCurrentStateToHistory();

        List<string> textStrings = [];
        foreach (BarcodeImageItem qrCodeItem in QrCodeBitmaps)
        {
            if (qrCodeItem.CodeAsBitmap is null)
                continue;

            string svgText = qrCodeItem.GetCodeAsSvgText(ForegroundColor.ToSystemDrawingColor(), BackgroundColor.ToSystemDrawingColor(), SelectedOption.ErrorCorrectionLevel);
            if (string.IsNullOrWhiteSpace(svgText))
                continue;

            textStrings.Add(svgText);
        }

        if (textStrings.Count == 0)
        {
            CodeInfoBarMessage = "No QR Codes to copy to the clipboard";
            ShowCodeInfoBar = true;
            CodeInfoBarSeverity = InfoBarSeverity.Error;
            CodeInfoBarTitle = "Failed to copy QR Codes to the clipboard";
            return;
        }

        DataPackage dataPackage = new();
        dataPackage.SetText(string.Join(Environment.NewLine, textStrings));
        Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions() { IsAllowedInHistory = true });

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (textStrings.Count == 1)
            CodeInfoBarTitle = "SVG QR Code copied to the clipboard";
        else
            CodeInfoBarTitle = $"{textStrings.Count} Text of SVGs QR Codes copied to the clipboard";

        copyInfoBarTimer.Start();
    }

    private void CopyInfoBarTimer_Tick(object? sender, object e)
    {
        copyInfoBarTimer.Stop();
        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = false;
        CodeInfoBarSeverity = InfoBarSeverity.Informational;
        CodeInfoBarTitle = "Copy infoBar title";
    }

    [RelayCommand]
    private void ToggleFaqPaneOpen()
    {
        IsFaqPaneOpen = !IsFaqPaneOpen;
    }

    [RelayCommand]
    private void ToggleHistoryPaneOpen()
    {
        IsHistoryPaneOpen = !IsHistoryPaneOpen;
    }

    [RelayCommand]
    private void ShareApp()
    {
        CopySharePopupOpen = !CopySharePopupOpen;
    }

    [RelayCommand]
    private void OpenFile()
    {
        NavigationService.NavigateTo(typeof(DecodingViewModel).FullName!, UrlText);
    }

    [RelayCommand]
    private void GoToSettings()
    {
        // pass the contents of the UrlText to the settings page
        // so when coming back it can be rehydrated
        NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!, UrlText);
    }

    [RelayCommand]
    private async Task SavePng()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        SaveCurrentStateToHistory();

        await SaveAllFiles(FileKind.PNG);

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (QrCodeBitmaps.Count == 1)
            CodeInfoBarTitle = "PNG QR Code saved!";
        else
            CodeInfoBarTitle = $"{QrCodeBitmaps.Count} PNG QR Codes saved!";
    }

    [RelayCommand]
    private async Task SaveSvg()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        SaveCurrentStateToHistory();

        await SaveAllFiles(FileKind.SVG);

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (QrCodeBitmaps.Count == 1)
            CodeInfoBarTitle = "SVG QR Code saved!";
        else
            CodeInfoBarTitle = $"{QrCodeBitmaps.Count} SVG QR Codes saved!";
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

    [RelayCommand]
    private async Task SelectLogo()
    {
        FileOpenPicker openPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        openPicker.FileTypeFilter.Add(".png");
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".bmp");
        openPicker.FileTypeFilter.Add(".gif");

        Window window = new();
        IntPtr windowHandle = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(openPicker, windowHandle);

        StorageFile file = await openPicker.PickSingleFileAsync();

        if (file == null)
            return;

        try
        {
            // Dispose of the old logo if it exists
            LogoImage?.Dispose();
            
            // Use stream to access file instead of direct path for better compatibility
            using var stream = await file.OpenReadAsync();
            LogoImage = new System.Drawing.Bitmap(stream.AsStreamForRead());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load logo image: {ex.Message}");
            CodeInfoBarMessage = "Failed to load the selected image";
            ShowCodeInfoBar = true;
            CodeInfoBarSeverity = InfoBarSeverity.Error;
            CodeInfoBarTitle = "Error loading logo";
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        LogoImage?.Dispose();
        LogoImage = null;
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
                await imageItem.SaveCodeAsSvgFile(file,
                    ForegroundColor.ToSystemDrawingColor(),
                    BackgroundColor.ToSystemDrawingColor(),
                    SelectedOption.ErrorCorrectionLevel);
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
            string fileName = imageItem.CodeAsText.ToSafeFileName();

            if (string.IsNullOrWhiteSpace(fileName) || imageItem.CodeAsBitmap is null)
                continue;

            fileName += extension;

            StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            await WriteImageToFile(imageItem, file, kindOfFile);
        }
    }

    public async void OnNavigatedTo(object parameter)
    {
        await LoadHistory();
        MultiLineCodeMode = await LocalSettingsService.ReadSettingAsync<MultiLineCodeMode>(nameof(MultiLineCodeMode));
        BaseText = await LocalSettingsService.ReadSettingAsync<string>(nameof(BaseText)) ?? string.Empty;
        UrlText = BaseText;
        WarnWhenNotUrl = await LocalSettingsService.ReadSettingAsync<bool>(nameof(WarnWhenNotUrl));
        HideMinimumSizeText = await LocalSettingsService.ReadSettingAsync<bool>(nameof(HideMinimumSizeText));
        MinSizeScanDistanceScaleFactor = await LocalSettingsService.ReadSettingAsync<double>(nameof(MinSizeScanDistanceScaleFactor));
        if (MinSizeScanDistanceScaleFactor < 0.35)
        {
            MinSizeScanDistanceScaleFactor = 1;
            // reset to 1 if the value is too small, this can happen when settings are reset
            await LocalSettingsService.SaveSettingAsync(nameof(MinSizeScanDistanceScaleFactor), MinSizeScanDistanceScaleFactor);
        }

        // check on text rehydration, could be coming from Reading or Settings
        if (parameter is string textParam && !string.IsNullOrWhiteSpace(textParam))
            UrlText = textParam;
    }

    public void OnNavigatedFrom()
    {
        if (!string.IsNullOrWhiteSpace(UrlText))
            SaveCurrentStateToHistory();
    }

    public void SaveCurrentStateToHistory()
    {
        HistoryItem historyItem = new()
        {
            CodesContent = UrlText,
            Foreground = ForegroundColor,
            Background = BackgroundColor,
            ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
        };

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
