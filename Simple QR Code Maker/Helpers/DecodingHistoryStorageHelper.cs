using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Helpers;

public static class DecodingHistoryStorageHelper
{
    private const string HistoryFileName = "DecodingHistory.json";
    private const string HistoryImagesFolderName = "DecodingHistoryImages";
    private const int MaxHistoryItems = 50;

    public static async Task<string?> SaveImageCopyAsync(string sourcePath)
    {
        try
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return null;

            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFolder imagesFolder = await localFolder.CreateFolderAsync(
                HistoryImagesFolderName,
                CreationCollisionOption.OpenIfExists);

            string fileName = $"{DateTimeOffset.Now.Ticks}_decoded.png";
            StorageFile destFile = await imagesFolder.CreateFileAsync(
                fileName,
                CreationCollisionOption.ReplaceExisting);

            await Task.Run(() => File.Copy(sourcePath, destFile.Path, overwrite: true));

            return destFile.Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save image copy for decoding history: {ex.Message}");
            return null;
        }
    }

    public static async Task<ObservableCollection<DecodingHistoryItem>> LoadHistoryAsync()
    {
        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            IStorageItem? item = await localFolder.TryGetItemAsync(HistoryFileName);

            if (item is not StorageFile historyFile)
                return [];

            string json = await FileIO.ReadTextAsync(historyFile);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            ObservableCollection<DecodingHistoryItem>? history =
                JsonSerializer.Deserialize(json, DecodingHistoryJsonContext.Default.ObservableCollectionDecodingHistoryItem);

            return history ?? [];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load decoding history: {ex.Message}");
            return [];
        }
    }

    public static async Task SaveHistoryAsync(ObservableCollection<DecodingHistoryItem> items)
    {
        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string json = JsonSerializer.Serialize(items, DecodingHistoryJsonContext.Default.ObservableCollectionDecodingHistoryItem);

            string tempFileName = HistoryFileName + ".tmp";
            StorageFile tempFile = await localFolder.CreateFileAsync(
                tempFileName,
                CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(tempFile, json);
            await tempFile.RenameAsync(HistoryFileName, NameCollisionOption.ReplaceExisting);

            Debug.WriteLine($"Saved {items.Count} decoding history items.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save decoding history: {ex.Message}");
        }
    }

    public static async Task AddAndSaveAsync(
        ObservableCollection<DecodingHistoryItem> items,
        DecodingHistoryItem newItem)
    {
        items.Insert(0, newItem);

        while (items.Count > MaxHistoryItems)
            items.RemoveAt(items.Count - 1);

        await SaveHistoryAsync(items);
    }

    /// <summary>
    /// Replaces the image and decoded texts of the most-recent history item in place.
    /// Used when the user applies advanced tool changes to an already-saved image so that
    /// a second history entry is not created.
    /// </summary>
    public static async Task UpdateLatestAndSaveAsync(
        ObservableCollection<DecodingHistoryItem> items,
        string newImageSourcePath,
        List<string> newDecodedTexts)
    {
        if (items.Count == 0)
            return;

        DecodingHistoryItem latest = items[0];

        // Delete the old image file so it doesn't become orphaned
        if (!string.IsNullOrEmpty(latest.SavedImagePath) && File.Exists(latest.SavedImagePath))
        {
            try { File.Delete(latest.SavedImagePath); }
            catch (Exception ex) { Debug.WriteLine($"Could not delete old history image: {ex.Message}"); }
        }

        string? savedImagePath = await SaveImageCopyAsync(newImageSourcePath);

        latest.SavedImagePath = savedImagePath ?? string.Empty;
        latest.DecodedTexts = newDecodedTexts;

        await SaveHistoryAsync(items);
    }
}
