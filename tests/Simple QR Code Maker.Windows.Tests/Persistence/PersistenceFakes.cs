using Simple_QR_Code_Maker.Contracts.Services;

namespace Simple_QR_Code_Maker.Windows.Tests.Persistence;

internal sealed class MemoryJsonFileStore : IJsonFileStore
{
    public Dictionary<string, string> Files { get; } = [];
    public List<(string Name, string Json)> Writes { get; } = [];
    public Exception? ReadFailure { get; set; }
    public Exception? WriteFailure { get; set; }
    public TaskCompletionSource? WriteEntered { get; set; }
    public TaskCompletionSource? ReleaseWrite { get; set; }

    public Task<string?> ReadAsync(string fileName)
    {
        if (ReadFailure is not null)
            throw ReadFailure;
        return Task.FromResult(Files.GetValueOrDefault(fileName));
    }

    public async Task WriteAsync(string fileName, string json)
    {
        WriteEntered?.TrySetResult();
        if (ReleaseWrite is not null)
            await ReleaseWrite.Task;
        if (WriteFailure is not null)
            throw WriteFailure;
        Writes.Add((fileName, json));
        Files[fileName] = json;
    }
}

internal sealed class MemorySettingsStore : ISettingsStore
{
    public Dictionary<string, string> Values { get; } = [];
    public int Reads { get; private set; }
    public int Removals { get; private set; }
    public Exception? Failure { get; set; }
    public Exception? RemoveFailure { get; set; }

    public Task<string?> ReadAsync(string key)
    {
        Reads++;
        if (Failure is not null)
            throw Failure;
        return Task.FromResult(Values.GetValueOrDefault(key));
    }

    public Task WriteAsync(string key, string value)
    {
        if (Failure is not null)
            throw Failure;
        Values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        if (RemoveFailure is not null)
            throw RemoveFailure;
        Removals++;
        Values.Remove(key);
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider : TimeProvider
{
    public static readonly DateTimeOffset Now = new(2025, 2, 3, 4, 5, 6, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

internal sealed class FakeImages : IHistoryImageStore
{
    public string? CopyResult { get; set; } = "new-image.png";
    public Exception? CopyFailure { get; set; }
    public Exception? DeleteFailure { get; set; }
    public List<string> CopiedSources { get; } = [];
    public List<string> DeletedPaths { get; } = [];

    public Task<string?> SaveCopyAsync(string sourcePath)
    {
        CopiedSources.Add(sourcePath);
        if (CopyFailure is not null)
            throw CopyFailure;
        return Task.FromResult(CopyResult);
    }

    public Task DeleteAsync(string path)
    {
        if (DeleteFailure is not null)
            throw DeleteFailure;
        DeletedPaths.Add(path);
        return Task.CompletedTask;
    }
}
