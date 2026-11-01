using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Services;
using Simple_QR_Code_Maker.Windows.Tests.Persistence;

namespace Simple_QR_Code_Maker.Windows.Tests.Services;

[TestClass]
public sealed class RedirectorWarningSettingsTests
{
    private static LocalSettingsService CreateService() =>
        new(new FakeRuntime(), new MemorySettingsStore(), new MemorySettingsStore());

    [TestMethod]
    public async Task MissingSettingsKeepWarningEnabledAndSafeListEmpty()
    {
        LocalSettingsService service = CreateService();
        Assert.IsTrue(await RedirectorWarningSettingsHelper.ReadWarningEnabledAsync(service));
        Assert.IsEmpty(await RedirectorWarningSettingsHelper.ReadSafeDomainsAsync(service));
    }

    [TestMethod]
    public async Task ExplicitWarningPreferenceRoundTrips()
    {
        LocalSettingsService service = CreateService();
        await RedirectorWarningSettingsHelper.SaveWarningEnabledAsync(service, false);
        Assert.IsFalse(await RedirectorWarningSettingsHelper.ReadWarningEnabledAsync(service));
        await RedirectorWarningSettingsHelper.SaveWarningEnabledAsync(service, true);
        Assert.IsTrue(await RedirectorWarningSettingsHelper.ReadWarningEnabledAsync(service));
    }

    [TestMethod]
    public async Task SafeDomainsAreNormalizedDeduplicatedAndBlankValuesRemoved()
    {
        LocalSettingsService service = CreateService();
        await RedirectorWarningSettingsHelper.SaveSafeDomainsAsync(service, [" EXAMPLE.COM ", "example.com", "www.other.example.", "", " "]);
        IReadOnlyList<string> hosts = await RedirectorWarningSettingsHelper.ReadSafeDomainsAsync(service);
        CollectionAssert.AreEquivalent(new[] { "example.com", "other.example" }, hosts.ToArray());
    }

    private sealed class FakeRuntime : IRuntimeEnvironment
    {
        public bool IsPackaged => false;
        public string LocalApplicationDataPath => throw new AssertFailedException("Unexpected live path access.");
    }
}
