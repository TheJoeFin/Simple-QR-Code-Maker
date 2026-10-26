using System.Text.Json;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Services;

public sealed class PackagedSettingsStore : ISettingsStore
{
    private static ContainerSettingsStore Current => new(ApplicationData.Current.LocalSettings);

    public Task<string?> ReadAsync(string key) => Current.ReadAsync(key);

    public Task WriteAsync(string key, string value) => Current.WriteAsync(key, value);

    public Task RemoveAsync(string key) => Current.RemoveAsync(key);
}

public sealed class ContainerSettingsStore(ApplicationDataContainer settings) : ISettingsStore
{
    public Task<string?> ReadAsync(string key)
    {
        if (!settings.Values.TryGetValue(key, out object? value))
            return Task.FromResult<string?>(null);

        return Task.FromResult((string?)value);
    }

    public Task WriteAsync(string key, string value)
    {
        settings.Values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        settings.Values.Remove(key);
        return Task.CompletedTask;
    }
}

public sealed class FileSettingsStore(IJsonFileStore files, string fileName = "LocalSettings.json") : ISettingsStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, string>? _settings;

    public async Task<string?> ReadAsync(string key)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Dictionary<string, string> settings = await LoadAsync().ConfigureAwait(false);
            return settings.GetValueOrDefault(key);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task WriteAsync(string key, string value) => ChangeAsync(key, value);

    public Task RemoveAsync(string key) => ChangeAsync(key, null);

    private async Task ChangeAsync(string key, string? value)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Dictionary<string, string> candidate = new(await LoadAsync().ConfigureAwait(false));
            if (value is null)
                candidate.Remove(key);
            else
                candidate[key] = value;

            string json = JsonSerializer.Serialize(candidate, HistoryJsonContext.Default.DictionaryStringString);
            await files.WriteAsync(fileName, json).ConfigureAwait(false);
            _settings = candidate;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadAsync()
    {
        if (_settings is not null)
            return _settings;

        string? json = await files.ReadAsync(fileName).ConfigureAwait(false);
        _settings = json is null
            ? []
            : JsonSerializer.Deserialize(json, HistoryJsonContext.Default.DictionaryStringString)
                ?? throw new JsonException("The local settings file must contain a settings dictionary.");
        return _settings;
    }
}
