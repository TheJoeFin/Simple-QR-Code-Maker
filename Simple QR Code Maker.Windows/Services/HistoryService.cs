using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace Simple_QR_Code_Maker.Services;

public class HistoryService(IHistoryStore store) : IHistoryService
{
    public Task PendingShutdownSave { get; private set; } = Task.CompletedTask;

    public async Task LoadAsync(ObservableCollection<HistoryItem> historyItems)
    {
        ObservableCollection<HistoryItem> loadedHistory = await store.LoadAsync();

        historyItems.Clear();
        foreach (HistoryItem item in loadedHistory)
        {
            historyItems.Add(item);
        }
    }

    public async Task SaveAsync(ObservableCollection<HistoryItem> historyItems)
    {
        await store.SaveAsync(historyItems);
    }

    public async Task AddOrReplaceAndSaveAsync(ObservableCollection<HistoryItem> historyItems, HistoryItem historyItem)
    {
        historyItems.Remove(historyItem);
        historyItems.Insert(0, historyItem);
        await store.SaveAsync(historyItems);
    }

    public void SaveSnapshotOnShutdown(IEnumerable<HistoryItem> historyItems, HistoryItem historyItem)
    {
        try
        {
            ObservableCollection<HistoryItem> snapshot = new(historyItems.Where(item => !item.Equals(historyItem)));
            snapshot.Insert(0, historyItem);
            // Freeze mutable items and arrays before any asynchronous work can yield.
            string json = JsonSerializer.Serialize(snapshot, HistoryJsonContext.Default.ObservableCollectionHistoryItem);
            snapshot = JsonSerializer.Deserialize(json, HistoryJsonContext.Default.ObservableCollectionHistoryItem)!;
            PendingShutdownSave = Task.WhenAll(PendingShutdownSave, SaveShutdownSnapshotAsync(snapshot));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveHistoryOnShutdown error: {ex}");
            PendingShutdownSave = Task.WhenAll(PendingShutdownSave, Task.FromException(ex));
        }
    }

    private async Task SaveShutdownSnapshotAsync(ObservableCollection<HistoryItem> snapshot)
    {
        try
        {
            await store.SaveAsync(snapshot).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveHistoryOnShutdown error: {ex}");
            throw;
        }
    }
}
