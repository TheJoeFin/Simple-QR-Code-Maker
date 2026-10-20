using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_QR_Code_Maker.Core.Storage;
using Simple_QR_Code_Maker.Core.Storage.Models;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.Maui.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly QrHistoryRepository historyRepository;
    private readonly CreatorViewModel creatorViewModel;
    private readonly List<HistoryLibraryItem> entries = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string status = "Loading history…";

    public ObservableCollection<HistoryLibraryItem> FilteredEntries { get; } = [];

    public HistoryViewModel(QrHistoryRepository historyRepository, CreatorViewModel creatorViewModel)
    {
        this.historyRepository = historyRepository;
        this.creatorViewModel = creatorViewModel;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            QrHistoryDocument document = await historyRepository.LoadAsync();
            entries.Clear();
            entries.AddRange(document.Entries
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Select(entry => new HistoryLibraryItem(entry)));
            ApplyFilter();
            Status = entries.Count == 0
                ? "No QR codes are in history."
                : $"{entries.Count} QR code{(entries.Count == 1 ? string.Empty : "s")} in history.";
        }
        catch (Exception exception)
        {
            Status = $"Could not load history: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ReopenAsync(HistoryLibraryItem? item)
    {
        if (item is null)
            return;

        creatorViewModel.ApplyDesign(item.Entry.Design);
        await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
        Status = "QR code reopened in Create.";
    }

    [RelayCommand]
    private async Task RemoveAsync(HistoryLibraryItem? item)
    {
        if (item is null)
            return;

        try
        {
            await historyRepository.DeleteAsync(item.Entry.Id);
            await RefreshAsync();
            Status = "History entry removed.";
        }
        catch (Exception exception)
        {
            Status = $"Could not remove history entry: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        try
        {
            await historyRepository.ClearAsync();
            await RefreshAsync();
            Status = "History cleared.";
        }
        catch (Exception exception)
        {
            Status = $"Could not clear history: {exception.Message}";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<HistoryLibraryItem> filtered = entries.Where(item =>
            string.IsNullOrWhiteSpace(SearchText)
            || item.Content.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));

        FilteredEntries.Clear();
        foreach (HistoryLibraryItem item in filtered)
            FilteredEntries.Add(item);
    }
}

public sealed record HistoryLibraryItem(QrHistoryEntry Entry)
{
    public string Content => Entry.Design.Content;

    public string CreatedAt => Entry.CreatedAtUtc.LocalDateTime.ToString("g");

    public string Summary => $"{Entry.Design.PixelSize}px · {Entry.Design.ErrorCorrectionLevel} correction";
}
