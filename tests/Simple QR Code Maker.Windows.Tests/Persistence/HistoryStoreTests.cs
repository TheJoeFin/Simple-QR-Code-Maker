using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;
using Windows.UI;

namespace Simple_QR_Code_Maker.Windows.Tests.Persistence;

[TestClass]
public sealed class HistoryStoreTests
{
    private readonly MemoryJsonFileStore _files = new();
    private readonly MemorySettingsStore _legacy = new();

    private HistoryStore CreateStore() => new(_files, _legacy, new FixedTimeProvider());

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("[]")]
    [DataRow("null")]
    [DataRow("{malformed")]
    public async Task MissingEmptyOrMalformedWithoutLegacyReturnsEmpty(string? json)
    {
        if (json is not null)
            _files.Files["History.json"] = json;
        Assert.IsEmpty(await CreateStore().LoadAsync());
        Assert.AreEqual(0, _legacy.Removals);
        Assert.IsEmpty(_files.Writes);
    }

    [TestMethod]
    public async Task ExistingFilePreservesOrderContentAndAllDesignFieldsWithoutReadingLegacy()
    {
        ObservableCollection<HistoryItem> original =
        [
            new() { CodesContent = "second", SavedDateTime = FixedTimeProvider.Now.DateTime, CommonBrandCodes = ["x", "y"], ContentKind = QrContentKind.WiFi, FrameText = "label" },
            new() { CodesContent = "first", LogoEmoji = "🐱", ErrorCorrectionLevelAsString = "H" }
        ];
        HistoryStore store = CreateStore();
        await store.SaveAsync(original);
        ObservableCollection<HistoryItem> loaded = await store.LoadAsync();
        CollectionAssert.AreEqual(new[] { "second", "first" }, loaded.Select(item => item.CodesContent).ToArray());
        CollectionAssert.AreEqual(new[] { "x", "y" }, loaded[0].CommonBrandCodes);
        Assert.AreEqual(QrContentKind.WiFi, loaded[0].ContentKind);
        Assert.AreEqual("label", loaded[0].FrameText);
        Assert.AreEqual("🐱", loaded[1].LogoEmoji);
        Assert.AreEqual("H", loaded[1].ErrorCorrectionLevelAsString);
        Assert.AreEqual(0, _legacy.Reads);
    }

    [TestMethod]
    public async Task LegacyHexAndObjectColorsMigrateUsingSourceGeneratedContract()
    {
        _legacy.Values["HistoryItems"] = """
            [
              {"CodesContent":"hex","Foreground":"#FF123456","Background":"#FFABCDEF","ContentKind":"WiFi","LogoPaddingPixels":9,"FrameText":"kept"},
              {"CodesContent":"object","Foreground":{"A":128,"R":12,"G":34,"B":56},"Background":{"R":255,"G":255,"B":255}},
              {"CodesContent":"defaults","Foreground":{"A":999},"Background":"bad","LogoEmojiStyle":"unknown"}
            ]
            """;
        ObservableCollection<HistoryItem> loaded = await CreateStore().LoadAsync();
        Assert.HasCount(3, loaded);
        Assert.AreEqual(Color.FromArgb(255, 0x12, 0x34, 0x56), loaded[0].Foreground);
        Assert.AreEqual(Color.FromArgb(255, 0xAB, 0xCD, 0xEF), loaded[0].Background);
        Assert.AreEqual(QrContentKind.WiFi, loaded[0].ContentKind);
        Assert.AreEqual("kept", loaded[0].FrameText);
        Assert.AreEqual(Color.FromArgb(128, 12, 34, 56), loaded[1].Foreground);
        Assert.AreEqual(Color.FromArgb(255, 255, 255, 255), loaded[1].Background);
        Assert.AreEqual(Color.FromArgb(255, 0, 0, 0), loaded[2].Foreground);
        Assert.AreEqual(Color.FromArgb(255, 255, 255, 255), loaded[2].Background);
        Assert.IsNull(loaded[2].LogoEmojiStyle);
        Assert.AreEqual(FixedTimeProvider.Now.DateTime, loaded[2].SavedDateTime);
        Assert.AreEqual(15, loaded[2].LogoSizePercentage);
        Assert.AreEqual(4, loaded[2].LogoPaddingPixels);
        Assert.AreEqual("M", loaded[2].ErrorCorrectionLevelAsString);
        Assert.AreEqual(1, _legacy.Removals);
        Assert.IsFalse(_legacy.Values.ContainsKey("HistoryItems"));
        ObservableCollection<HistoryItem> saved = JsonSerializer.Deserialize(
            _files.Files["History.json"], HistoryJsonContext.Default.ObservableCollectionHistoryItem)!;
        Assert.HasCount(3, saved);
    }

    [TestMethod]
    public async Task InvalidScalarItemsAreSkippedButValidItemsStillMigrate()
    {
        _legacy.Values["HistoryItems"] = """
            [{"CodesContent":"valid"}, {"CodesContent":"invalid","LogoSizePercentage":"not-a-number"}, null]
            """;
        ObservableCollection<HistoryItem> loaded = await CreateStore().LoadAsync();
        Assert.HasCount(1, loaded);
        Assert.AreEqual("valid", loaded[0].CodesContent);
        Assert.AreEqual(1, _legacy.Removals);
    }

    [TestMethod]
    public async Task MissingContentAndInvalidColorsRetainLegacyDefaults()
    {
        _legacy.Values["HistoryItems"] = """[{"Foreground":"#GG000000","Background":42}]""";
        ObservableCollection<HistoryItem> loaded = await CreateStore().LoadAsync();
        Assert.HasCount(1, loaded);
        Assert.AreEqual(string.Empty, loaded[0].CodesContent);
        Assert.AreEqual(Color.FromArgb(255, 0, 0, 0), loaded[0].Foreground);
        Assert.AreEqual(Color.FromArgb(255, 255, 255, 255), loaded[0].Background);
    }

    [TestMethod]
    public async Task TransparentBlackColorsAreRepairedOnLoad()
    {
        _files.Files["History.json"] = """[{"CodesContent":"transparent","Foreground":"#00000000","Background":"#00000000"}]""";
        HistoryItem item = (await CreateStore().LoadAsync())[0];
        Assert.AreEqual(Color.FromArgb(255, 0, 0, 0), item.Foreground);
        Assert.AreEqual(Color.FromArgb(255, 255, 255, 255), item.Background);
    }

    [TestMethod]
    public async Task FailedMigrationWriteNeverClearsLegacy()
    {
        _legacy.Values["HistoryItems"] = """[{"CodesContent":"only copy"}]""";
        _files.WriteFailure = new IOException("Write failed.");
        await Assert.ThrowsExactlyAsync<IOException>(() => CreateStore().LoadAsync());
        Assert.AreEqual(0, _legacy.Removals);
        Assert.IsTrue(_legacy.Values.ContainsKey("HistoryItems"));
        Assert.IsFalse(_files.Files.ContainsKey("History.json"));
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("""[{"SavedDateTime":"invalid"}]""")]
    public async Task UnusableLegacyIsNotClearedOrReplaced(string legacy)
    {
        _legacy.Values["HistoryItems"] = legacy;
        Assert.IsEmpty(await CreateStore().LoadAsync());
        Assert.AreEqual(0, _legacy.Removals);
        Assert.IsEmpty(_files.Writes);
    }

    [TestMethod]
    public async Task CleanupFailureKeepsConfirmedPersistedHistoryUsable()
    {
        _legacy.Values["HistoryItems"] = """[{"CodesContent":"persisted"}]""";
        _legacy.RemoveFailure = new IOException("Cleanup failed.");
        Assert.HasCount(1, await CreateStore().LoadAsync());
        Assert.IsTrue(_files.Files.ContainsKey("History.json"));
        Assert.IsTrue(_legacy.Values.ContainsKey("HistoryItems"));
    }

    [TestMethod]
    public async Task SaveCapturesStateBeforeYieldAndPropagatesWriteFailure()
    {
        HistoryItem item = new() { CodesContent = "before", CommonBrandCodes = ["before"] };
        ObservableCollection<HistoryItem> items = [item];
        _files.WriteEntered = new();
        _files.ReleaseWrite = new();
        Task save = CreateStore().SaveAsync(items);
        await _files.WriteEntered.Task;
        item.CodesContent = "after";
        item.CommonBrandCodes[0] = "after";
        items.Clear();
        _files.ReleaseWrite.SetResult();
        await save;
        HistoryItem saved = JsonSerializer.Deserialize(
            _files.Files["History.json"], HistoryJsonContext.Default.ObservableCollectionHistoryItem)![0];
        Assert.AreEqual("before", saved.CodesContent);
        Assert.AreEqual("before", saved.CommonBrandCodes[0]);
        _files.WriteFailure = new IOException("Write failed.");
        await Assert.ThrowsExactlyAsync<IOException>(() => CreateStore().SaveAsync(items));
    }

    [TestMethod]
    public async Task ConcurrentLoadsMigrateOnlyOnce()
    {
        _legacy.Values["HistoryItems"] = """[{"CodesContent":"once"}]""";
        _files.WriteEntered = new();
        _files.ReleaseWrite = new();
        HistoryStore store = CreateStore();
        Task<ObservableCollection<HistoryItem>> first = store.LoadAsync();
        await _files.WriteEntered.Task;
        Task<ObservableCollection<HistoryItem>> second = store.LoadAsync();
        _files.ReleaseWrite.SetResult();
        await Task.WhenAll(first, second);
        Assert.HasCount(1, _files.Writes);
        Assert.AreEqual(1, _legacy.Removals);
    }
}
