using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class FaqsContentViewModel : ObservableRecipient
{
    [ObservableProperty]
    public ObservableCollection<FaqItem> faqItems = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    public FaqsContentViewModel()
    {
        _searchTimer.Tick += _searchTimer_Tick; ;
        _searchTimer.Interval = TimeSpan.FromMilliseconds(500);

        foreach (var item in FaqItem.AllFaqs)
            FaqItems.Add(item);

        WeakReferenceMessenger.Default.Register<RequestPaneChange>(this, OnRequestPaneChange);
    }

    private void OnRequestPaneChange(object recipient, RequestPaneChange message)
    {
        if (message.Pane == MainViewPanes.Faq && message.RequestState == PaneState.Open)
            SearchText = message.SearchText;
    }

    private void _searchTimer_Tick(object? sender, object e)
    {
        _searchTimer.Stop();

        FaqItems.Clear();

        foreach (var item in FaqItem.AllFaqs)
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

    private DispatcherTimer _searchTimer = new();

    partial void OnSearchTextChanged(string value)
    {
        try
        {
            _searchTimer.Stop();
            _searchTimer.Start();

        }
        catch (Exception)
        {
            _searchTimer = new();
            _searchTimer.Start();
#if DEBUG
            throw;
#endif
        }
    }
}
