using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class SettingsViewModel : ObservableRecipient, INavigationAware, ITitleBarBackNavigation
{
    private readonly IThemeSelectorService _themeSelectorService;

    [ObservableProperty]
    public partial ElementTheme ElementTheme { get; set; }

    [ObservableProperty]
    public partial string VersionDescription { get; set; }

    [ObservableProperty]
    public partial LaunchMode LaunchMode { get; set; } = LaunchMode.CreatingQrCodes;

    [ObservableProperty]
    public partial MultiLineCodeMode MultiLineCodeMode { get; set; } = MultiLineCodeMode.OneLineOneCode;

    [ObservableProperty]
    public partial string BaseText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool WarnWhenNotUrl { get; set; } = true;

    [ObservableProperty]
    public partial bool WarnWhenLikelyRedirector { get; set; } = RedirectorWarningSettingsHelper.DefaultWarnWhenLikelyRedirector;

    [ObservableProperty]
    public partial bool HideMinimumSizeText { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowSaveBothButton { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowPrintButton { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowZipSaveOptions { get; set; } = true;

    [ObservableProperty]
    public partial bool UseAutoBrands { get; set; } = false;

    [ObservableProperty]
    public partial double MinSizeScanDistanceScaleFactor { get; set; } = 1.0;

    [ObservableProperty]
    public partial string MaxScanDistanceText { get; set; } = "36in or 1m";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QrPaddingDescription))]
    [NotifyPropertyChangedFor(nameof(QrPaddingSettingsDescription))]
    public partial double QrPaddingModules { get; set; } = 2.0;

    [ObservableProperty]
    public partial string QuickSaveLocation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasQuickSaveLocation { get; set; } = false;

    [ObservableProperty]
    public partial bool ExportIncludeSettings { get; set; } = true;

    [ObservableProperty]
    public partial bool ExportIncludeBrands { get; set; } = true;

    [ObservableProperty]
    public partial bool ExportIncludeHistory { get; set; } = true;

    [ObservableProperty]
    public partial string ImportExportStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity ImportExportStatusSeverity { get; set; } = InfoBarSeverity.Success;

    public ObservableCollection<string> SafeRedirectorDomains { get; } = [];

    public bool HasImportExportStatus => !string.IsNullOrEmpty(ImportExportStatusMessage);

    public bool HasSafeRedirectorDomains => SafeRedirectorDomains.Count > 0;

    public string SafeRedirectorDomainsEmptyStateText => HasSafeRedirectorDomains
        ? string.Empty
        : "No safe domains yet. Use the redirector warning info bar to mark one as safe.";

    partial void OnImportExportStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasImportExportStatus));
    }

    private readonly DispatcherTimer importExportStatusTimer = new();

    private readonly DispatcherTimer settingChangedDebounceTimer = new();

    private bool _isLoading;

    private HistoryItem? navigationHistoryItem = null;
    private NavigationRestoreState? backNavigationState = null;

    private INavigationService NavigationService { get; }
    public ILocalSettingsService LocalSettingsService { get; }

    public ICommand SwitchThemeCommand { get; }

    public bool CanUseTitleBarBack => true;

    [RelayCommand]
    private async Task SwitchLaunchMode(object param)
    {
        if (param is not string stringMode)
            return;

        bool parsed = Enum.TryParse(stringMode, out LaunchMode mode);

        if (!parsed)
            return;

        LaunchMode = mode;
        await LocalSettingsService.SaveSettingAsync(nameof(LaunchMode), LaunchMode);
    }

    [RelayCommand]
    private async Task SwitchMultiLineMode(object param)
    {
        if (param is not string stringMode)
            return;

        bool parsed = Enum.TryParse(stringMode, out MultiLineCodeMode mode);

        if (!parsed)
            return;

        MultiLineCodeMode = mode;
        await LocalSettingsService.SaveSettingAsync(nameof(MultiLineCodeMode), MultiLineCodeMode);
    }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, INavigationService navigationService, ILocalSettingsService localSettingsService)
    {
        NavigationService = navigationService;
        LocalSettingsService = localSettingsService;
        _themeSelectorService = themeSelectorService;
        ElementTheme = _themeSelectorService.Theme;
        VersionDescription = GetVersionDescription();

        settingChangedDebounceTimer.Interval = TimeSpan.FromMilliseconds(500);
        settingChangedDebounceTimer.Tick -= SettingChangedDebounceTimer_Tick;
        settingChangedDebounceTimer.Tick += SettingChangedDebounceTimer_Tick;

        importExportStatusTimer.Interval = TimeSpan.FromSeconds(6);
        importExportStatusTimer.Tick += (s, e) =>
        {
            importExportStatusTimer.Stop();
            ImportExportStatusMessage = string.Empty;
        };

        SafeRedirectorDomains.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasSafeRedirectorDomains));
            OnPropertyChanged(nameof(SafeRedirectorDomainsEmptyStateText));
        };

        SwitchThemeCommand = new RelayCommand<ElementTheme>(
            async (param) =>
            {
                if (ElementTheme != param)
                {
                    ElementTheme = param;
                    await _themeSelectorService.SetThemeAsync(param);
                }
            });
    }

    private async void SettingChangedDebounceTimer_Tick(object? sender, object e)
    {
        settingChangedDebounceTimer.Stop();
        await SaveAllSettingsAsync();
    }

    private async Task SaveAllSettingsAsync()
    {
        Trace.WriteLine("[SettingsVM] SaveAllSettingsAsync started");
        await SaveSingleSettingAsync(nameof(BaseText), BaseText);
        await SaveSingleSettingAsync(nameof(WarnWhenNotUrl), WarnWhenNotUrl);
        await RedirectorWarningSettingsHelper.SaveWarningEnabledAsync(LocalSettingsService, WarnWhenLikelyRedirector);
        await SaveSingleSettingAsync(nameof(HideMinimumSizeText), HideMinimumSizeText);
        await SaveSingleSettingAsync(nameof(ShowSaveBothButton), ShowSaveBothButton);
        await SaveSingleSettingAsync(nameof(ShowPrintButton), ShowPrintButton);
        await SaveSingleSettingAsync(nameof(ShowZipSaveOptions), ShowZipSaveOptions);
        await SaveSingleSettingAsync(nameof(UseAutoBrands), UseAutoBrands);
        await SaveSingleSettingAsync(nameof(MinSizeScanDistanceScaleFactor), MinSizeScanDistanceScaleFactor);
        await SaveSingleSettingAsync(nameof(QrPaddingModules), QrPaddingModules);
        await SaveSingleSettingAsync(nameof(QuickSaveLocation), QuickSaveLocation);
        await RedirectorWarningSettingsHelper.SaveSafeDomainsAsync(LocalSettingsService, SafeRedirectorDomains);
        Trace.WriteLine("[SettingsVM] SaveAllSettingsAsync completed");
    }

    private async Task SaveSingleSettingAsync<T>(string key, T value)
    {
        try
        {
            Trace.WriteLine($"[SettingsVM] Saving '{key}'");
            await LocalSettingsService.SaveSettingAsync(key, value);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[SettingsVM] Failed to save setting '{key}': {ex.Message}");
        }
    }

    private void RestartDebounceTimer()
    {
        if (_isLoading)
            return;

        settingChangedDebounceTimer.Stop();
        settingChangedDebounceTimer.Start();
    }

    partial void OnBaseTextChanged(string value)
    {
        RestartDebounceTimer();
    }

    partial void OnWarnWhenNotUrlChanged(bool value)
    {
        RestartDebounceTimer();
    }

    partial void OnWarnWhenLikelyRedirectorChanged(bool value)
    {
        RestartDebounceTimer();
    }

    partial void OnHideMinimumSizeTextChanged(bool value)
    {
        RestartDebounceTimer();
    }

    partial void OnShowSaveBothButtonChanged(bool value)
    {
        RestartDebounceTimer();
    }

    partial void OnShowPrintButtonChanged(bool value)
    {
        RestartDebounceTimer();
    }

    partial void OnShowZipSaveOptionsChanged(bool value)
    {
        RestartDebounceTimer();
    }

    partial void OnUseAutoBrandsChanged(bool value)
    {
        RestartDebounceTimer();
    }

    partial void OnQuickSaveLocationChanged(string value)
    {
        HasQuickSaveLocation = !string.IsNullOrWhiteSpace(value);
        RestartDebounceTimer();
    }

    partial void OnMinSizeScanDistanceScaleFactorChanged(double value)
    {
        RestartDebounceTimer();

        bool isMetric = RegionInfo.CurrentRegion.IsMetric;

        if (isMetric)
        {
            if (value == 1)
                MaxScanDistanceText = $"{value} meter";
            else
                MaxScanDistanceText = $"{value} meters";
        }
        else
        {
            if (value > 1)
            {
                MaxScanDistanceText = $"{Math.Round(value * 3, 1)} feet";
            }
            else
            {
                MaxScanDistanceText = $"{Math.Round(value * 36, 0)} inches";
            }
        }
    }

    public string QrPaddingDescription => $"{QrPaddingModules:0} modules";

    public string QrPaddingSettingsDescription => BarcodeHelpers.IsSizeRecommendationAvailableForPadding(QrPaddingModules)
        ? $"{QrPaddingDescription}. Print sizing stays reliable at this setting."
        : $"{QrPaddingDescription}. Print sizing is disabled outside 1-4 modules.";

    partial void OnQrPaddingModulesChanged(double value)
    {
        RestartDebounceTimer();
    }

    [RelayCommand]
    private void GoHome()
    {
        if (NavigationService.CanGoBack)
        {
            NavigationService.GoBack();
            return;
        }

        if (backNavigationState is not null)
        {
            NavigationService.NavigateTo(backNavigationState.PageKey, backNavigationState.Parameter);
            return;
        }

        object? parameter = backNavigationState is null
            ? navigationHistoryItem
            : new MainNavigationParameter
            {
                Parameter = navigationHistoryItem,
                BackNavigationState = backNavigationState,
            };
        NavigationService.NavigateTo(typeof(MainViewModel).FullName!, parameter);
    }

    public void NavigateBack() => GoHome();

    [RelayCommand]
    private async Task BrowseQuickSaveLocation()
    {
        FolderPicker folderPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        Window window = new();
        IntPtr windowHandle = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(folderPicker, windowHandle);

        StorageFolder folder = await folderPicker.PickSingleFolderAsync();

        if (folder is not null)
        {
            QuickSaveLocation = folder.Path;
        }
    }

    [RelayCommand]
    private void ClearQuickSaveLocation()
    {
        QuickSaveLocation = string.Empty;
    }

    [RelayCommand]
    private async Task RemoveSafeRedirectorDomain(string? domain)
    {
        string normalizedDomain = RedirectorWarningHelper.NormalizeHost(domain);
        if (normalizedDomain.Length == 0)
            return;

        for (int index = SafeRedirectorDomains.Count - 1; index >= 0; index--)
        {
            if (string.Equals(SafeRedirectorDomains[index], normalizedDomain, StringComparison.OrdinalIgnoreCase))
                SafeRedirectorDomains.RemoveAt(index);
        }

        await RedirectorWarningSettingsHelper.SaveSafeDomainsAsync(LocalSettingsService, SafeRedirectorDomains);
    }

    [RelayCommand]
    private async Task ClearSafeRedirectorDomains()
    {
        if (!HasSafeRedirectorDomains)
            return;

        SafeRedirectorDomains.Clear();
        await RedirectorWarningSettingsHelper.SaveSafeDomainsAsync(LocalSettingsService, SafeRedirectorDomains);
    }

    [RelayCommand]
    private static async Task ReviewApp()
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9NCH56G3RQFC"));
    }

    [RelayCommand]
    private static async Task OpenSimpleIconFileMaker()
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9NS1BM1FB99Z"));
    }

    [RelayCommand]
    private static async Task OpenTextGrab()
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?ProductId=9MZNKQJ7SL0B"));
    }

    private static string GetVersionDescription()
    {
        Version version;

        if (RuntimeHelper.IsMSIX)
        {
            PackageVersion packageVersion = Package.Current.Id.Version;

            version = new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }
        else
        {
            version = Assembly.GetExecutingAssembly().GetName().Version!;
        }

        return $"{"AppDisplayName".GetLocalized()} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    [RelayCommand]
    private void GoToMoreInfo()
    {
        NavigationService.NavigateTo(typeof(AboutQrCodesWebViewModel).FullName!);
    }

    [RelayCommand]
    [RequiresUnreferencedCode("Loads history and brand items for export")]
    private async Task ExportSettings()
    {
        if (!ExportIncludeSettings && !ExportIncludeBrands && !ExportIncludeHistory)
        {
            ShowStatus("Select at least one item to export.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            FileSavePicker picker = new()
            {
                SuggestedFileName = $"SimpleQRBackup_{DateTime.Now:yyyyMMdd_HHmmss}",
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeChoices.Add("Zip Archive", [".zip"]);

            IntPtr windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, windowHandle);

            StorageFile? zipFile = await picker.PickSaveFileAsync();
            if (zipFile is null)
                return;

            // imagePathMap: original absolute path → relative path inside the ZIP.
            Dictionary<string, string> imagePathMap = [];

            using MemoryStream zipStream = new();
            using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                if (ExportIncludeHistory)
                {
                    ObservableCollection<HistoryItem> historyItems = await HistoryStorageHelper.LoadHistoryAsync();
                    foreach (HistoryItem item in historyItems)
                    {
                        item.LogoImagePath = await AddImageToArchiveAsync(
                            archive,
                            item.LogoImagePath,
                            imagePathMap,
                            "history",
                            item.CodesContent,
                            item.SavedDateTime);
                    }

                    string historyJson = JsonSerializer.Serialize(historyItems, HistoryJsonSerializerOptions.Options);
                    ZipArchiveEntry historyEntry = archive.CreateEntry("History.json", CompressionLevel.Fastest);
                    using StreamWriter historyWriter = new(historyEntry.Open(), leaveOpen: false);
                    await historyWriter.WriteAsync(historyJson);
                }

                if (ExportIncludeBrands)
                {
                    ObservableCollection<BrandItem> brandItems = await BrandStorageHelper.LoadBrandsAsync();
                    foreach (BrandItem brand in brandItems)
                    {
                        brand.LogoImagePath = await AddImageToArchiveAsync(
                            archive,
                            brand.LogoImagePath,
                            imagePathMap,
                            "brand",
                            brand.Name,
                            brand.CreatedDateTime);
                    }

                    string brandsJson = JsonSerializer.Serialize(brandItems, BrandJsonSerializerOptions.Options);
                    ZipArchiveEntry brandsEntry = archive.CreateEntry("Brands.json", CompressionLevel.Fastest);
                    using StreamWriter brandsWriter = new(brandsEntry.Open(), leaveOpen: false);
                    await brandsWriter.WriteAsync(brandsJson);
                }

                if (ExportIncludeSettings)
                {
                    Dictionary<string, string> settingsDict = [];
                    foreach (KeyValuePair<string, object> kvp in ApplicationData.Current.LocalSettings.Values)
                    {
                        if (kvp.Value is string strVal)
                            settingsDict[kvp.Key] = strVal;
                    }

                    JsonSerializerOptions options = new() { WriteIndented = true };
                    string settingsJson = JsonSerializer.Serialize(settingsDict, options);
                    ZipArchiveEntry settingsEntry = archive.CreateEntry("LocalSettings.json", CompressionLevel.Fastest);
                    using StreamWriter settingsWriter = new(settingsEntry.Open(), leaveOpen: false);
                    await settingsWriter.WriteAsync(settingsJson);
                }
            }

            using IRandomAccessStream outputStream = await zipFile.OpenAsync(FileAccessMode.ReadWrite);
            outputStream.Size = 0;
            using Stream fileStream = outputStream.AsStreamForWrite();
            zipStream.Position = 0;
            await zipStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();

            ShowStatus("Export complete.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Export failed: {ex.Message}");
            ShowStatus($"Export failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    [RelayCommand]
    [RequiresUnreferencedCode("Deserializes history and brand items during import")]
    private async Task ImportSettings()
    {
        try
        {
            FileOpenPicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add(".zip");

            IntPtr windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, windowHandle);

            StorageFile? zipFile = await picker.PickSingleFileAsync();
            if (zipFile is null)
                return;

            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            using Stream zipStream = await zipFile.OpenStreamForReadAsync();
            using ZipArchive archive = new(zipStream, ZipArchiveMode.Read);

            // Extract images first so paths can be resolved when processing JSON.
            // zipRelPath (e.g. "images/history_20260413_170000_example_logo_001.png")
            // maps to a unique absolute local path for this import session.
            Dictionary<string, string> importedImagePaths = await ExtractImagesToLocalFolderAsync(archive, localFolder);

            ZipArchiveEntry? historyEntry = archive.GetEntry("History.json");
            if (historyEntry is not null)
            {
                using StreamReader reader = new(historyEntry.Open());
                string historyJson = await reader.ReadToEndAsync();

                ObservableCollection<HistoryItem>? historyItems = JsonSerializer.Deserialize<ObservableCollection<HistoryItem>>(
                    historyJson, HistoryJsonSerializerOptions.Options);

                if (historyItems is not null)
                {
                    foreach (HistoryItem item in historyItems)
                        item.LogoImagePath = ResolveImportedImagePath(item.LogoImagePath, importedImagePaths);

                    ObservableCollection<HistoryItem> existingHistory = await HistoryStorageHelper.LoadHistoryAsync();
                    ObservableCollection<HistoryItem> mergedHistory = MergeImportedHistory(existingHistory, historyItems);
                    await HistoryStorageHelper.SaveHistoryAsync(mergedHistory);
                }
            }

            ZipArchiveEntry? brandsEntry = archive.GetEntry("Brands.json");
            if (brandsEntry is not null)
            {
                using StreamReader reader = new(brandsEntry.Open());
                string brandsJson = await reader.ReadToEndAsync();

                ObservableCollection<BrandItem>? brandItems = JsonSerializer.Deserialize<ObservableCollection<BrandItem>>(
                    brandsJson, BrandJsonSerializerOptions.Options);

                if (brandItems is not null)
                {
                    foreach (BrandItem brand in brandItems)
                        brand.LogoImagePath = ResolveImportedImagePath(brand.LogoImagePath, importedImagePaths);

                    ObservableCollection<BrandItem> existingBrands = await BrandStorageHelper.LoadBrandsAsync();
                    ObservableCollection<BrandItem> mergedBrands = MergeImportedBrands(existingBrands, brandItems);
                    await BrandStorageHelper.SaveBrandsAsync(mergedBrands);
                }
            }

            ZipArchiveEntry? settingsEntry = archive.GetEntry("LocalSettings.json");
            if (settingsEntry is not null)
            {
                using StreamReader reader = new(settingsEntry.Open());
                string settingsJson = await reader.ReadToEndAsync();
                Dictionary<string, string>? settingsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson);
                if (settingsDict is not null)
                {
                    foreach (KeyValuePair<string, string> kvp in settingsDict)
                        Windows.Storage.ApplicationData.Current.LocalSettings.Values[kvp.Key] = kvp.Value;
                }
            }

            await ReloadAllSettingsAsync();
            ShowStatus("Import complete. Return to the main screen to reload history and brands.", InfoBarSeverity.Success);
        }
        catch (InvalidDataException ex)
        {
            Debug.WriteLine($"Import failed: Invalid backup archive. {ex.Message}");
            ShowStatus("Import failed: The selected file is not a valid backup ZIP.", InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Import failed: {ex.Message}");
            ShowStatus($"Import failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    /// <summary>
    /// Adds an image file to the ZIP archive (deduplicating by original path).
    /// Returns the relative ZIP path, or null if the image path is null/missing.
    /// </summary>
    private static async Task<string?> AddImageToArchiveAsync(
        ZipArchive archive,
        string? originalPath,
        Dictionary<string, string> imagePathMap,
        string category,
        string nameHint,
        DateTime timestamp)
    {
        if (string.IsNullOrEmpty(originalPath))
            return null;

        if (imagePathMap.TryGetValue(originalPath, out string? existingZipPath))
            return existingZipPath;

        if (!File.Exists(originalPath))
            return null;

        string zipRelPath = CreateArchiveImagePath(
            category,
            nameHint,
            originalPath,
            timestamp,
            imagePathMap.Count + 1);

        imagePathMap[originalPath] = zipRelPath;

        ZipArchiveEntry imgEntry = archive.CreateEntry(zipRelPath, CompressionLevel.NoCompression);
        using Stream imgEntryStream = imgEntry.Open();
        using FileStream imgFileStream = File.OpenRead(originalPath);
        await imgFileStream.CopyToAsync(imgEntryStream);

        return zipRelPath;
    }

    /// <summary>
    /// Extracts all images/* entries from the archive to LocalFolder/ImportedImages/.
    /// Returns a mapping from ZIP relative path to absolute local path.
    /// </summary>
    private static async Task<Dictionary<string, string>> ExtractImagesToLocalFolderAsync(
        ZipArchive archive, StorageFolder localFolder)
    {
        Dictionary<string, string> importedPaths = [];
        string importedImagesDir = Path.Combine(localFolder.Path, "ImportedImages", CreateImportSessionFolderName());
        Directory.CreateDirectory(importedImagesDir);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith("images/", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Name))
                continue;

            string localPath = CreateUniqueImportedImagePath(importedImagesDir, entry.Name);
            using Stream entryStream = entry.Open();
            using FileStream fileStream = new(localPath, FileMode.Create, FileAccess.Write);
            await entryStream.CopyToAsync(fileStream);

            importedPaths[entry.FullName] = localPath;
        }

        return importedPaths;
    }

    /// <summary>
    /// Maps a ZIP-relative image path to its extracted absolute local path.
    /// Returns null if the path was a ZIP reference that wasn't found in the import.
    /// Leaves non-relative paths (i.e. paths from old backups without images) as-is.
    /// </summary>
    private static string? ResolveImportedImagePath(string? logoPath, Dictionary<string, string> importedImagePaths)
    {
        if (string.IsNullOrEmpty(logoPath))
            return null;

        if (logoPath.StartsWith("images/", StringComparison.Ordinal))
            return importedImagePaths.TryGetValue(logoPath, out string? local) ? local : null;

        return logoPath;
    }

    private static ObservableCollection<HistoryItem> MergeImportedHistory(
        ObservableCollection<HistoryItem> existingHistory,
        ObservableCollection<HistoryItem> importedHistory)
    {
        ObservableCollection<HistoryItem> mergedHistory = new(existingHistory);

        for (int index = importedHistory.Count - 1; index >= 0; index--)
        {
            HistoryItem importedItem = importedHistory[index];
            mergedHistory.Remove(importedItem);
            mergedHistory.Insert(0, importedItem);
        }

        return mergedHistory;
    }

    private static ObservableCollection<BrandItem> MergeImportedBrands(
        ObservableCollection<BrandItem> existingBrands,
        ObservableCollection<BrandItem> importedBrands)
    {
        ObservableCollection<BrandItem> mergedBrands = new(existingBrands);
        BrandItem? preferredDefault = null;

        for (int index = importedBrands.Count - 1; index >= 0; index--)
        {
            BrandItem importedBrand = importedBrands[index];
            if (importedBrand.IsDefault)
                preferredDefault = importedBrand;

            mergedBrands.Remove(importedBrand);
            mergedBrands.Insert(0, importedBrand);
        }

        NormalizeDefaultBrand(mergedBrands, preferredDefault);
        return mergedBrands;
    }

    private static void NormalizeDefaultBrand(ObservableCollection<BrandItem> brands, BrandItem? preferredDefault)
    {
        BrandItem? resolvedDefault = preferredDefault;
        if (resolvedDefault is null)
        {
            foreach (BrandItem brand in brands)
            {
                if (brand.IsDefault)
                {
                    resolvedDefault = brand;
                    break;
                }
            }
        }

        bool defaultAssigned = false;
        foreach (BrandItem brand in brands)
        {
            bool shouldBeDefault = resolvedDefault is not null && !defaultAssigned && brand.Equals(resolvedDefault);
            brand.IsDefault = shouldBeDefault;
            defaultAssigned |= shouldBeDefault;
        }
    }

    private static string CreateArchiveImagePath(
        string category,
        string nameHint,
        string originalPath,
        DateTime timestamp,
        int imageNumber)
    {
        string extension = Path.GetExtension(originalPath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".img";

        string categorySegment = SanitizeFileNameSegment(category, 16);
        string timestampSegment = (timestamp == default ? DateTime.Now : timestamp).ToString("yyyyMMdd_HHmmss");
        string hintSegment = SanitizeFileNameSegment(nameHint, 36);
        string originalNameSegment = SanitizeFileNameSegment(Path.GetFileNameWithoutExtension(originalPath), 24);

        List<string> parts =
        [
            string.IsNullOrWhiteSpace(categorySegment) ? "image" : categorySegment,
            timestampSegment
        ];

        if (!string.IsNullOrWhiteSpace(hintSegment))
            parts.Add(hintSegment);

        if (!string.IsNullOrWhiteSpace(originalNameSegment) &&
            !string.Equals(originalNameSegment, hintSegment, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(originalNameSegment);
        }

        parts.Add($"logo_{imageNumber:D3}");

        return $"images/{string.Join("_", parts)}{extension}";
    }

    private static string CreateImportSessionFolderName()
    {
        return $"import_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
    }

    private static string CreateUniqueImportedImagePath(string importedImagesDir, string entryName)
    {
        string baseName = SanitizeFileNameSegment(Path.GetFileNameWithoutExtension(entryName), 96);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "imported_logo";

        string extension = Path.GetExtension(entryName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".img";

        string localPath = Path.Combine(importedImagesDir, $"{baseName}{extension}");
        int duplicateIndex = 1;

        while (File.Exists(localPath))
        {
            localPath = Path.Combine(importedImagesDir, $"{baseName}_{duplicateIndex:D2}{extension}");
            duplicateIndex++;
        }

        return localPath;
    }

    private static string SanitizeFileNameSegment(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder builder = new(maxLength);
        bool lastWasSeparator = false;

        foreach (char ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (builder.Length >= maxLength)
                    break;

                builder.Append(char.ToLowerInvariant(ch));
                lastWasSeparator = false;
                continue;
            }

            if (builder.Length == 0 || lastWasSeparator || builder.Length >= maxLength)
                continue;

            builder.Append('_');
            lastWasSeparator = true;
        }

        return builder.ToString().Trim('_');
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        importExportStatusTimer.Stop();
        ImportExportStatusSeverity = severity;
        ImportExportStatusMessage = message;
        importExportStatusTimer.Start();
    }

    public async void OnNavigatedTo(object parameter)
    {
        Trace.WriteLine($"[SettingsVM] OnNavigatedTo started (parameter: {parameter?.GetType().Name ?? "null"})");
        await ReloadAllSettingsAsync();

        backNavigationState = null;
        navigationHistoryItem = null;

        object? effectiveParameter = parameter;
        if (parameter is MainNavigationParameter mainNavigationParameter)
        {
            backNavigationState = mainNavigationParameter.BackNavigationState;
            effectiveParameter = mainNavigationParameter.Parameter;
        }

        // Store the HistoryItem to pass back when returning to main page
        if (effectiveParameter is HistoryItem historyItem)
        {
            navigationHistoryItem = historyItem;
        }
        // For backward compatibility, also handle string parameter
        else if (effectiveParameter is string urlText && !string.IsNullOrWhiteSpace(urlText))
        {
            navigationHistoryItem = new HistoryItem { CodesContent = urlText };
        }
    }

    private async Task ReloadAllSettingsAsync()
    {
        _isLoading = true;
        try
        {
            await LoadSettingAsync(nameof(LaunchMode), async () =>
                LaunchMode = await LocalSettingsService.ReadSettingAsync<LaunchMode?>(nameof(LaunchMode)) ?? LaunchMode.CreatingQrCodes);

            await LoadSettingAsync(nameof(MultiLineCodeMode), async () =>
                MultiLineCodeMode = await LocalSettingsService.ReadSettingAsync<MultiLineCodeMode>(nameof(MultiLineCodeMode)));

            await LoadSettingAsync(nameof(BaseText), async () =>
                BaseText = await LocalSettingsService.ReadSettingAsync<string>(nameof(BaseText)) ?? string.Empty);

            await LoadSettingAsync(nameof(WarnWhenNotUrl), async () =>
                WarnWhenNotUrl = await LocalSettingsService.ReadSettingAsync<bool?>(nameof(WarnWhenNotUrl)) ?? true);

            await LoadSettingAsync(nameof(WarnWhenLikelyRedirector), async () =>
                WarnWhenLikelyRedirector = await RedirectorWarningSettingsHelper.ReadWarningEnabledAsync(LocalSettingsService));

            await LoadSettingAsync(nameof(SafeRedirectorDomains), async () =>
                ReplaceSafeRedirectorDomains(await RedirectorWarningSettingsHelper.ReadSafeDomainsAsync(LocalSettingsService)));

            await LoadSettingAsync(nameof(HideMinimumSizeText), async () =>
                HideMinimumSizeText = await LocalSettingsService.ReadSettingAsync<bool>(nameof(HideMinimumSizeText)));

            await LoadSettingAsync(nameof(ShowSaveBothButton), async () =>
                ShowSaveBothButton = await LocalSettingsService.ReadSettingAsync<bool>(nameof(ShowSaveBothButton)));

            await LoadSettingAsync(nameof(ShowPrintButton), async () =>
                ShowPrintButton = await LocalSettingsService.ReadSettingAsync<bool?>(nameof(ShowPrintButton)) ?? true);

            await LoadSettingAsync(nameof(ShowZipSaveOptions), async () =>
                ShowZipSaveOptions = await LocalSettingsService.ReadSettingAsync<bool?>(nameof(ShowZipSaveOptions)) ?? true);

            await LoadSettingAsync(nameof(MinSizeScanDistanceScaleFactor), async () =>
            {
                MinSizeScanDistanceScaleFactor = await LocalSettingsService.ReadSettingAsync<double>(nameof(MinSizeScanDistanceScaleFactor));
                if (MinSizeScanDistanceScaleFactor < 0.35)
                {
                    MinSizeScanDistanceScaleFactor = 1;
                    await LocalSettingsService.SaveSettingAsync(nameof(MinSizeScanDistanceScaleFactor), MinSizeScanDistanceScaleFactor);
                }
            });

            await LoadSettingAsync(nameof(QrPaddingModules), async () =>
            {
                double? storedQrPaddingModules = await LocalSettingsService.ReadSettingAsync<double?>(nameof(QrPaddingModules));
                double normalizedQrPaddingModules = BarcodeHelpers.NormalizeQrPaddingModules(storedQrPaddingModules ?? 2.0);
                QrPaddingModules = normalizedQrPaddingModules;

                if (!storedQrPaddingModules.HasValue || storedQrPaddingModules.Value != normalizedQrPaddingModules)
                    await LocalSettingsService.SaveSettingAsync(nameof(QrPaddingModules), normalizedQrPaddingModules);
            });

            await LoadSettingAsync(nameof(QuickSaveLocation), async () =>
                QuickSaveLocation = await LocalSettingsService.ReadSettingAsync<string>(nameof(QuickSaveLocation)) ?? string.Empty);

            await LoadSettingAsync(nameof(UseAutoBrands), async () =>
                UseAutoBrands = await LocalSettingsService.ReadSettingAsync<bool>(nameof(UseAutoBrands)));

            await _themeSelectorService.RefreshThemeAsync();
            ElementTheme = _themeSelectorService.Theme;
        }
        finally
        {
            _isLoading = false;
            Trace.WriteLine("[SettingsVM] Settings loaded");
        }
    }

    private static async Task LoadSettingAsync(string key, Func<Task> loadAction)
    {
        try
        {
            Trace.WriteLine($"[SettingsVM] Loading '{key}'");
            await loadAction();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[SettingsVM] Failed to load setting '{key}': {ex.Message}");
        }
    }

    private void ReplaceSafeRedirectorDomains(IEnumerable<string> domains)
    {
        SafeRedirectorDomains.Clear();
        foreach (string domain in domains)
            SafeRedirectorDomains.Add(domain);
    }

    public async void OnNavigatedFrom()
    {
        Trace.WriteLine($"[SettingsVM] OnNavigatedFrom (debounce pending: {settingChangedDebounceTimer.IsEnabled})");
        if (settingChangedDebounceTimer.IsEnabled)
        {
            settingChangedDebounceTimer.Stop();
            await SaveAllSettingsAsync();
        }
    }
}
