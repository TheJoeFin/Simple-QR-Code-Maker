using System.Globalization;

using Microsoft.Windows.ApplicationModel.Resources;

namespace Simple_QR_Code_Maker.Helpers;

public static class ResourceExtensions
{
    private static readonly ResourceLoader _resourceLoader = new();

    public static string GetLocalized(this string resourceKey) => _resourceLoader.GetString(resourceKey);

    public static string FormatLocalized(this string resourceKey, params object?[] arguments)
        => string.Format(CultureInfo.CurrentCulture, resourceKey.GetLocalized(), arguments);

    public static string FormatCountLocalized(this string resourceKey, int count, params object?[] arguments)
        => (resourceKey + (count == 1 ? "_One" : "_Other")).FormatLocalized([count, .. arguments]);
}
