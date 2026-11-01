using Simple_QR_Code_Maker.Contracts.Services;

namespace Simple_QR_Code_Maker.Helpers;

public static class RedirectorWarningSettingsHelper
{
    public const string WarnWhenLikelyRedirectorKey = "WarnWhenLikelyRedirector";
    public const string SafeRedirectorDomainsKey = "SafeRedirectorDomains";
    public const bool DefaultWarnWhenLikelyRedirector = true;

    public static async Task<bool> ReadWarningEnabledAsync(ILocalSettingsService localSettingsService)
    {
        return await localSettingsService.ReadSettingAsync<bool?>(WarnWhenLikelyRedirectorKey)
            ?? DefaultWarnWhenLikelyRedirector;
    }

    public static async Task<IReadOnlyList<string>> ReadSafeDomainsAsync(ILocalSettingsService localSettingsService)
    {
        string[] storedSafeDomains = await localSettingsService.ReadSettingAsync<string[]>(SafeRedirectorDomainsKey) ?? [];
        return RedirectorWarningHelper.NormalizeHosts(storedSafeDomains);
    }

    public static Task SaveWarningEnabledAsync(ILocalSettingsService localSettingsService, bool value)
    {
        return localSettingsService.SaveSettingAsync(WarnWhenLikelyRedirectorKey, value);
    }

    public static Task SaveSafeDomainsAsync(ILocalSettingsService localSettingsService, IEnumerable<string> safeDomains)
    {
        string[] normalizedSafeDomains = [.. RedirectorWarningHelper.NormalizeHosts(safeDomains)];
        return localSettingsService.SaveSettingAsync(SafeRedirectorDomainsKey, normalizedSafeDomains);
    }
}
