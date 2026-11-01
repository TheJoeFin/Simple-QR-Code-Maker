namespace Simple_QR_Code_Maker.Helpers;

public static class QrDocumentHelper
{
    public static IEnumerable<string> EnumerateTexts(string text, MultiLineCodeMode mode)
    {
        if (mode != MultiLineCodeMode.OneLineOneCode)
        {
            if (!string.IsNullOrWhiteSpace(text))
                yield return text.Trim();
            yield break;
        }

        foreach (string line in text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            if (!string.IsNullOrWhiteSpace(line))
                yield return line.Trim();
        }
    }

    public static QrContentKind ClassifyContent(string? text)
        => VCardBuilderHelper.IsVCard(text) ? QrContentKind.VCard
        : WifiBuilderHelper.IsWifi(text) ? QrContentKind.WiFi
        : EmailBuilderHelper.IsEmail(text) ? QrContentKind.Email
        : QrContentKind.PlainText;

    public static bool MatchesBrandUrl(string content, string brandUrl)
    {
        string normalizedContent = NormalizeBrandUrl(content);
        string pattern = NormalizeBrandUrl(brandUrl).TrimEnd('/');
        int authorityEnd = normalizedContent.IndexOfAny(['/', '?', '#']);
        ReadOnlySpan<char> authority = authorityEnd >= 0 ? normalizedContent.AsSpan(0, authorityEnd) : normalizedContent.AsSpan();
        if (authority.Contains('@'))
            return false;
        if (pattern.Length == 0 || !normalizedContent.StartsWith(pattern, StringComparison.Ordinal))
            return false;
        return normalizedContent.Length == pattern.Length
            || normalizedContent[pattern.Length] is '/' or '?' or '#' or ':';
    }

    private static string NormalizeBrandUrl(string url)
    {
        string normalized = url.Trim().ToLowerInvariant();
        if (normalized.StartsWith("https://", StringComparison.Ordinal))
            normalized = normalized[8..];
        else if (normalized.StartsWith("http://", StringComparison.Ordinal))
            normalized = normalized[7..];
        if (normalized.StartsWith("www.", StringComparison.Ordinal))
            normalized = normalized[4..];
        return normalized;
    }
}
