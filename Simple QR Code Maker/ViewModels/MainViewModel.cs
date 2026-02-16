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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
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
    private string QuickSaveLocation = string.Empty;

    [ObservableProperty]
    private bool showSaveBothButton = false;

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
    private string savedFolderPath = string.Empty;

    [ObservableProperty]
    private bool copySharePopupOpen = false;

    [ObservableProperty]
    private System.Drawing.Bitmap? logoImage = null;

    [ObservableProperty]
    private bool hasLogo = false;

    [ObservableProperty]
    private double logoPaddingPixels = 4.0;

    [ObservableProperty]
    private double logoSizePercentage = 15;

    [ObservableProperty]
    private double logoSizeMaxPercentage = 20;

    private string? currentLogoPath = null;

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

        // Restore logo image and size if available
        if (!string.IsNullOrEmpty(value.LogoImagePath))
        {
            _ = LoadLogoFromHistory(value.LogoImagePath);
        }
        else
        {
            // Clear logo if history item has no logo
            LogoImage?.Dispose();
            LogoImage = null;
        }

        LogoSizePercentage = value.LogoSizePercentage;
        LogoPaddingPixels = value.LogoPaddingPixels;

        SelectedHistoryItem = null;
    }

    private async Task LoadLogoFromHistory(string logoPath)
    {
        try
        {
            if (File.Exists(logoPath))
            {
                // Dispose old logo first
                LogoImage?.Dispose();

                StorageFile file = await StorageFile.GetFileFromPathAsync(logoPath);
                using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
                LogoImage = new System.Drawing.Bitmap(stream.AsStreamForRead());
                currentLogoPath = logoPath;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load logo from history: {ex.Message}");
            LogoImage?.Dispose();
            LogoImage = null;
            currentLogoPath = null;
        }
    }

    public ObservableCollection<ErrorCorrectionOptions> ErrorCorrectionLevels { get; set; } = new(allCorrectionLevels);

    private static readonly List<ErrorCorrectionOptions> allCorrectionLevels =
    [
        new("Low 7%", ErrorCorrectionLevel.L),
        new("Medium 15%", ErrorCorrectionLevel.M),
        new("Quarter 25%", ErrorCorrectionLevel.Q),
        new("High 30%", ErrorCorrectionLevel.H),
    ];

    partial void OnSelectedOptionChanged(ErrorCorrectionOptions value)
    {
        // Ensure logo size doesn't exceed the new error correction level's maximum
        if (logoSizePercentage > logoSizeMaxPercentage)
        {
            logoSizePercentage = logoSizeMaxPercentage;
        }
        OnPropertyChanged(nameof(logoSizeMaxPercentage));

        LogoSizeMaxPercentage = BarcodeHelpers.GetMaxLogoSizePercentage(value.ErrorCorrectionLevel);

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

        if (HasLogo)
        {
            if (SelectedOption.ErrorCorrectionLevel == ErrorCorrectionLevel.L && ErrorCorrectionLevels.Count > 1)
            {
                SelectedOption = ErrorCorrectionLevels[1];
            }

            if (ErrorCorrectionLevels.Count > 0
                && ErrorCorrectionLevels[0].ErrorCorrectionLevel == ErrorCorrectionLevel.L)
                ErrorCorrectionLevels.RemoveAt(0);
        }
        else
        {
            if (ErrorCorrectionLevels.Count == 3)
                ErrorCorrectionLevels.Insert(0, new("Low 7%", ErrorCorrectionLevel.L));
        }

        debounceTimer.Stop();
        debounceTimer.Start();
    }

    partial void OnLogoSizePercentageChanged(double value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    partial void OnLogoPaddingPixelsChanged(double value)
    {
        debounceTimer.Stop();
        debounceTimer.Start();
    }

    public bool CanSaveImage => !string.IsNullOrWhiteSpace(UrlText);

    partial void OnUrlTextChanged(string value)
    {
        // Update max logo size when text changes (affects QR version/density)
        OnPropertyChanged(nameof(logoSizeMaxPercentage));

        // Ensure current logo size doesn't exceed the new maximum
        if (logoSizePercentage > logoSizeMaxPercentage)
        {
            logoSizePercentage = logoSizeMaxPercentage;
        }

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

    private async void OnSaveHistoryMessage(object recipient, SaveHistoryMessage message) => await SaveCurrentStateToHistory();

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

        placeholderTextTimer?.Tick -= PlaceholderTextTimer_Tick;

        debounceTimer?.Stop();
        debounceTimer?.Tick -= DebounceTimer_Tick;

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
                LogoSizePercentage,
                LogoPaddingPixels);
            BarcodeImageItem barcodeImageItem = new()
            {
                CodeAsBitmap = bitmap,
                CodeAsText = textToUse,
                IsAppShowingUrlWarnings = WarnWhenNotUrl,
                SizeTextVisible = (HideMinimumSizeText || LogoImage != null) ? Visibility.Collapsed : Visibility.Visible,
                ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
                ForegroundColor = ForegroundColor,
                BackgroundColor = BackgroundColor,
                MaxSizeScaleFactor = MinSizeScanDistanceScaleFactor,
                LogoImage = LogoImage,
                LogoSizePercentage = LogoSizePercentage,
                LogoPaddingPixels = LogoPaddingPixels,
            };

            double ratio = barcodeImageItem.ColorContrastRatio;
            Debug.WriteLine($"Contrast ratio: {ratio}");

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

        await SaveCurrentStateToHistory();

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

        await SaveCurrentStateToHistory();

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
    private async Task CopySvgTextToClipboard()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        await SaveCurrentStateToHistory();

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
        SavedFolderPath = string.Empty;
        ShowCodeInfoBar = false;
        CodeInfoBarSeverity = InfoBarSeverity.Informational;
        CodeInfoBarTitle = "Copy infoBar title";
    }

    [RelayCommand]
    private async Task OpenSavedFolder()
    {
        if (string.IsNullOrWhiteSpace(SavedFolderPath))
            return;

        await Windows.System.Launcher.LaunchFolderPathAsync(SavedFolderPath);
    }

    [RelayCommand]
    private void ToggleFaqPaneOpen() => IsFaqPaneOpen = !IsFaqPaneOpen;

    [RelayCommand]
    private void ToggleHistoryPaneOpen() => IsHistoryPaneOpen = !IsHistoryPaneOpen;

    [RelayCommand]
    private void ShareApp() => CopySharePopupOpen = !CopySharePopupOpen;

    [RelayCommand]
    private void OpenFile() => NavigationService.NavigateTo(typeof(DecodingViewModel).FullName!, CreateCurrentStateHistoryItem());

    [RelayCommand]
    private void GoToSettings() =>
        // pass the current state as a HistoryItem to the settings page
        // so when coming back it can be fully restored
        NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!, CreateCurrentStateHistoryItem());

    private HistoryItem CreateCurrentStateHistoryItem()
    {
        return new HistoryItem
        {
            CodesContent = UrlText,
            Foreground = ForegroundColor,
            Background = BackgroundColor,
            ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
            LogoImagePath = LogoImage != null ? GetCurrentLogoPath() : null,
            LogoSizePercentage = LogoSizePercentage,
            LogoPaddingPixels = LogoPaddingPixels,
        };
    }

    private string? GetCurrentLogoPath() => currentLogoPath;

    [RelayCommand]
    private async Task SavePng()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        await SaveCurrentStateToHistory();

        string? savedFolder = await SaveAllFiles(FileKind.PNG);

        if (savedFolder is null)
            return;

        ShowSaveSuccessInfoBar(
            QrCodeBitmaps.Count == 1 ? "PNG QR Code saved!" : $"{QrCodeBitmaps.Count} PNG QR Codes saved!",
            savedFolder);
    }

    [RelayCommand]
    private async Task SaveSvg()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        await SaveCurrentStateToHistory();

        string? savedFolder = await SaveAllFiles(FileKind.SVG);

        if (savedFolder is null)
            return;

        ShowSaveSuccessInfoBar(
            QrCodeBitmaps.Count == 1 ? "SVG QR Code saved!" : $"{QrCodeBitmaps.Count} SVG QR Codes saved!",
            savedFolder);
    }

    [RelayCommand]
    private async Task SavePngAsZip()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        await SaveCurrentStateToHistory();

        bool saved = await SaveAllFilesAsZip(FileKind.PNG);
        if (!saved)
            return;

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (QrCodeBitmaps.Count == 1)
            CodeInfoBarTitle = "PNG QR Code saved to zip!";
        else
            CodeInfoBarTitle = $"{QrCodeBitmaps.Count} PNG QR Codes saved to zip!";
    }

    [RelayCommand]
    private async Task SaveSvgAsZip()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        await SaveCurrentStateToHistory();

        bool saved = await SaveAllFilesAsZip(FileKind.SVG);
        if (!saved)
            return;

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (QrCodeBitmaps.Count == 1)
            CodeInfoBarTitle = "SVG QR Code saved to zip!";
        else
            CodeInfoBarTitle = $"{QrCodeBitmaps.Count} SVG QR Codes saved to zip!";
    }

    [RelayCommand]
    private async Task SaveBoth()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        await SaveCurrentStateToHistory();

        string? savedFolder = await SaveAllFiles(FileKind.PNG);

        if (savedFolder is null)
            return;

        await SaveAllFiles(FileKind.SVG, savedFolder);

        ShowSaveSuccessInfoBar(
            QrCodeBitmaps.Count == 1 ? "PNG and SVG QR Code saved!" : $"{QrCodeBitmaps.Count} PNG and SVG QR Codes saved!",
            savedFolder);
    }

    [RelayCommand]
    private async Task SaveBothAsZip()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        await SaveCurrentStateToHistory();

        bool saved = await SaveAllFilesAsZip(FileKind.PNG, FileKind.SVG);
        if (!saved)
            return;

        CodeInfoBarMessage = string.Empty;
        ShowCodeInfoBar = true;
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        if (QrCodeBitmaps.Count == 1)
            CodeInfoBarTitle = "PNG and SVG QR Code saved to zip!";
        else
            CodeInfoBarTitle = $"{QrCodeBitmaps.Count} PNG and SVG QR Codes saved to zip!";
    }

    [RelayCommand]
    private async Task CopyBothToClipboard()
    {
        if (QrCodeBitmaps.Count == 0)
            return;

        await SaveCurrentStateToHistory();

        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        List<StorageFile> files = [];
        foreach (BarcodeImageItem qrCodeItem in QrCodeBitmaps)
        {
            if (qrCodeItem.CodeAsBitmap is null)
                continue;

            string safeFileName = qrCodeItem.CodeAsText.ToSafeFileName();

            string pngFileName = $"{safeFileName}.png";
            StorageFile pngFile = await folder.CreateFileAsync(pngFileName, CreationCollisionOption.ReplaceExisting);
            bool pngSuccess = await qrCodeItem.CodeAsBitmap.SavePngToStorageFile(pngFile);
            if (pngSuccess)
                files.Add(pngFile);

            string svgFileName = $"{safeFileName}.svg";
            StorageFile svgFile = await folder.CreateFileAsync(svgFileName, CreationCollisionOption.ReplaceExisting);
            bool svgSuccess = await qrCodeItem.SaveCodeAsSvgFile(svgFile, ForegroundColor.ToSystemDrawingColor(), BackgroundColor.ToSystemDrawingColor(), SelectedOption.ErrorCorrectionLevel);
            if (svgSuccess)
                files.Add(svgFile);
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
        if (QrCodeBitmaps.Count == 1)
            CodeInfoBarTitle = "PNG and SVG QR Code copied to the clipboard";
        else
            CodeInfoBarTitle = $"{QrCodeBitmaps.Count} PNG and SVG QR Codes copied to the clipboard";

        copyInfoBarTimer.Start();
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

        if (file is null)
            return;

        try
        {
            // Dispose of the old logo if it exists
            LogoImage?.Dispose();

            // Use stream to access file instead of direct path for better compatibility
            using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
            LogoImage = new System.Drawing.Bitmap(stream.AsStreamForRead());

            // FIX: Store the selected file path so it can be saved to history
            currentLogoPath = file.Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load logo image: {ex.Message}");
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
        currentLogoPath = null;
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

    public async Task<string?> SaveAllFiles(FileKind kindOfFile, string? overrideFolderPath = null)
    {
        StorageFolder? folder = null;

        if (overrideFolderPath is not null)
        {
            folder = await StorageFolder.GetFolderFromPathAsync(overrideFolderPath);
        }
        else if (!string.IsNullOrWhiteSpace(QuickSaveLocation) && Directory.Exists(QuickSaveLocation))
        {
            folder = await StorageFolder.GetFolderFromPathAsync(QuickSaveLocation);
        }
        else
        {
            FolderPicker folderPicker = new()
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };

            Window saveWindow = new();
            IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
            InitializeWithWindow.Initialize(folderPicker, windowHandleSave);

            folder = await folderPicker.PickSingleFolderAsync();
        }

        if (folder is null)
            return null;

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

        return folder.Path;
    }

    private void ShowSaveSuccessInfoBar(string title, string folderPath)
    {
        CodeInfoBarSeverity = InfoBarSeverity.Success;
        CodeInfoBarTitle = title;
        CodeInfoBarMessage = $"Saved to {folderPath}";
        SavedFolderPath = folderPath;
        ShowCodeInfoBar = true;
        copyInfoBarTimer.Start();
    }

    public async Task<bool> SaveAllFilesAsZip(FileKind kindOfFile)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = $"QR Codes {DateTime.Now:yyyy-MM-dd}",
        };
        savePicker.FileTypeChoices.Add("ZIP Archive", [".zip"]);

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

        StorageFile zipFile = await savePicker.PickSaveFileAsync();

        if (zipFile is null)
            return false;

        string extension = $".{kindOfFile.ToString().ToLower()}";

        using MemoryStream zipStream = new();
        using (System.IO.Compression.ZipArchive archive = new(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            foreach (BarcodeImageItem imageItem in QrCodeBitmaps)
            {
                string fileName = imageItem.CodeAsText.ToSafeFileName();

                if (string.IsNullOrWhiteSpace(fileName) || imageItem.CodeAsBitmap is null)
                    continue;

                fileName += extension;

                // Write to a temp StorageFile, then copy bytes into the zip entry
                StorageFolder tempFolder = ApplicationData.Current.LocalCacheFolder;
                StorageFile tempFile = await tempFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                await WriteImageToFile(imageItem, tempFile, kindOfFile);

                System.IO.Compression.ZipArchiveEntry entry = archive.CreateEntry(fileName, System.IO.Compression.CompressionLevel.Optimal);
                using Stream entryStream = entry.Open();
                using IRandomAccessStreamWithContentType fileStream = await tempFile.OpenReadAsync();
                await fileStream.AsStreamForRead().CopyToAsync(entryStream);
            }
        }

        // Write the zip MemoryStream to the chosen StorageFile
        using IRandomAccessStream outputStream = await zipFile.OpenAsync(FileAccessMode.ReadWrite);
        outputStream.Size = 0;
        using Stream output = outputStream.AsStreamForWrite();
        zipStream.Position = 0;
        await zipStream.CopyToAsync(output);

        return true;
    }

    public async Task<bool> SaveAllFilesAsZip(params FileKind[] fileKinds)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = $"QR Codes {DateTime.Now:yyyy-MM-dd}",
        };
        savePicker.FileTypeChoices.Add("ZIP Archive", [".zip"]);

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

        StorageFile zipFile = await savePicker.PickSaveFileAsync();

        if (zipFile is null)
            return false;

        using MemoryStream zipStream = new();
        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, true))
        {
            foreach (BarcodeImageItem imageItem in QrCodeBitmaps)
            {
                string baseName = imageItem.CodeAsText.ToSafeFileName();

                if (string.IsNullOrWhiteSpace(baseName) || imageItem.CodeAsBitmap is null)
                    continue;

                foreach (FileKind kindOfFile in fileKinds)
                {
                    string extension = $".{kindOfFile.ToString().ToLower()}";
                    string fileName = baseName + extension;

                    StorageFolder tempFolder = ApplicationData.Current.LocalCacheFolder;
                    StorageFile tempFile = await tempFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    await WriteImageToFile(imageItem, tempFile, kindOfFile);

                    ZipArchiveEntry entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                    using Stream entryStream = entry.Open();
                    using IRandomAccessStreamWithContentType fileStream = await tempFile.OpenReadAsync();
                    await fileStream.AsStreamForRead().CopyToAsync(entryStream);
                }
            }
        }

        using IRandomAccessStream outputStream = await zipFile.OpenAsync(FileAccessMode.ReadWrite);
        outputStream.Size = 0;
        using Stream output = outputStream.AsStreamForWrite();
        zipStream.Position = 0;
        await zipStream.CopyToAsync(output);

        return true;
    }

    public async void OnNavigatedTo(object parameter)
    {
        await LoadHistory();

        // Force property change notification to refresh UI bindings after history loads
        OnPropertyChanged(nameof(HistoryItems));

        CheckCanPasteText();
        MultiLineCodeMode = await LocalSettingsService.ReadSettingAsync<MultiLineCodeMode>(nameof(MultiLineCodeMode));
        BaseText = await LocalSettingsService.ReadSettingAsync<string>(nameof(BaseText)) ?? string.Empty;
        UrlText = BaseText;
        WarnWhenNotUrl = await LocalSettingsService.ReadSettingAsync<bool>(nameof(WarnWhenNotUrl));
        HideMinimumSizeText = await LocalSettingsService.ReadSettingAsync<bool>(nameof(HideMinimumSizeText));
        ShowSaveBothButton = await LocalSettingsService.ReadSettingAsync<bool>(nameof(ShowSaveBothButton));
        QuickSaveLocation = await LocalSettingsService.ReadSettingAsync<string>(nameof(QuickSaveLocation)) ?? string.Empty;
        MinSizeScanDistanceScaleFactor = await LocalSettingsService.ReadSettingAsync<double>(nameof(MinSizeScanDistanceScaleFactor));
        if (MinSizeScanDistanceScaleFactor < 0.35)
        {
            MinSizeScanDistanceScaleFactor = 1;
            // reset to 1 if the value is too small, this can happen when settings are reset
            await LocalSettingsService.SaveSettingAsync(nameof(MinSizeScanDistanceScaleFactor), MinSizeScanDistanceScaleFactor);
        }

        // Check if parameter is a HistoryItem with full state restoration
        if (parameter is HistoryItem historyItem)
        {
            RestoreFromHistoryItem(historyItem);
        }
        // Otherwise check for text rehydration from other pages
        else if (parameter is string textParam && !string.IsNullOrWhiteSpace(textParam))
        {
            UrlText = textParam;
        }
    }

    private void RestoreFromHistoryItem(HistoryItem historyItem)
    {
        UrlText = historyItem.CodesContent;
        ForegroundColor = historyItem.Foreground;
        BackgroundColor = historyItem.Background;
        SelectedOption = ErrorCorrectionLevels.First(x => x.ErrorCorrectionLevel == historyItem.ErrorCorrection);

        // Restore logo image and settings if available
        if (!string.IsNullOrEmpty(historyItem.LogoImagePath))
        {
            _ = LoadLogoFromHistory(historyItem.LogoImagePath);
        }
        else
        {
            // Clear logo if history item has no logo
            LogoImage?.Dispose();
            LogoImage = null;
            currentLogoPath = null;
        }

        LogoSizePercentage = historyItem.LogoSizePercentage;
        LogoPaddingPixels = historyItem.LogoPaddingPixels;
    }

    public void OnNavigatedFrom()
    {
        if (!string.IsNullOrWhiteSpace(UrlText))
            _ = SaveCurrentStateToHistory();
    }

    public async Task SaveCurrentStateToHistory()
    {
        string? logoImagePath = null;

        // Save logo image to local app storage if present
        if (LogoImage is not null)
        {
            try
            {
                StorageFolder logoFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("LogoImages", CreationCollisionOption.OpenIfExists);
                string fileName = $"logo_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.png";
                StorageFile logoFile = await logoFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                using IRandomAccessStream stream = await logoFile.OpenAsync(FileAccessMode.ReadWrite);

                using IOutputStream outputStream = stream.GetOutputStreamAt(0);
                using DataWriter dataWriter = new(outputStream);
                using MemoryStream ms = new();
                LogoImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                byte[] bytes = ms.ToArray();
                dataWriter.WriteBytes(bytes);
                await dataWriter.StoreAsync();

                logoImagePath = logoFile.Path;
                currentLogoPath = logoImagePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save logo image: {ex.Message}");
            }
        }

        HistoryItem historyItem = new()
        {
            CodesContent = UrlText,
            Foreground = ForegroundColor,
            Background = BackgroundColor,
            ErrorCorrection = SelectedOption.ErrorCorrectionLevel,
            LogoImagePath = logoImagePath ?? currentLogoPath, // Use saved path or current path
            LogoSizePercentage = LogoSizePercentage,
            LogoPaddingPixels = LogoPaddingPixels,
        };

        HistoryItems.Remove(historyItem);
        HistoryItems.Insert(0, historyItem);

        await SaveHistoryToFile();
    }

    [RequiresUnreferencedCode("Calls HistoryStorageHelper.SaveHistoryAsync")]
    private async Task SaveHistoryToFile()
    {
        try
        {
            await HistoryStorageHelper.SaveHistoryAsync(HistoryItems);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to save history: {ex.Message}");
        }
    }

    [RequiresUnreferencedCode("Calls HistoryStorageHelper.LoadHistoryAsync")]
    private async Task LoadHistory()
    {
        ObservableCollection<HistoryItem> loadedHistory = await HistoryStorageHelper.LoadHistoryAsync();

        foreach (HistoryItem item in loadedHistory)
        {
            HistoryItems.Add(item);
        }
    }
}
