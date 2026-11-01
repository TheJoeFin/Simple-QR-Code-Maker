using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;
using Windows.UI;

namespace Simple_QR_Code_Maker.Windows.Tests.Persistence;

[TestClass]
public sealed class BrandStoreTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("malformed")]
    public async Task MissingEmptyMalformedOrNullReturnsEmpty(string? json)
    {
        MemoryJsonFileStore files = new();
        if (json is not null)
            files.Files["Brands.json"] = json;
        Assert.IsEmpty(await new BrandStore(files).LoadAsync());
        Assert.IsEmpty(files.Writes);
    }

    [TestMethod]
    public async Task RoundTripPreservesOrderDefaultsNullableColorsAndDesign()
    {
        MemoryJsonFileStore files = new();
        BrandStore store = new(files);
        await store.SaveAsync(
        [
            new() { Name = "second", IsDefault = true, Foreground = Color.FromArgb(123, 4, 5, 6), FrameText = "brand label" },
            new() { Name = "first", Background = null, LogoEmoji = "🌵", UrlContent = "https://example.com" }
        ]);
        ObservableCollection<BrandItem> loaded = await store.LoadAsync();
        CollectionAssert.AreEqual(new[] { "second", "first" }, loaded.Select(item => item.Name).ToArray());
        Assert.IsTrue(loaded[0].IsDefault);
        Assert.AreEqual(Color.FromArgb(123, 4, 5, 6), loaded[0].Foreground);
        Assert.AreEqual("brand label", loaded[0].FrameText);
        Assert.IsNull(loaded[1].Background);
        Assert.AreEqual("🌵", loaded[1].LogoEmoji);
        Assert.AreEqual("https://example.com", loaded[1].UrlContent);
        Assert.AreEqual("Brands.json", files.Writes[0].Name);
    }

    [TestMethod]
    public async Task FailurePropagatesWithoutReplacingExistingFile()
    {
        MemoryJsonFileStore files = new();
        BrandStore store = new(files);
        await store.SaveAsync([new() { Name = "original" }]);
        string previous = files.Files["Brands.json"];
        files.WriteFailure = new IOException("Rename failed.");
        await Assert.ThrowsExactlyAsync<IOException>(() => store.SaveAsync([new() { Name = "replacement" }]));
        Assert.AreEqual(previous, files.Files["Brands.json"]);
    }

    [TestMethod]
    public async Task SaveFreezesItemsBeforeAsynchronousWrite()
    {
        MemoryJsonFileStore files = new() { WriteEntered = new(), ReleaseWrite = new() };
        BrandItem item = new() { Name = "original" };
        Task save = new BrandStore(files).SaveAsync([item]);
        await files.WriteEntered.Task;
        item.Name = "changed";
        files.ReleaseWrite.SetResult();
        await save;
        BrandItem saved = JsonSerializer.Deserialize(files.Files["Brands.json"], BrandJsonContext.Default.ObservableCollectionBrandItem)![0];
        Assert.AreEqual("original", saved.Name);
    }
}
