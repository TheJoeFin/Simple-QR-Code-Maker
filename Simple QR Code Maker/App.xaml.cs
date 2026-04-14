using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Simple_QR_Code_Maker.Activation;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Core.Contracts.Services;
using Simple_QR_Code_Maker.Core.Services;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;
using Simple_QR_Code_Maker.ViewModels;
using Simple_QR_Code_Maker.Views;
using System.Diagnostics;

namespace Simple_QR_Code_Maker;

// To learn more about WinUI 3, see https://docs.microsoft.com/windows/apps/winui/winui3/.
public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static WindowEx MainWindow { get; } = new MainWindow();

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers
            services.AddTransient<IActivationHandler, ShareTargetActivationHandler>();

            // Services
            services.AddTransient<IWebViewService, WebViewService>();
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<AboutQrCodesWebViewModel>();
            services.AddTransient<AboutQrCodesWebPage>();
            services.AddTransient<DecodingViewModel>();
            services.AddTransient<DecodingPage>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainPage>();
            services.AddTransient<SpreadsheetImportViewModel>();
            services.AddTransient<SpreadsheetImportPage>();
            services.AddTransient<ShellViewModel>();
            services.AddTransient<ShellPage>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        UnhandledException += App_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"❌ UnhandledException: {e.Exception}");
        Debug.WriteLine($"   Message: {e.Message}");
        Debug.WriteLine($"   StackTrace: {e.Exception.StackTrace}");
        if (e.Exception.InnerException is not null)
        {
            Debug.WriteLine($"   InnerException: {e.Exception.InnerException}");
        }

        e.Handled = true;
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Debug.WriteLine($"❌ UnobservedTaskException: {e.Exception}");
        e.SetObserved();
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"❌ AppDomain.UnhandledException: {e.ExceptionObject}");
        Debug.WriteLine($"   IsTerminating: {e.IsTerminating}");
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        // Check if the app was activated via share target or other non-launch activation.
        AppInstance appInstance = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent();
        var activatedArgs = appInstance.GetActivatedEventArgs();

        if (activatedArgs?.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.ShareTarget
            && activatedArgs.Data is Windows.ApplicationModel.Activation.ShareTargetActivatedEventArgs shareArgs)
        {
            await App.GetService<IActivationService>().ActivateAsync(shareArgs);
        }
        else
        {
            await App.GetService<IActivationService>().ActivateAsync(args);
        }
    }
}
