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

    public void ActivateFeature(FaqItem faqItem)
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
            default:
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
