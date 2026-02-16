using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.ViewModels;
using System.Diagnostics;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel
    {
        get;
    }

    public ShellPage(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;

        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated;
        App.MainWindow.Closed += MainWindow_Closed;
        AppTitleBarText.Text = "AppDisplayName".GetLocalized();
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = AppTitleBarText;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        try
        {
            if (NavigationFrame.GetPageViewModel() is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ MainWindow_Closed error: {ex}");
        }

        // Force a clean process exit to prevent the WinRT XAML runtime's
        // internal teardown from throwing native stowed exceptions (0xc000027b).
        // This is a known WinUI 3 issue where the XAML teardown sequence
        // accesses disposed objects regardless of app-level cleanup.
        Environment.Exit(0);
    }
}
