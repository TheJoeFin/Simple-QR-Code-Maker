using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;
using Simple_QR_Code_Maker.Windows.Tests.Persistence;

namespace Simple_QR_Code_Maker.Windows.Tests.Services;

[TestClass]
public sealed class LocalSettingsServiceTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task TypedSettingsRoundTripUsesOnlySelectedStoragePolicy(bool packaged)
    {
        MemorySettingsStore packagedStore = new();
        MemorySettingsStore fileStore = new();
        LocalSettingsService service = new(new FakeRuntime(packaged), packagedStore, fileStore);
        await service.SaveSettingAsync("bool", true);
        await service.SaveSettingAsync("nullable", (bool?)false);
        await service.SaveSettingAsync("null", (int?)null);
        await service.SaveSettingAsync("int", 42);
        await service.SaveSettingAsync("double", 12.5);
        await service.SaveSettingAsync("string", "é \"quoted\" \\ value");
        await service.SaveSettingAsync("array", new[] { "one", "two" });
        await service.SaveSettingAsync("enum", QrFramePreset.None);
        Assert.IsTrue(await service.ReadSettingAsync<bool>("bool"));
        Assert.IsFalse(await service.ReadSettingAsync<bool?>("nullable"));
        Assert.IsNull(await service.ReadSettingAsync<int?>("null"));
        Assert.AreEqual(42, await service.ReadSettingAsync<int>("int"));
        Assert.AreEqual(12.5, await service.ReadSettingAsync<double>("double"));
        Assert.AreEqual("é \"quoted\" \\ value", await service.ReadSettingAsync<string>("string"));
        CollectionAssert.AreEqual(new[] { "one", "two" }, await service.ReadSettingAsync<string[]>("array"));
        Assert.AreEqual(QrFramePreset.None, await service.ReadSettingAsync<QrFramePreset>("enum"));
        MemorySettingsStore selected = packaged ? packagedStore : fileStore;
        MemorySettingsStore unused = packaged ? fileStore : packagedStore;
        Assert.HasCount(8, selected.Values);
        Assert.IsEmpty(unused.Values);
        Assert.AreEqual(0, unused.Reads);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task MissingSettingsReturnTypeDefaultsWithoutPersisting(bool packaged)
    {
        MemorySettingsStore store = new();
        LocalSettingsService service = new(new FakeRuntime(packaged), store, store);
        Assert.IsNull(await service.ReadSettingAsync<bool?>("missing"));
        Assert.IsNull(await service.ReadSettingAsync<string>("missing"));
        Assert.AreEqual(0, await service.ReadSettingAsync<int>("missing"));
        Assert.IsEmpty(store.Values);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task MalformedTypedDataIsObservableAndDoesNotFallBackToOtherPolicy(bool packaged)
    {
        MemorySettingsStore selected = new();
        selected.Values["bad"] = "not JSON";
        MemorySettingsStore other = new();
        other.Values["bad"] = "true";
        LocalSettingsService service = packaged
            ? new(new FakeRuntime(true), selected, other)
            : new(new FakeRuntime(false), other, selected);
        await Assert.ThrowsExactlyAsync<System.Text.Json.JsonException>(() => service.ReadSettingAsync<bool>("bad"));
        Assert.AreEqual(0, other.Reads);
    }

    [TestMethod]
    public async Task UnsupportedTypesFailWithoutReflectionFallback()
    {
        MemorySettingsStore store = new();
        LocalSettingsService service = new(new FakeRuntime(true), store, store);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.SaveSettingAsync("unsupported", DateTime.UnixEpoch));
        Assert.IsEmpty(store.Values);
    }

    [TestMethod]
    public async Task StorageFailuresRemainObservable()
    {
        MemorySettingsStore store = new() { Failure = new IOException("Settings unavailable.") };
        LocalSettingsService service = new(new FakeRuntime(false), store, store);
        await Assert.ThrowsExactlyAsync<IOException>(() => service.ReadSettingAsync<bool>("key"));
        await Assert.ThrowsExactlyAsync<IOException>(() => service.SaveSettingAsync("key", true));
    }

    private sealed class FakeRuntime(bool packaged) : IRuntimeEnvironment
    {
        public bool IsPackaged => packaged;
        public string LocalApplicationDataPath => throw new AssertFailedException("A typed settings policy must not access live paths.");
    }
}
