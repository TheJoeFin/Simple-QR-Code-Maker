using System.Collections.ObjectModel;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Contracts.Services;

public interface IBrandStore
{
    Task<ObservableCollection<BrandItem>> LoadAsync();

    Task SaveAsync(ObservableCollection<BrandItem> items);
}

public interface IHistoryStore
{
    Task<ObservableCollection<HistoryItem>> LoadAsync();

    Task SaveAsync(ObservableCollection<HistoryItem> items);
}

public interface IDecodingHistoryStore
{
    Task<ObservableCollection<DecodingHistoryItem>> LoadAsync();

    Task SaveAsync(ObservableCollection<DecodingHistoryItem> items);

    Task<string?> SaveImageCopyAsync(string sourcePath);

    Task AddAndSaveAsync(ObservableCollection<DecodingHistoryItem> items, DecodingHistoryItem newItem);

    Task ClearAndSaveAsync(ObservableCollection<DecodingHistoryItem> items);

    Task RemoveAndSaveAsync(ObservableCollection<DecodingHistoryItem> items, DecodingHistoryItem item);

    Task UpdateLatestAndSaveAsync(ObservableCollection<DecodingHistoryItem> items, string sourcePath, List<string> decodedTexts);
}
