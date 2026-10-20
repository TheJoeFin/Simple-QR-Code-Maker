#nullable enable

namespace Simple_QR_Code_Maker.Core.Storage.Models;

public sealed record LogoAssetId
{
    public required string Value { get; init; }

    public static LogoAssetId Create(string value)
    {
        if (!TryCreate(value, out LogoAssetId? assetId))
            throw new ArgumentException("Logo asset IDs must be relative paths under the app-owned logos directory.", nameof(value));

        return assetId!;
    }

    public static bool TryCreate(string? value, out LogoAssetId? assetId)
    {
        assetId = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalizedValue = value.Replace('\\', '/').Trim();
        string[] segments = normalizedValue.Split('/');
        if (!normalizedValue.StartsWith("logos/", StringComparison.Ordinal)
            || normalizedValue.Contains("//", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(segments[^1])
            || segments.Any(segment => segment is "." or "..")
            || Path.IsPathRooted(normalizedValue))
        {
            return false;
        }

        assetId = new LogoAssetId { Value = normalizedValue };
        return true;
    }
}
