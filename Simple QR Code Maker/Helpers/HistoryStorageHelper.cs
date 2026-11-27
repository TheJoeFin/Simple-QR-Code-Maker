using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.Helpers;

public static class HistoryStorageHelper
{
    public static Task<ObservableCollection<HistoryItem>> LoadHistoryAsync() =>
        App.GetService<IHistoryStore>().LoadAsync();

    public static Task SaveHistoryAsync(ObservableCollection<HistoryItem> historyItems) =>
        App.GetService<IHistoryStore>().SaveAsync(historyItems);
}