using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;

namespace Simple_QR_Code_Maker.Windows.Tests.Services;

[TestClass]
public sealed class HistoryServiceTests
{
    [TestMethod]
    public async Task LoadReplacesCollectionWithoutReordering()
    {
        FakeHistoryStore store = new() { Loaded = [new() { CodesContent = "newest" }, new() { CodesContent = "oldest" }] };
        ObservableCollection<HistoryItem> items = [new() { CodesContent = "stale" }];
        await new HistoryService(store).LoadAsync(items);
        CollectionAssert.AreEqual(new[] { "newest", "oldest" }, items.Select(item => item.CodesContent).ToArray());
    }

    [TestMethod]
    public async Task ContentIdentityReplacesHistoryAndMovesUpdatedDesignToFront()
    {
        FakeHistoryStore store = new();
        HistoryItem other = new() { CodesContent = "other" };
        ObservableCollection<HistoryItem> items = [other, new() { CodesContent = "same", LogoSizePercentage = 1 }];
        HistoryItem replacement = new() { CodesContent = "same", LogoSizePercentage = 30 };
        await new HistoryService(store).AddOrReplaceAndSaveAsync(items, replacement);
        Assert.HasCount(2, items);
        Assert.AreSame(replacement, items[0]);
        Assert.AreSame(other, items[1]);
        Assert.AreSame(items, store.Saved);
    }

    [TestMethod]
    public async Task ContentIdentityRemainsCaseSensitive()
    {
        FakeHistoryStore store = new();
        ObservableCollection<HistoryItem> items = [new() { CodesContent = "CASE" }];
        await new HistoryService(store).AddOrReplaceAndSaveAsync(items, new() { CodesContent = "case" });
        Assert.HasCount(2, items);
    }

    [TestMethod]
    public async Task SavePropagatesPersistenceFailure()
    {
        FakeHistoryStore store = new() { Failure = new IOException("Write failed.") };
        await Assert.ThrowsExactlyAsync<IOException>(() => new HistoryService(store).SaveAsync([]));
    }

    [TestMethod]
    public async Task ShutdownSnapshotIsIndependentOfCollectionItemsAndMutableArrays()
    {
        FakeHistoryStore store = new() { Release = new() };
        HistoryService service = new(store);
        HistoryItem current = new() { CodesContent = "current", CommonBrandCodes = ["current original"], FrameText = "frame" };
        HistoryItem previous = new() { CodesContent = "previous", CommonBrandCodes = ["previous original"] };
        ObservableCollection<HistoryItem> items = [previous, new() { CodesContent = "current" }];
        service.SaveSnapshotOnShutdown(items, current);
        Assert.IsFalse(service.PendingShutdownSave.IsCompleted);
        current.CodesContent = "mutated";
        current.FrameText = "changed";
        current.CommonBrandCodes[0] = "changed";
        previous.CommonBrandCodes[0] = "changed";
        items.Clear();
        Assert.IsNotNull(store.Saved);
        CollectionAssert.AreEqual(new[] { "current", "previous" }, store.Saved.Select(item => item.CodesContent).ToArray());
        Assert.AreEqual("frame", store.Saved[0].FrameText);
        Assert.AreEqual("current original", store.Saved[0].CommonBrandCodes[0]);
        Assert.AreEqual("previous original", store.Saved[1].CommonBrandCodes[0]);
        Assert.AreNotSame(current, store.Saved[0]);
        store.Release.SetResult();
        await service.PendingShutdownSave;
    }

    [TestMethod]
    public async Task ShutdownFailureIsExposedByRetainedTask()
    {
        HistoryService service = new(new FakeHistoryStore { Failure = new IOException("Shutdown write failed.") });
        service.SaveSnapshotOnShutdown([], new() { CodesContent = "current" });
        await Assert.ThrowsExactlyAsync<IOException>(() => service.PendingShutdownSave);
    }

    private sealed class FakeHistoryStore : IHistoryStore
    {
        public ObservableCollection<HistoryItem> Loaded { get; set; } = [];
        public ObservableCollection<HistoryItem>? Saved { get; private set; }
        public TaskCompletionSource? Release { get; init; }
        public Exception? Failure { get; init; }

        public Task<ObservableCollection<HistoryItem>> LoadAsync() => Task.FromResult(Loaded);

        public async Task SaveAsync(ObservableCollection<HistoryItem> items)
        {
            Saved = items;
            if (Release is not null)
                await Release.Task;
            if (Failure is not null)
                throw Failure;
        }
    }
}
