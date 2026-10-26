namespace Simple_QR_Code_Maker.Contracts.Services;

public interface IJsonFileStore
{
    Task<string?> ReadAsync(string fileName);

    Task WriteAsync(string fileName, string json);
}

public interface IAtomicFileOperations
{
    Task<string?> ReadAsync(string path);

    Task WriteAsync(string path, string content);

    void Replace(string sourcePath, string destinationPath);

    void Delete(string path);
}

public interface ISettingsStore
{
    Task<string?> ReadAsync(string key);

    Task WriteAsync(string key, string value);

    Task RemoveAsync(string key);
}

public interface IRuntimeEnvironment
{
    bool IsPackaged { get; }

    string LocalApplicationDataPath { get; }
}

public interface IHistoryImageStore
{
    Task<string?> SaveCopyAsync(string sourcePath);

    Task DeleteAsync(string path);
}
