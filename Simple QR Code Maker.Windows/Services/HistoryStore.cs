using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;
using Windows.UI;

namespace Simple_QR_Code_Maker.Services;

public sealed class HistoryStore(IJsonFileStore files, ISettingsStore legacySettings, TimeProvider clock) : IHistoryStore
{
    private const string HistoryFileName = "History.json";
    private const string LegacyKey = "HistoryItems";
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public async Task<ObservableCollection<HistoryItem>> LoadAsync()
    {
        await _loadGate.WaitAsync();
        try
        {
            ObservableCollection<HistoryItem>? current = await LoadFileAsync();
            if (current is { Count: > 0 })
                return current;

            ObservableCollection<HistoryItem>? migrated = await MigrateAsync();
            if (migrated is not { Count: > 0 })
                return [];

            // Do not remove the only durable copy until the replacement has been committed.
            await SaveAsync(migrated);
            try
            {
                await legacySettings.RemoveAsync(LegacyKey);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not clear old history settings: {ex.Message}");
            }
            return migrated;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public async Task SaveAsync(ObservableCollection<HistoryItem> items)
    {
        string json = JsonSerializer.Serialize(items, HistoryJsonContext.Default.ObservableCollectionHistoryItem);
        try
        {
            await files.WriteAsync(HistoryFileName, json).ConfigureAwait(false);
            Debug.WriteLine($"Saved {items.Count} items to {HistoryFileName} ({json.Length} bytes)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save history: {ex.Message}");
            throw;
        }
    }

    private async Task<ObservableCollection<HistoryItem>?> LoadFileAsync()
    {
        try
        {
            string? json = await files.ReadAsync(HistoryFileName);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("History.json is missing or empty - attempting migration from old settings.");
                return null;
            }

            ObservableCollection<HistoryItem>? items = JsonSerializer.Deserialize(json, HistoryJsonContext.Default.ObservableCollectionHistoryItem);
            if (items is not null)
                FixColors(items);
            return items;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading {HistoryFileName}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private async Task<ObservableCollection<HistoryItem>?> MigrateAsync()
    {
        try
        {
            string? json = await legacySettings.ReadAsync(LegacyKey);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using JsonDocument document = JsonDocument.Parse(json);
            ObservableCollection<HistoryItem> items = [];
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                try
                {
                    JsonObject itemJson = JsonNode.Parse(element.GetRawText()) as JsonObject
                        ?? throw new JsonException("A history item must be an object.");
                    itemJson["Foreground"] = ParseColor(element, "Foreground", false);
                    itemJson["Background"] = ParseColor(element, "Background", true);
                    if (!element.TryGetProperty("SavedDateTime", out _))
                        itemJson["SavedDateTime"] = clock.GetLocalNow().DateTime;

                    if (element.TryGetProperty("LogoEmojiStyle", out JsonElement emojiStyle)
                        && (emojiStyle.ValueKind != JsonValueKind.String
                            || !Enum.TryParse(emojiStyle.GetString(), true, out EmojiLogoStyle _)))
                    {
                        itemJson["LogoEmojiStyle"] = null;
                    }

                    HistoryItem item = JsonSerializer.Deserialize(itemJson.ToJsonString(), HistoryJsonContext.Default.HistoryItem)
                        ?? throw new JsonException("A history item cannot be null.");
                    items.Add(item);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not migrate history item: {ex.Message}");
                }
            }
            FixColors(items);
            return items;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error migrating old history settings: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string ParseColor(JsonElement item, string propertyName, bool background)
    {
        Color fallback = background ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 0, 0, 0);
        try
        {
            if (!item.TryGetProperty(propertyName, out JsonElement value))
                return ToHex(fallback);

            if (value.ValueKind == JsonValueKind.String)
            {
                string? hex = value.GetString();
                if (hex is { Length: 9 } && hex[0] == '#')
                {
                    _ = Convert.ToUInt32(hex[1..], 16);
                    return hex;
                }
            }
            else if (value.ValueKind == JsonValueKind.Object)
            {
                byte a = value.TryGetProperty("A", out JsonElement alpha) ? alpha.GetByte() : (byte)255;
                byte r = value.TryGetProperty("R", out JsonElement red) ? red.GetByte() : (byte)0;
                byte g = value.TryGetProperty("G", out JsonElement green) ? green.GetByte() : (byte)0;
                byte b = value.TryGetProperty("B", out JsonElement blue) ? blue.GetByte() : (byte)0;
                return ToHex(Color.FromArgb(a, r, g, b));
            }
            Debug.WriteLine($"Invalid legacy {propertyName} color; using the existing default.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not parse legacy {propertyName} color: {ex.Message}");
        }
        return ToHex(fallback);
    }

    private static string ToHex(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static void FixColors(IEnumerable<HistoryItem> items)
    {
        foreach (HistoryItem item in items)
        {
            if (item.Foreground == default)
            {
                Debug.WriteLine("Fixed transparent history foreground.");
                item.Foreground = Color.FromArgb(255, 0, 0, 0);
            }
            if (item.Background == default)
            {
                Debug.WriteLine("Fixed transparent history background.");
                item.Background = Color.FromArgb(255, 255, 255, 255);
            }
        }
    }
}
