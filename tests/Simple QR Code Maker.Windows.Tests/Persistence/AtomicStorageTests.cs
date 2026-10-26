using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Services;

namespace Simple_QR_Code_Maker.Windows.Tests.Persistence;

[TestClass]
public sealed class AtomicStorageTests
{
    [TestMethod]
    public async Task ConcurrentStoresSerializeSameDestinationAndCommitEachSnapshotInOrder()
    {
        BlockingFiles files = new();
        string directory = Path.Combine(Environment.CurrentDirectory, "TestResults", Guid.NewGuid().ToString("N"));
        AtomicJsonFileStore firstStore = new(directory, files);
        AtomicJsonFileStore secondStore = new(directory, files);
        Task first = firstStore.WriteAsync("History.json", "first snapshot");
        await files.FirstWriteEntered.Task;
        Task second = secondStore.WriteAsync("History.json", "second snapshot");
        Assert.AreEqual(1, files.Writes);
        files.ReleaseFirstWrite.SetResult();
        await Task.WhenAll(first, second);
        CollectionAssert.AreEqual(new[] { "first snapshot", "second snapshot" }, files.Commits);
        Assert.AreEqual("second snapshot", await secondStore.ReadAsync("History.json"));
        Assert.HasCount(1, files.Contents);
    }

    [TestMethod]
    public async Task FailedWriteReleasesSerializationGateAndDoesNotPublishPartialData()
    {
        BlockingFiles files = new() { FailFirstWrite = true };
        string directory = Path.Combine(Environment.CurrentDirectory, "TestResults", Guid.NewGuid().ToString("N"));
        AtomicJsonFileStore store = new(directory, files);
        Task first = store.WriteAsync("Brands.json", "failed");
        await files.FirstWriteEntered.Task;
        Task second = store.WriteAsync("Brands.json", "successful");
        files.ReleaseFirstWrite.SetResult();
        await Assert.ThrowsExactlyAsync<IOException>(() => first);
        await second;
        CollectionAssert.AreEqual(new[] { "successful" }, files.Commits);
        Assert.AreEqual("successful", await store.ReadAsync("Brands.json"));
        Assert.HasCount(1, files.Contents);
    }

    private sealed class BlockingFiles : IAtomicFileOperations
    {
        public TaskCompletionSource FirstWriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Dictionary<string, string> Contents { get; } = [];
        public List<string> Commits { get; } = [];
        public int Writes { get; private set; }
        public bool FailFirstWrite { get; init; }

        public Task<string?> ReadAsync(string path) => Task.FromResult(Contents.GetValueOrDefault(Path.GetFullPath(path)));

        public async Task WriteAsync(string path, string content)
        {
            Writes++;
            Contents[path] = content;
            if (Writes == 1)
            {
                FirstWriteEntered.SetResult();
                await ReleaseFirstWrite.Task;
                if (FailFirstWrite)
                    throw new IOException("First write failed.");
            }
        }

        public void Replace(string sourcePath, string destinationPath)
        {
            Contents[destinationPath] = Contents[sourcePath];
            Commits.Add(Contents[sourcePath]);
            Contents.Remove(sourcePath);
        }

        public void Delete(string path) => Contents.Remove(path);
    }
}
