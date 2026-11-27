using Simple_QR_Code_Maker.Contracts.Services;

namespace Simple_QR_Code_Maker.Services;

public sealed class HistoryImageStore(string folderPath, TimeProvider clock) : IHistoryImageStore
{
    public async Task<string?> SaveCopyAsync(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return null;

        Directory.CreateDirectory(folderPath);
        string path = Path.Combine(folderPath, $"{clock.GetLocalNow().Ticks}_{Guid.NewGuid():N}_decoded.png");
        try
        {
            using FileStream source = File.OpenRead(sourcePath);
            using FileStream destination = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(destination).ConfigureAwait(false);
            return path;
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    public Task DeleteAsync(string path)
    {
        // History can contain user paths from imported data. Never delete outside our owned folder.
        string root = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(path);
        if (candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            File.Delete(candidate);
        return Task.CompletedTask;
    }
}
