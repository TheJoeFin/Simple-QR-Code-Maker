using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Services;

namespace Simple_QR_Code_Maker.Windows.Tests.Services;

[TestClass]
public sealed class BrandServiceTests
{
    [TestMethod]
    public async Task LoadReplacesExistingItemsInPersistedOrder()
    {
        FakeBrandStore store = new() { Loaded = [new() { Name = "second" }, new() { Name = "first" }] };
        ObservableCollection<BrandItem> items = [new() { Name = "stale" }];
        await new BrandService(store).LoadAsync(items);
        CollectionAssert.AreEqual(new[] { "second", "first" }, items.Select(item => item.Name).ToArray());
        Assert.AreEqual(0, store.SaveCount);
    }

    [TestMethod]
    public async Task AddReplacesCaseInsensitiveIdentityAndMovesReplacementToFront()
    {
        FakeBrandStore store = new();
        BrandItem other = new() { Name = "other" };
        ObservableCollection<BrandItem> items = [other, new() { Name = "EXAMPLE", UrlContent = "old" }];
        BrandItem replacement = new() { Name = "example", UrlContent = "new" };
        await new BrandService(store).AddOrReplaceAndSaveAsync(items, replacement);
        Assert.HasCount(2, items);
        Assert.AreSame(replacement, items[0]);
        Assert.AreSame(other, items[1]);
        Assert.AreSame(items, store.Saved);
        Assert.AreEqual(1, store.SaveCount);
    }

    [TestMethod]
    public async Task DeleteUsesCaseInsensitiveIdentity()
    {
        FakeBrandStore store = new();
        ObservableCollection<BrandItem> items = [new() { Name = "FIRST" }, new() { Name = "second" }];
        await new BrandService(store).DeleteAndSaveAsync(items, new() { Name = "first" });
        Assert.HasCount(1, items);
        Assert.AreEqual("second", items[0].Name);
        Assert.AreEqual(1, store.SaveCount);
    }

    [TestMethod]
    public async Task DefaultSelectionClearsOtherDefaultsAndSecondSelectionTogglesOff()
    {
        FakeBrandStore store = new();
        BrandItem first = new() { Name = "first", IsDefault = true };
        BrandItem second = new() { Name = "second" };
        ObservableCollection<BrandItem> items = [first, second];
        BrandService service = new(store);
        await service.SetDefaultAndSaveAsync(items, second);
        Assert.IsFalse(first.IsDefault);
        Assert.IsTrue(second.IsDefault);
        CollectionAssert.AreEqual(new[] { first, second }, items.ToArray());
        await service.SetDefaultAndSaveAsync(items, second);
        Assert.IsFalse(items.Any(item => item.IsDefault));
        Assert.AreEqual(2, store.SaveCount);
    }

    [TestMethod]
    public async Task ReplaceMaintainsPositionAndReturnsFalseWithoutSavingWhenMissing()
    {
        FakeBrandStore store = new();
        ObservableCollection<BrandItem> items = [new() { Name = "first" }, new() { Name = "SECOND" }];
        BrandItem replacement = new() { Name = "renamed" };
        BrandService service = new(store);
        Assert.IsTrue(await service.ReplaceAndSaveAsync(items, new() { Name = "second" }, replacement));
        Assert.AreSame(replacement, items[1]);
        Assert.IsFalse(await service.ReplaceAndSaveAsync(items, new() { Name = "absent" }, new()));
        Assert.AreEqual(1, store.SaveCount);
    }

    [TestMethod]
    public async Task PersistenceFailureRemainsObservable()
    {
        FakeBrandStore store = new() { Failure = new IOException("Write failed.") };
        await Assert.ThrowsExactlyAsync<IOException>(() => new BrandService(store).AddOrReplaceAndSaveAsync([], new()));
    }

    private sealed class FakeBrandStore : IBrandStore
    {
        public ObservableCollection<BrandItem> Loaded { get; set; } = [];
        public ObservableCollection<BrandItem>? Saved { get; private set; }
        public int SaveCount { get; private set; }
        public Exception? Failure { get; init; }

        public Task<ObservableCollection<BrandItem>> LoadAsync() => Task.FromResult(Loaded);

        public Task SaveAsync(ObservableCollection<BrandItem> items)
        {
            SaveCount++;
            Saved = items;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
