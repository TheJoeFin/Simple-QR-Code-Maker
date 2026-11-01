using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Simple_QR_Code_Maker.Contracts.Services;

namespace Simple_QR_Code_Maker.Services;

public sealed class AtomicJsonFileStore(string folderPath, IAtomicFileOperations files) : IJsonFileStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public AtomicJsonFileStore(string folderPath) : this(folderPath, new AtomicFileOperations())
    {
    }

    public Task<string?> ReadAsync(string fileName) => files.ReadAsync(Path.Combine(folderPath, fileName));

    public async Task WriteAsync(string fileName, string json)
    {
        string destination = Path.GetFullPath(Path.Combine(folderPath, fileName));
        SemaphoreSlim gate = Gates.GetOrAdd(destination, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        string pending = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await files.WriteAsync(pending, json).ConfigureAwait(false);
            files.Replace(pending, destination);
        }
        finally
        {
            try
            {
                files.Delete(pending);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not remove pending persistence file: {ex.Message}");
            }
            gate.Release();
        }
    }
}

public sealed class AtomicFileOperations : IAtomicFileOperations
{
    public async Task<string?> ReadAsync(string path)
    {
        try
        {
            return await File.ReadAllTextAsync(path).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public Task WriteAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
    }

    public void Replace(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath, overwrite: true);

    public void Delete(string path) => File.Delete(path);
}
