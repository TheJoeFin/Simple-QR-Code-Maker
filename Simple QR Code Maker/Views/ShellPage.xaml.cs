using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using System.Diagnostics;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class ShellPage : Page
{
    private readonly DispatcherTimer titleBarSearchTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };

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
        App.MainWindow.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Closed += MainWindow_Closed;
        AppTitleBar.Title = App.MainWindow.Title;
        ViewModel.NavigationService.Navigated += NavigationService_Navigated;
        titleBarSearchTimer.Tick += TitleBarSearchTimer_Tick;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        titleBarSearchTimer.Stop();
        titleBarSearchTimer.Tick -= TitleBarSearchTimer_Tick;
        ViewModel.NavigationService.Navigated -= NavigationService_Navigated;

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

    private void TitleBarSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;

        titleBarSearchTimer.Stop();

        if (string.IsNullOrWhiteSpace(sender.Text))
        {
            ViewModel.ClearTitleBarSearch();
            return;
        }

        titleBarSearchTimer.Start();
    }

    private async void TitleBarSearchTimer_Tick(object? sender, object e)
    {
        titleBarSearchTimer.Stop();
        await ViewModel.RefreshTitleBarSearchResultsAsync(TitleBarSearchBox.Text);
    }

    private void TitleBarSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is TitleBarSearchResult searchResult)
            sender.Text = searchResult.Title;
    }

    private async void TitleBarSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        TitleBarSearchResult? selectedResult = args.ChosenSuggestion as TitleBarSearchResult
            ?? ViewModel.TitleBarSearchResults.FirstOrDefault();

        if (selectedResult is null)
            return;

        await ViewModel.ApplyTitleBarSearchResultAsync(selectedResult);
        ClearTitleBarSearchBox();
    }

    private void ClearTitleBarSearchBox()
    {
        titleBarSearchTimer.Stop();
        TitleBarSearchBox.Text = string.Empty;
        ViewModel.ClearTitleBarSearch();
    }

    private void NavigationService_Navigated(object sender, NavigationEventArgs e)
    {
        ClearTitleBarSearchBox();
    }
}
