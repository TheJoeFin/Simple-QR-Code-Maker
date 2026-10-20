#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Simple_QR_Code_Maker.Core.Storage;

public sealed class AtomicJsonFileStore
{
    private readonly string storageDirectory;

    public AtomicJsonFileStore(string storageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        this.storageDirectory = Path.GetFullPath(storageDirectory);
    }

    public async Task<T?> LoadAsync<T>(string relativePath, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)
    {
        string path = GetPath(relativePath);
        if (!File.Exists(path))
            return default;

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        return await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken);
    }

    public async Task SaveAsync<T>(string relativePath, T value, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        string path = GetPath(relativePath);
        string? directory = Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory!);

        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, jsonTypeInfo, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public Task DeleteAsync(string relativePath)
    {
        string path = GetPath(relativePath);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    public string GetStoragePath(string relativePath) => GetPath(relativePath);

    private string GetPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("Storage paths must be relative.", nameof(relativePath));

        string path = Path.GetFullPath(Path.Combine(storageDirectory, relativePath));
        string directoryPrefix = storageDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? storageDirectory
            : storageDirectory + Path.DirectorySeparatorChar;

        if (!path.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Storage paths cannot escape the app storage directory.", nameof(relativePath));

        return path;
    }
}
