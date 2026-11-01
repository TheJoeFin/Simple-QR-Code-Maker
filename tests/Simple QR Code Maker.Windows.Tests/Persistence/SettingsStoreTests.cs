using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;

namespace Simple_QR_Code_Maker.Windows.Tests.Persistence;

[TestClass]
public sealed class SettingsStoreTests
{
    [TestMethod]
    public void ConfiguredSettingsAndHistoricalMigrationPathsRemainDistinct()
    {
        string root = Path.Combine(Environment.CurrentDirectory, "fake-local-data");
        Assert.AreEqual(Path.Combine(root, "Simple QR Code Maker", "ApplicationData"),
            SettingsStoragePaths.GetFolder(root, new LocalSettingsOptions()));
        Assert.AreEqual(Path.Combine(root, "Simple_QR_Code_Maker", "ApplicationData"),
            SettingsStoragePaths.GetFolder(root, new LocalSettingsOptions { ApplicationDataFolder = Path.Combine("Simple_QR_Code_Maker", "ApplicationData") }));
        Assert.AreEqual(Path.Combine(root, "Simple QR Code Maker", "ApplicationData"),
            SettingsStoragePaths.GetLegacyHistoryFolder(root));
    }

    [TestMethod]
    public async Task MissingFileHasNoValueAndFirstWriteCreatesStringDictionary()
    {
        MemoryJsonFileStore files = new();
        FileSettingsStore store = new(files);
        Assert.IsNull(await store.ReadAsync("missing"));
        await store.WriteAsync("enabled", "true");
        using JsonDocument json = JsonDocument.Parse(files.Files["LocalSettings.json"]);
        Assert.AreEqual("true", json.RootElement.GetProperty("enabled").GetString());
        Assert.AreEqual("true", await store.ReadAsync("enabled"));
    }

    [TestMethod]
    public async Task ExistingNestedJsonStringsAndUnrelatedKeysSurviveUpdatesAndRemoval()
    {
        MemoryJsonFileStore files = new();
        files.Files["custom.json"] = """{"HistoryItems":"[{\"CodesContent\":\"old\"}]","Other":"42"}""";
        FileSettingsStore store = new(files, "custom.json");
        Assert.AreEqual("""[{"CodesContent":"old"}]""", await store.ReadAsync("HistoryItems"));
        await store.WriteAsync("Added", "\"value\"");
        await store.RemoveAsync("HistoryItems");
        using JsonDocument json = JsonDocument.Parse(files.Files["custom.json"]);
        Assert.IsFalse(json.RootElement.TryGetProperty("HistoryItems", out _));
        Assert.AreEqual("42", json.RootElement.GetProperty("Other").GetString());
        Assert.AreEqual("\"value\"", json.RootElement.GetProperty("Added").GetString());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("invalid")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("""{"bad":42}""")]
    public async Task InvalidSettingsFileDoesNotSilentlyReset(string content)
    {
        MemoryJsonFileStore files = new();
        files.Files["LocalSettings.json"] = content;
        FileSettingsStore store = new(files);
        await Assert.ThrowsExactlyAsync<JsonException>(() => store.ReadAsync("key"));
        Assert.IsEmpty(files.Writes);
        Assert.AreEqual(content, files.Files["LocalSettings.json"]);
    }

    [TestMethod]
    public async Task FailedWriteDoesNotPublishUncommittedCache()
    {
        MemoryJsonFileStore files = new();
        FileSettingsStore store = new(files);
        await store.WriteAsync("key", "old");
        files.WriteFailure = new IOException("Failed replace.");
        await Assert.ThrowsExactlyAsync<IOException>(() => store.WriteAsync("key", "new"));
        Assert.AreEqual("old", await store.ReadAsync("key"));
        files.WriteFailure = null;
        await store.WriteAsync("other", "value");
        using JsonDocument json = JsonDocument.Parse(files.Files["LocalSettings.json"]);
        Assert.AreEqual("old", json.RootElement.GetProperty("key").GetString());
    }

    [TestMethod]
    public async Task ConcurrentWritesAndLegacyRemovalAreSerializedWithoutLostKeys()
    {
        MemoryJsonFileStore files = new() { WriteEntered = new(), ReleaseWrite = new() };
        files.Files["LocalSettings.json"] = """{"HistoryItems":"legacy","preserve":"true"}""";
        FileSettingsStore store = new(files);
        Task first = store.WriteAsync("first", "1");
        await files.WriteEntered.Task;
        Task second = store.WriteAsync("second", "2");
        Task remove = store.RemoveAsync("HistoryItems");
        files.ReleaseWrite.SetResult();
        await Task.WhenAll(first, second, remove);
        using JsonDocument json = JsonDocument.Parse(files.Files["LocalSettings.json"]);
        Assert.AreEqual("1", json.RootElement.GetProperty("first").GetString());
        Assert.AreEqual("2", json.RootElement.GetProperty("second").GetString());
        Assert.AreEqual("true", json.RootElement.GetProperty("preserve").GetString());
        Assert.IsFalse(json.RootElement.TryGetProperty("HistoryItems", out _));
    }

    [TestMethod]
    public async Task FolderExtensionUsesJsonSuffixAndPreservesTypedRoundTrip()
    {
        MemoryJsonFileStore files = new();
        Assert.IsNull(await files.ReadAsync<string>("missing"));
        await files.SaveAsync("preference", new[] { "one", "two" });
        Assert.IsTrue(files.Files.ContainsKey("preference.json"));
        CollectionAssert.AreEqual(new[] { "one", "two" }, await files.ReadAsync<string[]>("preference"));
        files.Files["broken.json"] = "invalid";
        await Assert.ThrowsExactlyAsync<JsonException>(() => files.ReadAsync<string>("broken"));
        files.WriteFailure = new IOException("Save failed.");
        await Assert.ThrowsExactlyAsync<IOException>(() => files.SaveAsync("preference", false));
    }

    [TestMethod]
    public async Task ContainerExtensionPolicyUsesTheProvidedStoreAndExposesInvalidData()
    {
        MemorySettingsStore settings = new();
        Assert.IsNull(await settings.ReadAsync<string>("missing"));
        await settings.SaveAsync("preference", new[] { "one", "two" });
        CollectionAssert.AreEqual(new[] { "one", "two" }, await settings.ReadAsync<string[]>("preference"));
        settings.Values["broken"] = "invalid";
        await Assert.ThrowsExactlyAsync<JsonException>(() => settings.ReadAsync<string>("broken"));
        settings.Failure = new IOException("Container unavailable.");
        await Assert.ThrowsExactlyAsync<IOException>(() => settings.SaveAsync("preference", false));
    }
}
