using Simple_QR_Code_Maker.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Simple_QR_Code_Maker.Helpers;

public static partial class QrFrameTextResolver
{
    private const int MaxSummaryLength = 44;
    private const string Ellipsis = "...";

    public static string? Resolve(
        QrFramePreset framePreset,
        QrFrameTextSource frameTextSource,
        string? manualFrameText,
        string? rawCodeText,
        QrContentKind contentKind,
        MultiLineCodeMode? multiLineCodeModeOverride = null)
    {
        if (framePreset == QrFramePreset.None || frameTextSource == QrFrameTextSource.Manual)
            return manualFrameText;

        string? summary = contentKind switch
        {
            QrContentKind.VCard => TryResolveVCardSummary(rawCodeText),
            QrContentKind.WiFi => TryResolveWifiSummary(rawCodeText),
            QrContentKind.Email => TryResolveEmailSummary(rawCodeText),
            _ => TryResolvePlainTextOrUriSummary(rawCodeText, multiLineCodeModeOverride),
        };

        return string.IsNullOrWhiteSpace(summary)
            ? manualFrameText
            : summary;
    }

    private static string? TryResolveVCardSummary(string? rawCodeText)
    {
        if (!VCardBuilderHelper.TryParse(rawCodeText, out VCardBuilderState state))
            return null;

        return NormalizeSummary(VCardBuilderHelper.GetDisplayName(state));
    }

    private static string? TryResolveWifiSummary(string? rawCodeText)
    {
        if (!WifiBuilderHelper.TryParse(rawCodeText, out WifiBuilderState state))
            return null;

        return NormalizeSummary(WifiBuilderHelper.GetDisplayName(state));
    }

    private static string? TryResolveEmailSummary(string? rawCodeText)
    {
        if (!EmailBuilderHelper.TryParse(rawCodeText, out EmailBuilderState state))
            return null;

        return NormalizeSummary(EmailBuilderHelper.GetDisplayName(state));
    }

    private static string? TryResolvePlainTextOrUriSummary(string? rawCodeText, MultiLineCodeMode? multiLineCodeModeOverride)
    {
        string trimmedText = rawCodeText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedText))
            return null;

        string? uriSummary = TryResolveUriSummary(trimmedText);
        if (!string.IsNullOrWhiteSpace(uriSummary))
            return uriSummary;

        return ResolvePlainTextSummary(trimmedText, multiLineCodeModeOverride);
    }

    private static string? TryResolveUriSummary(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri))
            return null;

        string normalizedHost = RedirectorWarningHelper.NormalizeHost(uri.Host);
        if (string.IsNullOrWhiteSpace(normalizedHost))
            return null;

        StringBuilder builder = new();
        builder.Append(normalizedHost);

        if (!uri.IsDefaultPort && uri.Port > 0)
            builder.Append(':').Append(uri.Port.ToString(CultureInfo.InvariantCulture));

        string baseLabel = builder.ToString();
        string? pathSuffix = GetUriPathSuffix(uri);
        if (!string.IsNullOrWhiteSpace(pathSuffix))
        {
            string enrichedLabel = baseLabel + pathSuffix;
            if (enrichedLabel.Length <= MaxSummaryLength)
                return enrichedLabel;
        }

        return NormalizeSummary(baseLabel);
    }

    private static string? GetUriPathSuffix(Uri uri)
    {
        string path = uri.GetComponents(UriComponents.Path, UriFormat.Unescaped).Trim('/');
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string[] segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizePathSegment)
            .Where(static segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        if (segments.Length == 0)
            return null;

        return "/" + string.Join("/", segments);
    }

    private static string NormalizePathSegment(string segment)
    {
        return CollapseWhitespace(segment).Trim();
    }

    private static string? ResolvePlainTextSummary(string text, MultiLineCodeMode? multiLineCodeModeOverride)
    {
        foreach (string line in text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            string normalizedLine = CollapseWhitespace(line).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedLine))
                return NormalizeSummary(normalizedLine);
        }

        return multiLineCodeModeOverride == MultiLineCodeMode.MultilineOneCode
            ? NormalizeSummary(CollapseWhitespace(text).Trim())
            : null;
    }

    private static string? NormalizeSummary(string? value)
    {
        string normalizedValue = CollapseWhitespace(value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
            return null;

        return Truncate(normalizedValue, MaxSummaryLength);
    }

    private static string CollapseWhitespace(string value)
    {
        return WhitespaceRegex().Replace(value, " ");
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        int trimmedLength = Math.Max(maxLength - Ellipsis.Length, 1);
        return value[..trimmedLength].TrimEnd() + Ellipsis;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
