using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Services;

namespace Simple_QR_Code_Maker.Windows.Tests.Persistence;

[TestClass]
[TestCategory("Integration")]
public sealed class DiskStorageIntegrationTests
{
    private string _directory = null!;

    [TestInitialize]
    public void CreatePrivateDirectory()
    {
        _directory = Path.Combine(Environment.CurrentDirectory, "TestResults", "Persistence", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemovePrivateDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public async Task AtomicWritesCreateAndReplaceOnlyTheirOwnFile()
    {
        AtomicJsonFileStore store = new(_directory);
        Assert.IsNull(await store.ReadAsync("missing.json"));
        await store.WriteAsync("history.json", """{"original":true}""");
        await store.WriteAsync("history.json", """{"replacement":true}""");
        Assert.AreEqual("""{"replacement":true}""", await store.ReadAsync("history.json"));
        Assert.HasCount(1, Directory.GetFiles(_directory));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedWriteOrRenameKeepsPreviousRealFileAndRemovesPendingFile(bool failRename)
    {
        AtomicJsonFileStore healthy = new(_directory);
        await healthy.WriteAsync("history.json", "previous");
        AtomicJsonFileStore failing = new(_directory, new FailingDiskFiles(failRename));
        await Assert.ThrowsExactlyAsync<IOException>(() => failing.WriteAsync("history.json", "replacement"));
        Assert.AreEqual("previous", await healthy.ReadAsync("history.json"));
        Assert.HasCount(1, Directory.GetFiles(_directory));
        await healthy.WriteAsync("history.json", "recovered");
        Assert.AreEqual("recovered", await healthy.ReadAsync("history.json"));
    }

    [TestMethod]
    public async Task FileSettingsPersistAcrossInstancesAndMigrationKeepsOtherKeys()
    {
        AtomicJsonFileStore files = new(_directory);
        FileSettingsStore legacy = new(files);
        await legacy.WriteAsync("HistoryItems", """[{"CodesContent":"migrated","Foreground":{"A":255,"R":1,"G":2,"B":3}}]""");
        await legacy.WriteAsync("Other", "42");
        HistoryStore history = new(files, legacy, new FixedTimeProvider());
        Assert.HasCount(1, await history.LoadAsync());
        FileSettingsStore reloaded = new(new AtomicJsonFileStore(_directory));
        Assert.IsNull(await reloaded.ReadAsync("HistoryItems"));
        Assert.AreEqual("42", await reloaded.ReadAsync("Other"));
        Assert.HasCount(1, await history.LoadAsync());
    }

    [TestMethod]
    public async Task ImageCopiesAreIndependentUniqueWithFrozenClockAndOwnedDeletionOnly()
    {
        string source = Path.Combine(_directory, "source.png");
        byte[] bytes = [1, 2, 3, 4, 5];
        await File.WriteAllBytesAsync(source, bytes);
        string imagesDirectory = Path.Combine(_directory, "images");
        HistoryImageStore images = new(imagesDirectory, new FixedTimeProvider());
        string? first = await images.SaveCopyAsync(source);
        string? second = await images.SaveCopyAsync(source);
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreNotEqual(first, second);
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(first));
        await File.WriteAllBytesAsync(source, [9]);
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(first));
        await images.DeleteAsync(source);
        Assert.IsTrue(File.Exists(source));
        await images.DeleteAsync(first);
        Assert.IsFalse(File.Exists(first));
        Assert.IsTrue(File.Exists(second));
        await images.DeleteAsync(Path.Combine(imagesDirectory, "..", "source.png"));
        Assert.IsTrue(File.Exists(source));
    }

    [TestMethod]
    public async Task MissingImageDoesNotCreateStorageFolder()
    {
        string imagesDirectory = Path.Combine(_directory, "images");
        HistoryImageStore images = new(imagesDirectory, new FixedTimeProvider());
        Assert.IsNull(await images.SaveCopyAsync(string.Empty));
        Assert.IsNull(await images.SaveCopyAsync(Path.Combine(_directory, "missing.png")));
        Assert.IsFalse(Directory.Exists(imagesDirectory));
    }

    private sealed class FailingDiskFiles(bool failRename) : IAtomicFileOperations
    {
        private readonly AtomicFileOperations _inner = new();

        public Task<string?> ReadAsync(string path) => _inner.ReadAsync(path);

        public async Task WriteAsync(string path, string content)
        {
            await _inner.WriteAsync(path, content);
            if (!failRename)
                throw new IOException("Failed after writing pending data.");
        }

        public void Replace(string sourcePath, string destinationPath) =>
            throw new IOException("Failed before replacing destination.");

        public void Delete(string path) => _inner.Delete(path);
    }
}
