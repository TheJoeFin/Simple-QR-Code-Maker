using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Services;

public sealed class DecodingHistoryStore(IJsonFileStore files, IHistoryImageStore images) : IDecodingHistoryStore
{
    private const string FileName = "DecodingHistory.json";
    private const int MaximumItems = 50;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<string?> SaveImageCopyAsync(string sourcePath)
    {
        try
        {
            return await images.SaveCopyAsync(sourcePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save image copy for decoding history: {ex.Message}");
            return null;
        }
    }

    public async Task<ObservableCollection<DecodingHistoryItem>> LoadAsync()
    {
        try
        {
            return await ReadAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load decoding history: {ex.Message}");
            return [];
        }
    }

    public async Task SaveAsync(ObservableCollection<DecodingHistoryItem> items)
    {
        string json = Serialize(items);
        await _gate.WaitAsync();
        try
        {
            await SaveCoreAsync(json);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAndSaveAsync(ObservableCollection<DecodingHistoryItem> items, DecodingHistoryItem newItem)
    {
        await _gate.WaitAsync();
        try
        {
            ObservableCollection<DecodingHistoryItem> candidate = new(items.Prepend(newItem).Take(MaximumItems));
            await SaveCoreAsync(Serialize(candidate));
            items.Clear();
            foreach (DecodingHistoryItem item in candidate)
                items.Add(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAndSaveAsync(ObservableCollection<DecodingHistoryItem> items)
    {
        await _gate.WaitAsync();
        try
        {
            await SaveCoreAsync(Serialize([]));
            items.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAndSaveAsync(ObservableCollection<DecodingHistoryItem> items, DecodingHistoryItem item)
    {
        await _gate.WaitAsync();
        try
        {
            ObservableCollection<DecodingHistoryItem> candidate = new(items);
            candidate.Remove(item);
            await SaveCoreAsync(Serialize(candidate));
            items.Remove(item);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateLatestAndSaveAsync(ObservableCollection<DecodingHistoryItem> items, string sourcePath, List<string> decodedTexts)
    {
        await _gate.WaitAsync();
        try
        {
            if (items.Count == 0)
                return;

            DecodingHistoryItem latest = items[0];
            string? replacementPath = await images.SaveCopyAsync(sourcePath);
            if (replacementPath is null)
                throw new IOException("The replacement decoding history image could not be copied.");

            ObservableCollection<DecodingHistoryItem> candidate = JsonSerializer.Deserialize(
                Serialize(items), DecodingHistoryJsonContext.Default.ObservableCollectionDecodingHistoryItem)!;
            candidate[0].SavedImagePath = replacementPath;
            candidate[0].DecodedTexts = [.. decodedTexts];
            string oldImagePath = latest.SavedImagePath;
            try
            {
                await SaveCoreAsync(Serialize(candidate), oldImagePath);
            }
            catch
            {
                await DeleteImageAsync(replacementPath);
                throw;
            }
            latest.SavedImagePath = replacementPath;
            latest.DecodedTexts = [.. decodedTexts];
            if (!string.IsNullOrEmpty(oldImagePath) && !items.Any(item => string.Equals(item.SavedImagePath, oldImagePath, StringComparison.OrdinalIgnoreCase)))
                await DeleteImageAsync(oldImagePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update decoding history: {ex.Message}");
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ObservableCollection<DecodingHistoryItem>> ReadAsync()
    {
        string? json = await files.ReadAsync(FileName);
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize(json, DecodingHistoryJsonContext.Default.ObservableCollectionDecodingHistoryItem) ?? [];
    }

    private async Task SaveCoreAsync(string json, string? replacedImagePath = null)
    {
        ObservableCollection<DecodingHistoryItem> previous = await LoadAsync();
        try
        {
            await files.WriteAsync(FileName, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save decoding history: {ex.Message}");
            throw;
        }
        ObservableCollection<DecodingHistoryItem> current = JsonSerializer.Deserialize(
            json, DecodingHistoryJsonContext.Default.ObservableCollectionDecodingHistoryItem)!;
        HashSet<string> retainedImages = new(current.Select(item => item.SavedImagePath), StringComparer.OrdinalIgnoreCase);
        foreach (string path in previous.Select(item => item.SavedImagePath).Where(path => !string.IsNullOrEmpty(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!retainedImages.Contains(path) && !string.Equals(path, replacedImagePath, StringComparison.OrdinalIgnoreCase))
                await DeleteImageAsync(path);
        }
        Debug.WriteLine($"Saved {current.Count} decoding history items.");
    }

    private async Task DeleteImageAsync(string path)
    {
        try
        {
            await images.DeleteAsync(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not delete decoding history image: {ex.Message}");
        }
    }

    private static string Serialize(ObservableCollection<DecodingHistoryItem> items) =>
        JsonSerializer.Serialize(items, DecodingHistoryJsonContext.Default.ObservableCollectionDecodingHistoryItem);
}
