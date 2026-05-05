using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Simple_QR_Code_Maker.Services;

public class HistoryService : IHistoryService
{
    public async Task LoadAsync(ObservableCollection<HistoryItem> historyItems)
    {
        ObservableCollection<HistoryItem> loadedHistory = await HistoryStorageHelper.LoadHistoryAsync();

        historyItems.Clear();
        foreach (HistoryItem item in loadedHistory)
        {
            historyItems.Add(item);
        }
    }

    public async Task SaveAsync(ObservableCollection<HistoryItem> historyItems)
    {
        await HistoryStorageHelper.SaveHistoryAsync(historyItems);
    }

    public async Task AddOrReplaceAndSaveAsync(ObservableCollection<HistoryItem> historyItems, HistoryItem historyItem)
    {
        historyItems.Remove(historyItem);
        historyItems.Insert(0, historyItem);
        await HistoryStorageHelper.SaveHistoryAsync(historyItems);
    }

    public void SaveSnapshotOnShutdown(IEnumerable<HistoryItem> historyItems, HistoryItem historyItem)
    {
        try
        {
            ObservableCollection<HistoryItem> snapshot = new(historyItems.Where(item => !item.Equals(historyItem)));
            snapshot.Insert(0, historyItem);
            _ = HistoryStorageHelper.SaveHistoryAsync(snapshot);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ SaveHistoryOnShutdown error: {ex}");
        }
    }
}
