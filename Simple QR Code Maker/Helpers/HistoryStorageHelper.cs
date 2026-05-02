using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Helpers;

/// <summary>
/// Helper class for managing QR code history storage and migration.
/// Handles both new JSON file format and legacy settings-based storage.
/// </summary>
public static class HistoryStorageHelper
{
    private const string HistoryFileName = "History.json";
    private const string HistoryItemsKey = "HistoryItems";

    /// <summary>
    /// Loads history from storage, attempting migration from old settings if needed.
    /// </summary>
    /// <returns>Collection of history items, or empty collection if no history exists.</returns>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer operations")]
    public static async Task<ObservableCollection<HistoryItem>> LoadHistoryAsync()
    {
        ObservableCollection<HistoryItem> historyItems = [];

        // Try to load from new file location first
        ObservableCollection<HistoryItem>? historyFromFile = await LoadHistoryFromFileAsync();

        if (historyFromFile != null && historyFromFile.Count > 0)
        {
            foreach (HistoryItem item in historyFromFile)
            {
                ValidateAndFixColors(item);
                historyItems.Add(item);
            }
            return historyItems;
        }

        // If no file exists, attempt migration from old settings
        ObservableCollection<HistoryItem>? migratedHistory = await MigrateFromOldSettingsAsync();

        if (migratedHistory != null && migratedHistory.Count > 0)
        {
            foreach (HistoryItem item in migratedHistory)
            {
                ValidateAndFixColors(item);
                historyItems.Add(item);
            }

            // Save migrated history to new file location
            await SaveHistoryAsync(historyItems);

            // Clean up old settings
            await ClearOldSettingsAsync();
        }

        return historyItems;
    }

    /// <summary>
    /// Saves history to the JSON file.
    /// </summary>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize")]
    public static async Task SaveHistoryAsync(ObservableCollection<HistoryItem> historyItems)
    {
        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            string json = JsonSerializer.Serialize(historyItems, HistoryJsonSerializerOptions.Options);

            // Write to a temp file first, then rename over the real file.
            // This prevents an empty History.json when the app exits before
            // the write completes (fire-and-forget in SaveHistoryOnShutdown).
            string tempFileName = HistoryFileName + ".tmp";
            StorageFile tempFile = await localFolder.CreateFileAsync(
                tempFileName,
                CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(tempFile, json);

            // Atomic rename – replaces the destination if it already exists.
            await tempFile.RenameAsync(HistoryFileName, NameCollisionOption.ReplaceExisting);

            Debug.WriteLine($"?? Saved {historyItems.Count} items to {HistoryFileName} ({json.Length} bytes)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? Failed to save history: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Validates and fixes transparent or invalid colors in a history item.
    /// </summary>
    private static void ValidateAndFixColors(HistoryItem item)
    {
        // Fix transparent foreground color
        if (item.Foreground.A == 0 && item.Foreground.R == 0 &&
            item.Foreground.G == 0 && item.Foreground.B == 0)
        {
            Debug.WriteLine($"?? Fixed transparent foreground for: {item.CodesContent}");
            item.Foreground = Windows.UI.Color.FromArgb(255, 0, 0, 0);
        }

        // Fix transparent background color
        if (item.Background.A == 0 && item.Background.R == 0 &&
            item.Background.G == 0 && item.Background.B == 0)
        {
            Debug.WriteLine($"?? Fixed transparent background for: {item.CodesContent}");
            item.Background = Windows.UI.Color.FromArgb(255, 255, 255, 255);
        }
    }

    /// <summary>
    /// Loads history from the JSON file.
    /// </summary>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize")]
    private static async Task<ObservableCollection<HistoryItem>?> LoadHistoryFromFileAsync()
    {
        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile historyFile = await localFolder.GetFileAsync(HistoryFileName);
            string json = await FileIO.ReadTextAsync(historyFile);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine($"?? {HistoryFileName} exists but is empty - treating as missing");
                return null;
            }

            ObservableCollection<HistoryItem>? history = JsonSerializer.Deserialize<ObservableCollection<HistoryItem>>(
                json,
                HistoryJsonSerializerOptions.Options);

            Debug.WriteLine($"? Loaded {history?.Count ?? 0} items from {HistoryFileName}");
            return history;
        }
        catch (FileNotFoundException)
        {
            Debug.WriteLine($"?? {HistoryFileName} not found - attempting migration from old settings");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? Error loading {HistoryFileName}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Migrates history from old settings storage to new format.
    /// </summary>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer operations")]
    private static async Task<ObservableCollection<HistoryItem>?> MigrateFromOldSettingsAsync()
    {
        Debug.WriteLine("?? Starting history migration from old settings...");

        try
        {
            string? historyJson = await ReadOldSettingsJsonAsync();

            if (string.IsNullOrEmpty(historyJson))
            {
                Debug.WriteLine("?? No history found in old settings - user may be new or data was cleared");
                return null;
            }

            Debug.WriteLine($"?? Parsing JSON (first 300 chars):\n{historyJson.Substring(0, Math.Min(300, historyJson.Length))}...");

            // ATTEMPT 1: Try deserializing with proper options (handles new hex format)
            ObservableCollection<HistoryItem>? migratedHistory = await TryDeserializeWithOptionsAsync(historyJson);
            if (migratedHistory != null)
            {
                return migratedHistory;
            }

            // ATTEMPT 2: Manual JSON parsing (handles old object format)
            migratedHistory = ParseHistoryManually(historyJson);
            return migratedHistory;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"?? FATAL ERROR in migration: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"   Stack: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Reads the old history JSON from settings storage.
    /// </summary>
    private static async Task<string?> ReadOldSettingsJsonAsync()
    {
        if (RuntimeHelper.IsMSIX)
        {
            return ReadFromMsixSettings();
        }
        else
        {
            return await ReadFromFileBasedSettingsAsync();
        }
    }

    /// <summary>
    /// Reads history from MSIX ApplicationData settings.
    /// </summary>
    private static string? ReadFromMsixSettings()
    {
        Debug.WriteLine("?? MSIX: Reading from ApplicationData.Current.LocalSettings");

        if (ApplicationData.Current.LocalSettings.Values.TryGetValue(HistoryItemsKey, out object? obj))
        {
            string? historyJson = obj as string;
            Debug.WriteLine($"   Found {historyJson?.Length ?? 0} characters");
            return historyJson;
        }

        Debug.WriteLine($"   Key '{HistoryItemsKey}' not found in LocalSettings");
        return null;
    }

    /// <summary>
    /// Reads history from file-based settings (non-MSIX).
    /// </summary>
    private static async Task<string?> ReadFromFileBasedSettingsAsync()
    {
        Debug.WriteLine("?? Non-MSIX: Reading from LocalSettings.json file");

        try
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Simple QR Code Maker/ApplicationData",
                "LocalSettings.json");

            Debug.WriteLine($"   Looking for: {appDataPath}");

            if (!File.Exists(appDataPath))
            {
                Debug.WriteLine("   File does not exist");
                return null;
            }

            string settingsJson = File.ReadAllText(appDataPath);
            Debug.WriteLine($"   File exists, size: {settingsJson.Length} bytes");

            Dictionary<string, string>? settingsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson);

            if (settingsDict != null && settingsDict.TryGetValue(HistoryItemsKey, out string? histVal))
            {
                Debug.WriteLine($"   Found {HistoryItemsKey}: {histVal.Length} characters");
                return histVal;
            }

            Debug.WriteLine($"   {HistoryItemsKey} key not found in settings dictionary");
            return null;
        }
        catch (Exception fileEx)
        {
            Debug.WriteLine($"? Failed to read settings file: {fileEx.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to deserialize history using proper serializer options.
    /// </summary>
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize")]
    private static async Task<ObservableCollection<HistoryItem>?> TryDeserializeWithOptionsAsync(string historyJson)
    {
        try
        {
            ObservableCollection<HistoryItem>? history = JsonSerializer.Deserialize<ObservableCollection<HistoryItem>>(
                historyJson,
                HistoryJsonSerializerOptions.Options);

            if (history != null && history.Count > 0)
            {
                Debug.WriteLine($"? Method 1 SUCCESS: Migrated {history.Count} items with proper format");
                return history;
            }
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"?? Method 1 FAILED: {ex.Message}");
            Debug.WriteLine($"   Path: {ex.Path}, Line: {ex.LineNumber}");
        }

        return null;
    }

    /// <summary>
    /// Manually parses history JSON to handle old color format.
    /// </summary>
    private static ObservableCollection<HistoryItem>? ParseHistoryManually(string historyJson)
    {
        Debug.WriteLine("?? Trying Method 2: Manual JSON parsing with old format support");

        try
        {
            using JsonDocument document = JsonDocument.Parse(historyJson);
            ObservableCollection<HistoryItem> migratedHistory = [];

            int itemCount = 0;
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                itemCount++;
                try
                {
                    HistoryItem item = ParseHistoryItem(element, itemCount);
                    migratedHistory.Add(item);

                    Debug.WriteLine($"   ? Item {itemCount} migrated: {item.CodesContent.Substring(0, Math.Min(50, item.CodesContent.Length))}");
                }
                catch (Exception itemEx)
                {
                    Debug.WriteLine($"   ? Item {itemCount} FAILED: {itemEx.Message}");
                }
            }

            if (migratedHistory.Count > 0)
            {
                Debug.WriteLine($"? Method 2 SUCCESS: Migrated {migratedHistory.Count} of {itemCount} items");
                return migratedHistory;
            }

            Debug.WriteLine($"? Method 2 FAILED: Parsed {itemCount} items but 0 were valid");
            return null;
        }
        catch (Exception fallbackEx)
        {
            Debug.WriteLine($"? Method 2 FAILED: {fallbackEx.GetType().Name}: {fallbackEx.Message}");
            Debug.WriteLine($"   Stack: {fallbackEx.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Parses a single history item from JSON element.
    /// </summary>
    private static HistoryItem ParseHistoryItem(JsonElement element, int itemIndex)
    {
        HistoryItem item = new()
        {
            CodesContent = element.TryGetProperty("CodesContent", out JsonElement contentElem)
                ? (contentElem.GetString() ?? string.Empty)
                : string.Empty,

            SavedDateTime = element.TryGetProperty("SavedDateTime", out JsonElement dateElem)
                ? dateElem.GetDateTime()
                : DateTime.Now,

            ErrorCorrectionLevelAsString = element.TryGetProperty("ErrorCorrectionLevelAsString", out JsonElement errElem)
                ? (errElem.GetString() ?? "M")
                : "M",

            LogoSizePercentage = element.TryGetProperty("LogoSizePercentage", out JsonElement sizeElem)
                ? sizeElem.GetDouble()
                : 15,

            LogoPaddingPixels = element.TryGetProperty("LogoPaddingPixels", out JsonElement padElem)
                ? padElem.GetDouble()
                : 4.0,

            LogoImagePath = element.TryGetProperty("LogoImagePath", out JsonElement logoElem)
                ? logoElem.GetString()
                : null,

            LogoEmoji = element.TryGetProperty("LogoEmoji", out JsonElement emojiElem)
                ? emojiElem.GetString()
                : null,

            LogoEmojiStyle = element.TryGetProperty("LogoEmojiStyle", out JsonElement emojiStyleElem)
                && Enum.TryParse(emojiStyleElem.GetString(), ignoreCase: true, out EmojiLogoStyle parsedEmojiStyle)
                    ? parsedEmojiStyle
                    : null
        };

        // Parse colors
        if (element.TryGetProperty("Foreground", out JsonElement fgElem))
        {
            item.Foreground = ParseColorFromJson(fgElem, "Foreground", itemIndex);
        }
        else
        {
            Debug.WriteLine($"   Item {itemIndex}: No Foreground property, using default black");
            item.Foreground = Windows.UI.Color.FromArgb(255, 0, 0, 0);
        }

        if (element.TryGetProperty("Background", out JsonElement bgElem))
        {
            item.Background = ParseColorFromJson(bgElem, "Background", itemIndex);
        }
        else
        {
            Debug.WriteLine($"   Item {itemIndex}: No Background property, using default white");
            item.Background = Windows.UI.Color.FromArgb(255, 255, 255, 255);
        }

        return item;
    }

    /// <summary>
    /// Parses a color from JSON element, handling both hex and object formats.
    /// </summary>
    private static Windows.UI.Color ParseColorFromJson(JsonElement colorElement, string colorName, int itemIndex)
    {
        try
        {
            // Handle new format: "#FF000000"
            if (colorElement.ValueKind == JsonValueKind.String)
            {
                string? hex = colorElement.GetString();
                if (!string.IsNullOrEmpty(hex) && hex.Length == 9 && hex.StartsWith("#"))
                {
                    byte a = Convert.ToByte(hex.Substring(1, 2), 16);
                    byte r = Convert.ToByte(hex.Substring(3, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(5, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(7, 2), 16);

                    Debug.WriteLine($"   Item {itemIndex} {colorName}: Parsed HEX {hex} ? ARGB({a},{r},{g},{b})");
                    return Windows.UI.Color.FromArgb(a, r, g, b);
                }
                Debug.WriteLine($"   Item {itemIndex} {colorName}: Invalid HEX format '{hex}'");
            }
            // Handle old format: {"A":255,"R":0,"G":0,"B":0}
            else if (colorElement.ValueKind == JsonValueKind.Object)
            {
                byte a = colorElement.TryGetProperty("A", out JsonElement aElem) ? aElem.GetByte() : (byte)255;
                byte r = colorElement.TryGetProperty("R", out JsonElement rElem) ? rElem.GetByte() : (byte)0;
                byte g = colorElement.TryGetProperty("G", out JsonElement gElem) ? gElem.GetByte() : (byte)0;
                byte b = colorElement.TryGetProperty("B", out JsonElement bElem) ? bElem.GetByte() : (byte)0;

                Debug.WriteLine($"   Item {itemIndex} {colorName}: Parsed OBJ ? ARGB({a},{r},{g},{b})");
                return Windows.UI.Color.FromArgb(a, r, g, b);
            }

            Debug.WriteLine($"   Item {itemIndex} {colorName}: Unexpected type {colorElement.ValueKind}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"   Item {itemIndex} {colorName}: Parse error - {ex.Message}");
        }

        // Return appropriate default color
        return GetDefaultColor(colorName, itemIndex);
    }

    /// <summary>
    /// Gets the default color for a given color type.
    /// </summary>
    private static Windows.UI.Color GetDefaultColor(string colorName, int itemIndex)
    {
        bool isBackground = colorName.Equals("Background", StringComparison.OrdinalIgnoreCase);
        byte defaultR = isBackground ? (byte)255 : (byte)0;
        byte defaultG = isBackground ? (byte)255 : (byte)0;
        byte defaultB = isBackground ? (byte)255 : (byte)0;

        Debug.WriteLine($"   Item {itemIndex} {colorName}: Using default ARGB(255,{defaultR},{defaultG},{defaultB})");
        return Windows.UI.Color.FromArgb(255, defaultR, defaultG, defaultB);
    }

    /// <summary>
    /// Clears the old settings storage after successful migration.
    /// </summary>
    private static async Task ClearOldSettingsAsync()
    {
        try
        {
            if (RuntimeHelper.IsMSIX)
            {
                ClearMsixSettings();
            }
            else
            {
                await ClearFileBasedSettingsAsync();
            }

            Debug.WriteLine("?? Migration complete!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"?? Could not clear old settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears history from MSIX settings.
    /// </summary>
    private static void ClearMsixSettings()
    {
        ApplicationData.Current.LocalSettings.Values.Remove(HistoryItemsKey);
        Debug.WriteLine("??? Cleared old MSIX settings");
    }

    /// <summary>
    /// Clears history from file-based settings.
    /// </summary>
    private static async Task ClearFileBasedSettingsAsync()
    {
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Simple QR Code Maker/ApplicationData");

        string settingsPath = Path.Combine(appDataPath, "LocalSettings.json");

        if (!File.Exists(settingsPath))
        {
            return;
        }

        string settingsJson = File.ReadAllText(settingsPath);
        Dictionary<string, string> settingsDict = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson) ?? [];
        settingsDict.Remove(HistoryItemsKey);

        string updatedJson = JsonSerializer.Serialize(settingsDict, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, updatedJson);

        Debug.WriteLine("??? Cleared old file-based settings");
    }
}
