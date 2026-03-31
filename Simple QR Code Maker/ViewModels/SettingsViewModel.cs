using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class SettingsViewModel : ObservableRecipient, INavigationAware
{
    private readonly IThemeSelectorService _themeSelectorService;

    [ObservableProperty]
    private ElementTheme _elementTheme;

    [ObservableProperty]
    private string _versionDescription;

    [ObservableProperty]
    private MultiLineCodeMode _multiLineCodeMode = MultiLineCodeMode.OneLineOneCode;

    [ObservableProperty]
    private string baseText = string.Empty;

    [ObservableProperty]
    private bool warnWhenNotUrl = true;

    [ObservableProperty]
    private bool hideMinimumSizeText = false;

    [ObservableProperty]
    private bool showSaveBothButton = false;

    [ObservableProperty]
    private double minSizeScanDistanceScaleFactor = 1.0;

    [ObservableProperty]
    private string maxScanDistanceText = "36in or 1m";

    [ObservableProperty]
    private string quickSaveLocation = string.Empty;

    [ObservableProperty]
    private bool hasQuickSaveLocation = false;

    private readonly DispatcherTimer settingChangedDebounceTimer = new();

    private bool _isLoading;

    private HistoryItem? navigationHistoryItem = null;

    private INavigationService NavigationService { get; }
    public ILocalSettingsService LocalSettingsService { get; }

    public ICommand SwitchThemeCommand { get; }

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
        _elementTheme = _themeSelectorService.Theme;
        _versionDescription = GetVersionDescription();

        settingChangedDebounceTimer.Interval = TimeSpan.FromMilliseconds(500);
        settingChangedDebounceTimer.Tick -= SettingChangedDebounceTimer_Tick;
        settingChangedDebounceTimer.Tick += SettingChangedDebounceTimer_Tick;

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
        await SaveSingleSettingAsync(nameof(HideMinimumSizeText), HideMinimumSizeText);
        await SaveSingleSettingAsync(nameof(ShowSaveBothButton), ShowSaveBothButton);
        await SaveSingleSettingAsync(nameof(MinSizeScanDistanceScaleFactor), MinSizeScanDistanceScaleFactor);
        await SaveSingleSettingAsync(nameof(QuickSaveLocation), QuickSaveLocation);
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

    partial void OnHideMinimumSizeTextChanged(bool value)
    {
        RestartDebounceTimer();
    }

    partial void OnShowSaveBothButtonChanged(bool value)
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

    [RelayCommand]
    private void GoHome()
    {
        NavigationService.NavigateTo(typeof(MainViewModel).FullName!, navigationHistoryItem);
    }

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

    public async void OnNavigatedTo(object parameter)
    {
        Trace.WriteLine($"[SettingsVM] OnNavigatedTo started (parameter: {parameter?.GetType().Name ?? "null"})");
        _isLoading = true;
        try
        {
            await LoadSettingAsync(nameof(MultiLineCodeMode), async () =>
                MultiLineCodeMode = await LocalSettingsService.ReadSettingAsync<MultiLineCodeMode>(nameof(MultiLineCodeMode)));

            await LoadSettingAsync(nameof(BaseText), async () =>
                BaseText = await LocalSettingsService.ReadSettingAsync<string>(nameof(BaseText)) ?? string.Empty);

            await LoadSettingAsync(nameof(WarnWhenNotUrl), async () =>
                WarnWhenNotUrl = await LocalSettingsService.ReadSettingAsync<bool?>(nameof(WarnWhenNotUrl)) ?? true);

            await LoadSettingAsync(nameof(HideMinimumSizeText), async () =>
                HideMinimumSizeText = await LocalSettingsService.ReadSettingAsync<bool>(nameof(HideMinimumSizeText)));

            await LoadSettingAsync(nameof(ShowSaveBothButton), async () =>
                ShowSaveBothButton = await LocalSettingsService.ReadSettingAsync<bool>(nameof(ShowSaveBothButton)));

            await LoadSettingAsync(nameof(MinSizeScanDistanceScaleFactor), async () =>
            {
                MinSizeScanDistanceScaleFactor = await LocalSettingsService.ReadSettingAsync<double>(nameof(MinSizeScanDistanceScaleFactor));
                if (MinSizeScanDistanceScaleFactor < 0.35)
                {
                    MinSizeScanDistanceScaleFactor = 1;
                    await LocalSettingsService.SaveSettingAsync(nameof(MinSizeScanDistanceScaleFactor), MinSizeScanDistanceScaleFactor);
                }
            });

            await LoadSettingAsync(nameof(QuickSaveLocation), async () =>
                QuickSaveLocation = await LocalSettingsService.ReadSettingAsync<string>(nameof(QuickSaveLocation)) ?? string.Empty);
        }
        finally
        {
            _isLoading = false;
            Trace.WriteLine("[SettingsVM] OnNavigatedTo settings loaded");
        }

        // Store the HistoryItem to pass back when returning to main page
        if (parameter is HistoryItem historyItem)
        {
            navigationHistoryItem = historyItem;
        }
        // For backward compatibility, also handle string parameter
        else if (parameter is string urlText && !string.IsNullOrWhiteSpace(urlText))
        {
            navigationHistoryItem = new HistoryItem { CodesContent = urlText };
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
