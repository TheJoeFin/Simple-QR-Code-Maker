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
        searchTimer.Tick += SearchTimer_Tick; ;
        searchTimer.Interval = TimeSpan.FromMilliseconds(500);

        foreach (FaqItem item in FaqItem.AllFaqs)
            FaqItems.Add(item);

        WeakReferenceMessenger.Default.Register<RequestPaneChange>(this, OnRequestPaneChange);
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
