using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Helpers;
using System.Reflection;
using System.Windows.Input;
using Windows.ApplicationModel;

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

    private readonly DispatcherTimer settingChangedDebounceTimer = new();

    private string navigationText = string.Empty;

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
        await LocalSettingsService.SaveSettingAsync(nameof(BaseText), BaseText);
        await LocalSettingsService.SaveSettingAsync(nameof(WarnWhenNotUrl), WarnWhenNotUrl);
        await LocalSettingsService.SaveSettingAsync(nameof(HideMinimumSizeText), HideMinimumSizeText);
    }

    partial void OnBaseTextChanged(string value)
    {
        settingChangedDebounceTimer.Stop();
        settingChangedDebounceTimer.Start();
    }

    partial void OnWarnWhenNotUrlChanged(bool value)
    {
        settingChangedDebounceTimer.Stop();
        settingChangedDebounceTimer.Start();
    }

    partial void OnHideMinimumSizeTextChanged(bool value)
    {
        settingChangedDebounceTimer.Stop();
        settingChangedDebounceTimer.Start();
    }

    [RelayCommand]
    private void GoHome()
    {
        NavigationService.NavigateTo(typeof(MainViewModel).FullName!, navigationText);
    }

    [RelayCommand]
    private static async Task ReviewApp()
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9NCH56G3RQFC"));
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
        MultiLineCodeMode = await LocalSettingsService.ReadSettingAsync<MultiLineCodeMode>(nameof(MultiLineCodeMode));
        BaseText = await LocalSettingsService.ReadSettingAsync<string>(nameof(BaseText)) ?? string.Empty;
        WarnWhenNotUrl = await LocalSettingsService.ReadSettingAsync<bool>(nameof(WarnWhenNotUrl));
        HideMinimumSizeText = await LocalSettingsService.ReadSettingAsync<bool>(nameof(HideMinimumSizeText));

        if (parameter is string urlText && !string.IsNullOrWhiteSpace(urlText))
        {
            navigationText = urlText;
        }
    }

    public void OnNavigatedFrom()
    {
    }
}
