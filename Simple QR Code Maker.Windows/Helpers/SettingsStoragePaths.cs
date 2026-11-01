using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Helpers;

public static class SettingsStoragePaths
{
    public const string DefaultFileName = "LocalSettings.json";

    public static string GetFolder(string localApplicationData, LocalSettingsOptions options) =>
        Path.Combine(localApplicationData, options.ApplicationDataFolder ?? Path.Combine("Simple QR Code Maker", "ApplicationData"));

    public static string GetLegacyHistoryFolder(string localApplicationData) =>
        Path.Combine(localApplicationData, "Simple QR Code Maker", "ApplicationData");
}
