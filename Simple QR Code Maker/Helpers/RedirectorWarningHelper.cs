using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Helpers;

public static class RedirectorWarningHelper
{
    private static readonly HashSet<string> knownRedirectorHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "adf.ly",
        "bit.ly",
        "bl.ink",
        "getqr.com",
        "ow.ly",
        "qrcodeveloper.com",
        "qrco.de",
        "qrfy.io",
        "qrto.to",
        "scan.page",
        "scanned.page",
        "scnv.io",
        "t2mio.com",
        "tinyurl.com",
    };

    public static RedirectorUrlClassification Classify(string? text, IEnumerable<string>? safeDomains = null)
    {
        string candidate = text?.Trim() ?? string.Empty;
        if (candidate.Length == 0 || !Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
            return RedirectorUrlClassification.None;

        string normalizedHost = NormalizeHost(uri.Host);
        if (normalizedHost.Length == 0)
            return new RedirectorUrlClassification(true, false, false, string.Empty);

        bool isKnownRedirector = MatchesHostOrSubdomain(normalizedHost, knownRedirectorHosts);
        bool isSafeDomain = isKnownRedirector && MatchesHostOrSubdomain(normalizedHost, NormalizeHosts(safeDomains));
        return new RedirectorUrlClassification(true, isKnownRedirector, isSafeDomain, normalizedHost);
    }

    public static IReadOnlyList<string> NormalizeHosts(IEnumerable<string>? hosts)
    {
        if (hosts is null)
            return [];

        HashSet<string> normalizedHosts = new(StringComparer.OrdinalIgnoreCase);
        foreach (string host in hosts)
        {
            string normalizedHost = NormalizeHost(host);
            if (normalizedHost.Length > 0)
                normalizedHosts.Add(normalizedHost);
        }

        return [.. normalizedHosts.OrderBy(static host => host, StringComparer.OrdinalIgnoreCase)];
    }

    public static bool MatchesHostOrSubdomain(string normalizedHost, IEnumerable<string> hosts)
    {
        foreach (string host in hosts)
        {
            if (string.Equals(normalizedHost, host, StringComparison.OrdinalIgnoreCase)
                || normalizedHost.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return string.Empty;

        string normalizedHost = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalizedHost.StartsWith("www.", StringComparison.Ordinal))
            return normalizedHost[4..];

        return normalizedHost;
    }
}
