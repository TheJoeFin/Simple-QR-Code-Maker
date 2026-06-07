using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class ShellPage : Page
{
    private const string AppStoreUrl = "https://apps.microsoft.com/detail/9nch56g3rqfc";

    private readonly DispatcherTimer titleBarSearchTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };
    private bool isTitleBarConfigured;
    private bool isSyncingNavItem;

    public ShellViewModel ViewModel
    {
        get;
    }

    public ShellPage(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        AppTitleBar.BackRequested += (_, _) => ViewModel.BackCommand.Execute(null);
        ViewModel.NavigationService.Frame = NavigationFrame;

        App.MainWindow.Closed += MainWindow_Closed;
        ViewModel.NavigationService.Navigated += NavigationService_Navigated;
        titleBarSearchTimer.Tick += TitleBarSearchTimer_Tick;
        Loaded += ShellPage_Loaded;
    }

    private void ShellPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (TryConfigureTitleBar())
            return;

        DispatcherQueue.TryEnqueue(() => _ = TryConfigureTitleBar());
    }

    private bool TryConfigureTitleBar()
    {
        if (isTitleBarConfigured || App.MainWindow.Content != this || AppTitleBar.XamlRoot is null)
            return false;

        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        App.MainWindow.SetTitleBar(AppTitleBar);
        AppTitleBar.Title = App.MainWindow.Title;
        isTitleBarConfigured = true;
        return true;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        titleBarSearchTimer.Stop();
        titleBarSearchTimer.Tick -= TitleBarSearchTimer_Tick;
        ViewModel.NavigationService.Navigated -= NavigationService_Navigated;
        Loaded -= ShellPage_Loaded;

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
        //titleBarSearchTimer.Stop();
        //await ViewModel.RefreshTitleBarSearchResultsAsync(TitleBarSearchBox.Text);
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
        //titleBarSearchTimer.Stop();
        //TitleBarSearchBox.Text = string.Empty;
        ViewModel.ClearTitleBarSearch();
    }

    private void NavigationService_Navigated(object sender, NavigationEventArgs e)
    {
        ClearTitleBarSearchBox();
        SyncNavViewSelection();
    }

    private void SyncNavViewSelection()
    {
        isSyncingNavItem = true;
        try
        {
            NavView.SelectedItem = NavigationFrame.GetPageViewModel() switch
            {
                MainViewModel => MakeCodesNavItem,
                DecodingViewModel => ReadCodesNavItem,
                SettingsViewModel => SettingsNavItem,
                _ => NavView.SelectedItem,
            };
        }
        finally
        {
            isSyncingNavItem = false;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (isSyncingNavItem)
            return;

        switch (args.SelectedItem)
        {
            case NavigationViewItem { Tag: "Make" }:
                ViewModel.NavigationService.NavigateTo(typeof(MainViewModel).FullName!, clearNavigation: true);
                break;

            case NavigationViewItem { Tag: "Read" }:
                ViewModel.NavigationService.NavigateTo(typeof(DecodingViewModel).FullName!, clearNavigation: true);
                break;

            case NavigationViewItem { Tag: "Settings" }:
                ViewModel.NavigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
                break;
        }
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        switch (args.InvokedItemContainer)
        {
            case NavigationViewItem { Tag: "Faq" }:
                HandleFaqToggle();
                break;

            case NavigationViewItem { Tag: "History" }:
                HandleHistoryToggle();
                break;

            case NavigationViewItem { Tag: "Share" }:
                HandleShareToggle();
                break;
        }
    }

    private void HandleFaqToggle()
    {
        switch (NavigationFrame.GetPageViewModel())
        {
            case MainViewModel mainVm:
                mainVm.ToggleFaqPaneOpenCommand.Execute(null);
                break;
            case DecodingViewModel decodingVm:
                decodingVm.ToggleFaqPaneOpenCommand.Execute(null);
                break;
        }
    }

    private void HandleHistoryToggle()
    {
        switch (NavigationFrame.GetPageViewModel())
        {
            case MainViewModel mainVm:
                mainVm.ToggleHistoryPaneOpenCommand.Execute(null);
                mainVm.MarkHistoryButtonUsedCommand.Execute(null);
                break;
            case DecodingViewModel decodingVm:
                decodingVm.ToggleDecodingHistoryPaneOpenCommand.Execute(null);
                break;
        }
    }

    private void HandleShareToggle()
    {
        ViewModel.IsShareOpen = !ViewModel.IsShareOpen;
    }

    private void CopyLinkButton_Click(object sender, RoutedEventArgs e)
    {
        DataPackage dataPackage = new();
        dataPackage.SetText(AppStoreUrl);
        Clipboard.SetContent(dataPackage);
    }

    private async void VisitLinkButton_Click(object sender, RoutedEventArgs e)
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(AppStoreUrl));
    }

    private void CtrlF_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        //TitleBarSearchBox.Focus(FocusState.Programmatic);
    }
}
