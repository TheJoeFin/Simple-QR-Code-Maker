using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Navigation;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    private const int MaxBrandResults = 3;
    private const int MaxHistoryResults = 3;
    private const int MaxFaqResults = 4;
    private const int MaxPreviewLength = 80;
    private int titleBarSearchRequestVersion;

    [ObservableProperty]
    public partial bool IsBackEnabled { get; set; }

    [ObservableProperty]
    public partial object? Selected { get; set; }
    public ObservableCollection<TitleBarSearchResult> TitleBarSearchResults { get; } = [];

    [RelayCommand]
    private void Back()
    {
        if (NavigationService.CanGoBack)
            NavigationService.GoBack();
    }

    public INavigationService NavigationService
    {
        get;
    }

    public ShellViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;
        ClearTitleBarSearch();
    }

    public void ClearTitleBarSearch()
    {
        titleBarSearchRequestVersion++;
        TitleBarSearchResults.Clear();
    }

    [RequiresUnreferencedCode("Calls BrandStorageHelper.LoadBrandsAsync and HistoryStorageHelper.LoadHistoryAsync")]
    public async Task RefreshTitleBarSearchResultsAsync(string searchText)
    {
        int requestVersion = ++titleBarSearchRequestVersion;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            if (requestVersion == titleBarSearchRequestVersion)
                TitleBarSearchResults.Clear();
            return;
        }

        string query = searchText.Trim();
        object? currentPageViewModel = NavigationService.Frame?.GetPageViewModel();

        IReadOnlyList<BrandItem> brands;
        IReadOnlyList<HistoryItem> historyItems;

        if (currentPageViewModel is MainViewModel mainViewModel)
        {
            brands = mainViewModel.BrandItems.ToList();
            historyItems = mainViewModel.HistoryItems.ToList();
        }
        else
        {
            Task<ObservableCollection<BrandItem>> loadBrandsTask = BrandStorageHelper.LoadBrandsAsync();
            Task<ObservableCollection<HistoryItem>> loadHistoryTask = HistoryStorageHelper.LoadHistoryAsync();
            await Task.WhenAll(loadBrandsTask, loadHistoryTask);

            brands = [.. loadBrandsTask.Result];
            historyItems = [.. loadHistoryTask.Result];
        }

        List<TitleBarSearchResult> searchResults =
        [
            .. GetBrandSearchResults(brands, query),
            .. GetHistorySearchResults(historyItems, query),
            .. GetFaqSearchResults(query)
        ];

        if (requestVersion != titleBarSearchRequestVersion)
            return;

        TitleBarSearchResults.Clear();
        foreach (TitleBarSearchResult result in searchResults)
            TitleBarSearchResults.Add(result);
    }

    public async Task ApplyTitleBarSearchResultAsync(TitleBarSearchResult? searchResult)
    {
        if (searchResult is null)
            return;

        object? currentPageViewModel = NavigationService.Frame?.GetPageViewModel();

        switch (searchResult.Kind)
        {
            case TitleBarSearchResultKind.Brand when searchResult.BrandItem is not null:
                if (currentPageViewModel is MainViewModel mainViewModel)
                {
                    BrandItem brandToApply = mainViewModel.BrandItems
                        .FirstOrDefault(item => item.Equals(searchResult.BrandItem))
                        ?? searchResult.BrandItem;

                    await mainViewModel.ApplyBrandCommand.ExecuteAsync(brandToApply);
                    return;
                }

                NavigationService.NavigateTo(typeof(MainViewModel).FullName!, searchResult);
                return;

            case TitleBarSearchResultKind.History when searchResult.HistoryItem is not null:
                if (currentPageViewModel is MainViewModel currentMainViewModel)
                {
                    currentMainViewModel.SelectedHistoryItem = currentMainViewModel.HistoryItems
                        .FirstOrDefault(item => item.Equals(searchResult.HistoryItem))
                        ?? searchResult.HistoryItem;
                    return;
                }

                NavigationService.NavigateTo(typeof(MainViewModel).FullName!, searchResult);
                return;

            case TitleBarSearchResultKind.Faq:
                if (currentPageViewModel is DecodingViewModel decodingViewModel)
                {
                    decodingViewModel.IsFaqPaneOpen = true;
                    WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, searchResult.SearchText));
                    return;
                }

                if (currentPageViewModel is MainViewModel)
                {
                    WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, searchResult.SearchText));
                    return;
                }

                NavigationService.NavigateTo(typeof(MainViewModel).FullName!, searchResult);
                return;
        }
    }

    private static IEnumerable<TitleBarSearchResult> GetBrandSearchResults(
        IEnumerable<BrandItem> brandItems,
        string query)
    {
        return brandItems
            .Where(item => MatchesQuery(item.Name, query) || MatchesQuery(item.UrlContent, query))
            .OrderBy(item => GetBestMatchScore(query, item.Name, item.UrlContent))
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxBrandResults)
            .Select(item => new TitleBarSearchResult
            {
                Kind = TitleBarSearchResultKind.Brand,
                Title = item.Name,
                Subtitle = GetBrandSubtitle(item),
                SearchText = item.Name,
                BrandItem = item,
            });
    }

    private static IEnumerable<TitleBarSearchResult> GetHistorySearchResults(
        IEnumerable<HistoryItem> historyItems,
        string query)
    {
        return historyItems
            .Where(item => MatchesQuery(item.CodesContent, query))
            .OrderBy(item => GetBestMatchScore(query, item.CodesContent))
            .ThenByDescending(item => item.SavedDateTime)
            .Take(MaxHistoryResults)
            .Select(item => new TitleBarSearchResult
            {
                Kind = TitleBarSearchResultKind.History,
                Title = Truncate(item.DisplayText),
                Subtitle = string.IsNullOrWhiteSpace(item.ContentKindLabel)
                    ? $"Saved {item.SaveDateAsString}"
                    : $"{item.ContentKindLabel} - Saved {item.SaveDateAsString}",
                SearchText = item.CodesContent,
                HistoryItem = item,
            });
    }

    private static IEnumerable<TitleBarSearchResult> GetFaqSearchResults(string query)
    {
        return FaqItem.AllFaqs
            .Where(item => MatchesQuery(item.Title, query) || MatchesQuery(item.Content, query))
            .OrderBy(item => GetBestMatchScore(query, item.Title, item.Content))
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxFaqResults)
            .Select(item => new TitleBarSearchResult
            {
                Kind = TitleBarSearchResultKind.Faq,
                Title = item.Title,
                Subtitle = Truncate(item.Content),
                SearchText = item.Title,
            });
    }

    private static string GetBrandSubtitle(BrandItem brandItem)
    {
        string description = string.IsNullOrWhiteSpace(brandItem.UrlContent)
            ? "Saved brand"
            : Truncate(brandItem.UrlContent);

        if (brandItem.IsDefault)
            return $"Default brand - {description}";

        return description;
    }

    private static bool MatchesQuery(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private static int GetBestMatchScore(string query, params string?[] values)
    {
        int score = int.MaxValue;

        foreach (string? value in values)
            score = Math.Min(score, GetMatchScore(query, value));

        return score;
    }

    private static int GetMatchScore(string query, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return int.MaxValue;

        if (string.Equals(value, query, StringComparison.CurrentCultureIgnoreCase))
            return 0;

        if (value.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            return 1;

        int index = value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
        return index >= 0 ? 2 + index : int.MaxValue;
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        return trimmed.Length <= MaxPreviewLength
            ? trimmed
            : $"{trimmed[..(MaxPreviewLength - 1)]}…";
    }
}
