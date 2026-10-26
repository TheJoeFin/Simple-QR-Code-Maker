using System.Text.Json;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private readonly ISettingsStore _settings;

    public LocalSettingsService(IRuntimeEnvironment runtime, ISettingsStore packagedSettings, ISettingsStore fileSettings)
    {
        _settings = runtime.IsPackaged ? packagedSettings : fileSettings;
    }

    public async Task<T?> ReadSettingAsync<T>(string key)
    {
        string? json = await _settings.ReadAsync(key).ConfigureAwait(false);
        if (json is null)
            return default;

        object? value = JsonSerializer.Deserialize(json, typeof(T), LocalSettingsJsonContext.Default);
        return value is T result ? result : default;
    }

    public Task SaveSettingAsync<T>(string key, T value)
    {
        string json = JsonSerializer.Serialize(value, typeof(T), LocalSettingsJsonContext.Default);
        return _settings.WriteAsync(key, json);
    }
}
