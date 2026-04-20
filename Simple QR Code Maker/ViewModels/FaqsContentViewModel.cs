using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class FaqsContentViewModel : ObservableRecipient
{
    [ObservableProperty]
    public partial ObservableCollection<FaqItem> FaqItems { get; set; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    private readonly INavigationService navigationService;

    public FaqsContentViewModel(INavigationService navigationService)
    {
        this.navigationService = navigationService;
        searchTimer.Tick += SearchTimer_Tick; ;
        searchTimer.Interval = TimeSpan.FromMilliseconds(500);

        foreach (FaqItem item in FaqItem.AllFaqs)
            FaqItems.Add(item);

        WeakReferenceMessenger.Default.Register<RequestPaneChange>(this, OnRequestPaneChange);
    }

    public async Task ActivateFeatureAsync(FaqItem faqItem)
    {
        object? currentPageViewModel = navigationService.Frame?.GetPageViewModel();

        switch (faqItem.ActionKind)
        {
            case FaqActionKind.OpenReadCodes:
                OpenReadCodes(currentPageViewModel);
                break;
            case FaqActionKind.OpenAdvancedTools:
                OpenAdvancedTools(currentPageViewModel);
                break;
            case FaqActionKind.OpenSettings:
                OpenSettings(currentPageViewModel);
                break;
            case FaqActionKind.OpenUrlBuilder:
                await OpenBuilderAsync(currentPageViewModel, static viewModel => viewModel.OpenUrlBuilderCommand.ExecuteAsync(null));
                break;
            case FaqActionKind.OpenVCardBuilder:
                await OpenBuilderAsync(currentPageViewModel, static viewModel => viewModel.OpenVCardBuilderCommand.ExecuteAsync(null));
                break;
            case FaqActionKind.OpenWifiBuilder:
                await OpenBuilderAsync(currentPageViewModel, static viewModel => viewModel.OpenWifiBuilderCommand.ExecuteAsync(null));
                break;
            default:
                break;
        }
    }

    private async Task OpenBuilderAsync(object? currentPageViewModel, Func<MainViewModel, Task> openBuilderAsync)
    {
        CloseFaqPane(currentPageViewModel);

        MainViewModel? mainViewModel = currentPageViewModel as MainViewModel;
        if (mainViewModel is null)
        {
            navigationService.NavigateTo(typeof(MainViewModel).FullName!);
            mainViewModel = navigationService.Frame?.GetPageViewModel() as MainViewModel;
        }

        if (mainViewModel is null)
            throw new InvalidOperationException("Could not resolve the main page view model for FAQ navigation.");

        mainViewModel.IsFaqPaneOpen = false;
        await openBuilderAsync(mainViewModel);
    }

    private static void CloseFaqPane(object? currentPageViewModel)
    {
        switch (currentPageViewModel)
        {
            case MainViewModel mainViewModel:
                mainViewModel.IsFaqPaneOpen = false;
                break;
            case DecodingViewModel decodingViewModel:
                decodingViewModel.IsFaqPaneOpen = false;
                break;
        }
    }

    private void OpenReadCodes(object? currentPageViewModel)
    {
        switch (currentPageViewModel)
        {
            case DecodingViewModel decodingViewModel:
                decodingViewModel.IsFaqPaneOpen = false;
                break;
            case MainViewModel mainViewModel:
                mainViewModel.OpenFileCommand.Execute(null);
                break;
            default:
                navigationService.NavigateTo(typeof(DecodingViewModel).FullName!);
                break;
        }
    }

    private void OpenAdvancedTools(object? currentPageViewModel)
    {
        if (currentPageViewModel is MainViewModel mainViewModel)
        {
            mainViewModel.OpenFileCommand.Execute(null);
            currentPageViewModel = navigationService.Frame?.GetPageViewModel();
        }
        else if (currentPageViewModel is not DecodingViewModel)
        {
            navigationService.NavigateTo(typeof(DecodingViewModel).FullName!);
            currentPageViewModel = navigationService.Frame?.GetPageViewModel();
        }

        if (currentPageViewModel is DecodingViewModel decodingViewModel)
        {
            decodingViewModel.IsAdvancedToolsVisible = true;
            decodingViewModel.IsFaqPaneOpen = false;
        }
    }

    private void OpenSettings(object? currentPageViewModel)
    {
        if (currentPageViewModel is MainViewModel mainViewModel)
        {
            mainViewModel.GoToSettingsCommand.Execute(null);
            return;
        }

        navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
    }

    private void OnRequestPaneChange(object recipient, RequestPaneChange message)
    {
        if (message.Pane == MainViewPanes.Faq && message.RequestState == PaneState.Open)
            SearchText = message.SearchText;
    }

    private void SearchTimer_Tick(object? sender, object e)
    {
        searchTimer.Stop();

        FaqItems.Clear();

        foreach (FaqItem item in FaqItem.AllFaqs)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                if (FaqItems.Contains(item))
                    continue;
                FaqItems.Add(item);
                continue;
            }

            if (item.Title.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
                || item.Content.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase))
            {
                if (FaqItems.Contains(item))
                    continue;

                FaqItems.Add(item);
            }
        }
    }

    private DispatcherTimer searchTimer = new();

    partial void OnSearchTextChanged(string value)
    {
        try
        {
            searchTimer.Stop();
            searchTimer.Start();

        }
        catch (Exception)
        {
            searchTimer = new();
            searchTimer.Start();
#if DEBUG
            throw;
#endif
        }
    }
}
