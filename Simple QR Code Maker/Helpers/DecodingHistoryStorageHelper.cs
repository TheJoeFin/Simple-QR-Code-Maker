using System.Collections.ObjectModel;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Helpers;

public static class DecodingHistoryStorageHelper
{
    public static Task<string?> SaveImageCopyAsync(string sourcePath) =>
        App.GetService<IDecodingHistoryStore>().SaveImageCopyAsync(sourcePath);

    public static Task<ObservableCollection<DecodingHistoryItem>> LoadHistoryAsync() =>
        App.GetService<IDecodingHistoryStore>().LoadAsync();

    public static Task SaveHistoryAsync(ObservableCollection<DecodingHistoryItem> items) =>
        App.GetService<IDecodingHistoryStore>().SaveAsync(items);

    public static Task AddAndSaveAsync(ObservableCollection<DecodingHistoryItem> items, DecodingHistoryItem newItem) =>
        App.GetService<IDecodingHistoryStore>().AddAndSaveAsync(items, newItem);

    public static Task UpdateLatestAndSaveAsync(ObservableCollection<DecodingHistoryItem> items, string newImageSourcePath, List<string> newDecodedTexts) =>
        App.GetService<IDecodingHistoryStore>().UpdateLatestAndSaveAsync(items, newImageSourcePath, newDecodedTexts);
}