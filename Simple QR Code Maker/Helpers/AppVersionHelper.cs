using System.Reflection;
using Windows.ApplicationModel;

namespace Simple_QR_Code_Maker.Helpers;

public static class AppVersionHelper
{
    /// <summary>
    /// Gets the running app version, reading the package identity when packaged
    /// and falling back to the executing assembly when unpackaged.
    /// </summary>
    public static Version GetCurrentVersion()
    {
        if (RuntimeHelper.IsMSIX)
        {
            PackageVersion packageVersion = Package.Current.Id.Version;

            return new(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
        }

        return Assembly.GetExecutingAssembly().GetName().Version!;
    }

    /// <summary>
    /// Gets the running app version as a four part string, the form persisted in local settings.
    /// </summary>
    public static string GetCurrentVersionString()
    {
        Version version = GetCurrentVersion();

        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
