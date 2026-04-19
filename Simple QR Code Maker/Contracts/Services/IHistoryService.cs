using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.Contracts.Services;

public interface IHistoryService
{
    Task LoadAsync(ObservableCollection<HistoryItem> historyItems);

    Task SaveAsync(ObservableCollection<HistoryItem> historyItems);

    Task AddOrReplaceAndSaveAsync(ObservableCollection<HistoryItem> historyItems, HistoryItem historyItem);

    void SaveSnapshotOnShutdown(IEnumerable<HistoryItem> historyItems, HistoryItem historyItem);
}
