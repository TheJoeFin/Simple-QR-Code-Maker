using Microsoft.Extensions.Options;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Core.Contracts.Services;
using Simple_QR_Code_Maker.Core.Helpers;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Text.Json;

namespace Simple_QR_Code_Maker.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string _defaultApplicationDataFolder = "Simple QR Code Maker/ApplicationData";
    private const string _defaultLocalSettingsFile = "LocalSettings.json";

    private readonly IFileService _fileService;
    private readonly LocalSettingsOptions _options;

    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder;
    private readonly string _localSettingsFile;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        _options = options.Value;

        _applicationDataFolder = Path.Combine(_localApplicationData, _options.ApplicationDataFolder ?? _defaultApplicationDataFolder);
        _localSettingsFile = _options.LocalSettingsFile ?? _defaultLocalSettingsFile;

        _settings = new Dictionary<string, object>();
    }

    private async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _settings = await Task.Run(() => _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localSettingsFile)) ?? new Dictionary<string, object>();

            _isInitialized = true;
        }
    }

    public async Task<T?> ReadSettingAsync<T>(string key)
    {
        if (RuntimeHelper.IsMSIX)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out object? obj))
            {
                object? deserialized = JsonSerializer.Deserialize((string)obj, typeof(T), LocalSettingsJsonContext.Default);
                return deserialized is T result ? result : default;
            }
        }
        else
        {
            await InitializeAsync();

            if (_settings != null && _settings.TryGetValue(key, out object? obj))
            {
                // Settings are persisted as JSON strings, but System.Text.Json materializes the
                // dictionary's object values as JsonElement when read back from disk (it is a string
                // only for values written in the current session). Handle both.
                string? json = obj switch
                {
                    null => null,
                    string s => s,
                    JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                    JsonElement element => element.GetRawText(),
                    _ => obj.ToString(),
                };

                return json is null ? default : await Json.ToObjectAsync<T>(json);
            }
        }

        return default;
    }

    public async Task SaveSettingAsync<T>(string key, T value)
    {
        if (RuntimeHelper.IsMSIX)
        {
            ApplicationData.Current.LocalSettings.Values[key] = JsonSerializer.Serialize(value, typeof(T), LocalSettingsJsonContext.Default);
        }
        else
        {
            await InitializeAsync();

            _settings[key] = await Json.StringifyAsync(value);

            await Task.Run(() => _fileService.Save(_applicationDataFolder, _localSettingsFile, _settings));
        }
    }
}
