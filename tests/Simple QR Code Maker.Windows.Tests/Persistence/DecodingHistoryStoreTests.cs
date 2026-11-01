using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;

namespace Simple_QR_Code_Maker.Windows.Tests.Persistence;

[TestClass]
public sealed class DecodingHistoryStoreTests
{
    private readonly MemoryJsonFileStore _files = new();
    private readonly FakeImages _images = new();

    private DecodingHistoryStore CreateStore() => new(_files, _images);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("invalid")]
    public async Task MissingEmptyMalformedOrNullReturnsEmpty(string? json)
    {
        if (json is not null)
            _files.Files["DecodingHistory.json"] = json;
        Assert.IsEmpty(await CreateStore().LoadAsync());
        Assert.IsEmpty(_files.Writes);
    }

    [TestMethod]
    public async Task AddLimitsHistoryToFiftyAndDeletesOnlyPrunedUnreferencedImages()
    {
        ObservableCollection<DecodingHistoryItem> items = new(Enumerable.Range(0, 50).Select(index => new DecodingHistoryItem
        {
            SavedImagePath = $"image-{index}.png",
            DecodedTexts = [$"text-{index}"],
            SavedDateTime = FixedTimeProvider.Now.DateTime
        }));
        DecodingHistoryStore store = CreateStore();
        await store.SaveAsync(items);
        DecodingHistoryItem added = new() { SavedImagePath = "new.png", DecodedTexts = ["new"] };
        await store.AddAndSaveAsync(items, added);
        Assert.HasCount(50, items);
        Assert.AreSame(added, items[0]);
        Assert.AreEqual("text-48", items[^1].DecodedTexts[0]);
        CollectionAssert.AreEqual(new[] { "image-49.png" }, _images.DeletedPaths);
        ObservableCollection<DecodingHistoryItem> loaded = await store.LoadAsync();
        Assert.HasCount(50, loaded);
        Assert.AreEqual("new.png", loaded[0].SavedImagePath);
    }

    [TestMethod]
    public async Task UpdateLatestPreservesIdentityMetadataAndOrderAndCopiesTextList()
    {
        DecodingHistoryItem latest = new()
        {
            SavedImagePath = "old.png",
            SourceFilePath = "source.png",
            SourceKind = DecodingSourceKind.Clipboard,
            SavedDateTime = FixedTimeProvider.Now.DateTime,
            DecodedTexts = ["old"]
        };
        DecodingHistoryItem older = new() { SavedImagePath = "older.png", DecodedTexts = ["older"] };
        ObservableCollection<DecodingHistoryItem> items = [latest, older];
        DecodingHistoryStore store = CreateStore();
        await store.SaveAsync(items);
        List<string> texts = ["new", "another"];
        await store.UpdateLatestAndSaveAsync(items, "edited.png", texts);
        texts.Clear();
        Assert.HasCount(2, items);
        Assert.AreSame(latest, items[0]);
        Assert.AreSame(older, items[1]);
        Assert.AreEqual("new-image.png", latest.SavedImagePath);
        Assert.AreEqual(FixedTimeProvider.Now.DateTime, latest.SavedDateTime);
        Assert.AreEqual(DecodingSourceKind.Clipboard, latest.SourceKind);
        Assert.AreEqual("source.png", latest.SourceFilePath);
        CollectionAssert.AreEqual(new[] { "new", "another" }, latest.DecodedTexts);
        CollectionAssert.AreEqual(new[] { "old.png" }, _images.DeletedPaths);
        CollectionAssert.AreEqual(new[] { "edited.png" }, _images.CopiedSources);
        ObservableCollection<DecodingHistoryItem> loaded = await store.LoadAsync();
        CollectionAssert.AreEqual(new[] { "new", "another" }, loaded[0].DecodedTexts);
    }

    [TestMethod]
    public async Task UpdatingEmptyHistoryDoesNothing()
    {
        await CreateStore().UpdateLatestAndSaveAsync([], "source.png", ["text"]);
        Assert.IsEmpty(_files.Writes);
        Assert.IsEmpty(_images.CopiedSources);
    }

    [TestMethod]
    public async Task FailedReplacementCopyPreservesOldImageAndMetadata()
    {
        _images.CopyFailure = new IOException("Copy failed.");
        ObservableCollection<DecodingHistoryItem> items = [new() { SavedImagePath = "old.png", DecodedTexts = ["old"] }];
        await Assert.ThrowsExactlyAsync<IOException>(() => CreateStore().UpdateLatestAndSaveAsync(items, "new.png", ["new"]));
        Assert.AreEqual("old.png", items[0].SavedImagePath);
        CollectionAssert.AreEqual(new[] { "old" }, items[0].DecodedTexts);
        Assert.IsEmpty(_files.Writes);
        Assert.IsEmpty(_images.DeletedPaths);
    }

    [TestMethod]
    public async Task MissingReplacementImageIsObservableWithoutDestroyingOldImage()
    {
        _images.CopyResult = null;
        ObservableCollection<DecodingHistoryItem> items = [new() { SavedImagePath = "old.png" }];
        await Assert.ThrowsExactlyAsync<IOException>(() => CreateStore().UpdateLatestAndSaveAsync(items, "missing.png", ["new"]));
        Assert.AreEqual("old.png", items[0].SavedImagePath);
        Assert.IsEmpty(_images.DeletedPaths);
    }

    [TestMethod]
    public async Task FailedWritePreservesOldMetadataAndImageAndRemovesUncommittedCopy()
    {
        ObservableCollection<DecodingHistoryItem> items = [new() { SavedImagePath = "old.png", DecodedTexts = ["old"] }];
        DecodingHistoryStore store = CreateStore();
        await store.SaveAsync(items);
        string previous = _files.Files["DecodingHistory.json"];
        _files.WriteFailure = new IOException("Rename failed.");
        await Assert.ThrowsExactlyAsync<IOException>(() => store.UpdateLatestAndSaveAsync(items, "new.png", ["new"]));
        Assert.AreEqual(previous, _files.Files["DecodingHistory.json"]);
        Assert.AreEqual("old.png", items[0].SavedImagePath);
        CollectionAssert.AreEqual(new[] { "old" }, items[0].DecodedTexts);
        CollectionAssert.AreEqual(new[] { "new-image.png" }, _images.DeletedPaths);
    }

    [TestMethod]
    public async Task FailedAddDoesNotTrimLiveCollectionOrDeleteImages()
    {
        ObservableCollection<DecodingHistoryItem> items = [new() { SavedImagePath = "old.png" }];
        _files.WriteFailure = new IOException("Write failed.");
        await Assert.ThrowsExactlyAsync<IOException>(() => CreateStore().AddAndSaveAsync(items, new() { SavedImagePath = "new.png" }));
        Assert.HasCount(1, items);
        Assert.AreEqual("old.png", items[0].SavedImagePath);
        Assert.IsEmpty(_images.DeletedPaths);
    }

    [TestMethod]
    public async Task RemovalPreservesSharedImagesAndClearDeletesRemainingImages()
    {
        DecodingHistoryStore store = CreateStore();
        ObservableCollection<DecodingHistoryItem> items =
        [
            new() { SavedImagePath = "shared.png" },
            new() { SavedImagePath = "SHARED.PNG" },
            new() { SavedImagePath = "removed.png" }
        ];
        await store.SaveAsync(items);
        await store.RemoveAndSaveAsync(items, items[0]);
        Assert.IsEmpty(_images.DeletedPaths);
        await store.RemoveAndSaveAsync(items, items[1]);
        Assert.HasCount(1, items);
        CollectionAssert.AreEqual(new[] { "removed.png" }, _images.DeletedPaths);
        await store.ClearAndSaveAsync(items);
        Assert.IsEmpty(items);
        Assert.IsEmpty(await store.LoadAsync());
        CollectionAssert.AreEqual(new[] { "removed.png", "SHARED.PNG" }, _images.DeletedPaths);
    }

    [TestMethod]
    public async Task ImageCleanupFailureDoesNotUndoConfirmedPersistence()
    {
        DecodingHistoryStore store = CreateStore();
        await store.SaveAsync([new() { SavedImagePath = "old.png" }]);
        _images.DeleteFailure = new IOException("Delete failed.");
        await store.SaveAsync([]);
        Assert.IsEmpty(await store.LoadAsync());
    }

    [TestMethod]
    public async Task CopyFacadeRetainsNullResultForUnavailableImages()
    {
        _images.CopyFailure = new IOException("Copy failed.");
        Assert.IsNull(await CreateStore().SaveImageCopyAsync("image.png"));
        _images.CopyFailure = null;
        _images.CopyResult = null;
        Assert.IsNull(await CreateStore().SaveImageCopyAsync("missing.png"));
    }

    [TestMethod]
    public async Task ConcurrentAddsSerializePolicyAndKeepBothEntries()
    {
        _files.WriteEntered = new();
        _files.ReleaseWrite = new();
        DecodingHistoryStore store = CreateStore();
        ObservableCollection<DecodingHistoryItem> items = [];
        Task first = store.AddAndSaveAsync(items, new() { DecodedTexts = ["first"] });
        await _files.WriteEntered.Task;
        Task second = store.AddAndSaveAsync(items, new() { DecodedTexts = ["second"] });
        _files.ReleaseWrite.SetResult();
        await Task.WhenAll(first, second);
        CollectionAssert.AreEqual(new[] { "second", "first" }, items.Select(item => item.DecodedTexts[0]).ToArray());
        Assert.HasCount(2, await store.LoadAsync());
    }

    [TestMethod]
    public async Task ClearDuringPendingAddKeepsLiveAndPersistedHistoryEmpty()
    {
        DecodingHistoryStore store = CreateStore();
        ObservableCollection<DecodingHistoryItem> items = [new() { SavedImagePath = "old.png" }];
        await store.SaveAsync(items);
        _files.WriteEntered = new();
        _files.ReleaseWrite = new();

        Task add = store.AddAndSaveAsync(items, new() { SavedImagePath = "new.png" });
        await _files.WriteEntered.Task;
        Task clear = store.ClearAndSaveAsync(items);
        _files.ReleaseWrite.SetResult();
        await Task.WhenAll(add, clear);

        Assert.IsEmpty(items);
        Assert.IsEmpty(await store.LoadAsync());
        CollectionAssert.AreEquivalent(new[] { "old.png", "new.png" }, _images.DeletedPaths);
    }

    [TestMethod]
    public async Task RemoveDuringPendingAddPreservesNewAndUnremovedEntries()
    {
        DecodingHistoryStore store = CreateStore();
        DecodingHistoryItem removed = new() { SavedImagePath = "removed.png" };
        DecodingHistoryItem retained = new() { SavedImagePath = "retained.png" };
        DecodingHistoryItem added = new() { SavedImagePath = "new.png" };
        ObservableCollection<DecodingHistoryItem> items = [removed, retained];
        await store.SaveAsync(items);
        _files.WriteEntered = new();
        _files.ReleaseWrite = new();

        Task add = store.AddAndSaveAsync(items, added);
        await _files.WriteEntered.Task;
        Task remove = store.RemoveAndSaveAsync(items, removed);
        _files.ReleaseWrite.SetResult();
        await Task.WhenAll(add, remove);

        CollectionAssert.AreEqual(new[] { added, retained }, items);
        CollectionAssert.AreEqual(new[] { "new.png", "retained.png" },
            (await store.LoadAsync()).Select(item => item.SavedImagePath).ToArray());
        CollectionAssert.AreEqual(new[] { "removed.png" }, _images.DeletedPaths);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task FailedClearOrRemovalPreservesLiveAndPersistedHistoryAndImages(bool clear)
    {
        DecodingHistoryStore store = CreateStore();
        DecodingHistoryItem item = new() { SavedImagePath = "old.png" };
        ObservableCollection<DecodingHistoryItem> items = [item];
        await store.SaveAsync(items);
        string previous = _files.Files["DecodingHistory.json"];
        _files.WriteFailure = new IOException("Write failed.");

        await Assert.ThrowsExactlyAsync<IOException>(() => clear
            ? store.ClearAndSaveAsync(items)
            : store.RemoveAndSaveAsync(items, item));

        Assert.HasCount(1, items);
        Assert.AreSame(item, items[0]);
        Assert.AreEqual(previous, _files.Files["DecodingHistory.json"]);
        Assert.IsEmpty(_images.DeletedPaths);

        _files.WriteFailure = null;
        await (clear ? store.ClearAndSaveAsync(items) : store.RemoveAndSaveAsync(items, item));
        Assert.IsEmpty(items);
        Assert.IsEmpty(await store.LoadAsync());
        CollectionAssert.AreEqual(new[] { "old.png" }, _images.DeletedPaths);
    }

    [TestMethod]
    public async Task SaveSnapshotDoesNotObserveLaterCollectionOrTextMutations()
    {
        _files.WriteEntered = new();
        _files.ReleaseWrite = new();
        DecodingHistoryItem item = new() { SavedImagePath = "old.png", DecodedTexts = ["before"] };
        ObservableCollection<DecodingHistoryItem> items = [item];
        Task save = CreateStore().SaveAsync(items);
        await _files.WriteEntered.Task;
        item.DecodedTexts[0] = "after";
        items.Clear();
        _files.ReleaseWrite.SetResult();
        await save;
        DecodingHistoryItem saved = JsonSerializer.Deserialize(
            _files.Files["DecodingHistory.json"], DecodingHistoryJsonContext.Default.ObservableCollectionDecodingHistoryItem)![0];
        Assert.AreEqual("before", saved.DecodedTexts[0]);
    }
}
