using Simple_QR_Code_Maker.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace Simple_QR_Code_Maker.Helpers;

public static partial class UrlBuilderHelper
{
    public static IReadOnlyList<UrlBuilderLine> ParseText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [new UrlBuilderLine()];

        List<UrlBuilderLine> lines = [];
        foreach (string line in text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(ParseLine(line));
        }

        return lines.Count > 0 ? lines : [new UrlBuilderLine()];
    }

    public static UrlBuilderLine ParseLine(string? line)
    {
        string trimmedLine = line?.Trim() ?? string.Empty;
        if (trimmedLine.Length == 0)
            return new UrlBuilderLine();

        (string beforeFragment, string fragment) = SplitFirst(trimmedLine, '#');
        (string basePart, string query) = SplitFirst(beforeFragment, '?');

        UrlBuilderLine builderLine = ParseBaseParts(basePart);
        foreach (UrlBuilderQueryParameter parameter in ParseQueryParameters(query))
            builderLine.QueryParameters.Add(parameter);

        builderLine.Fragment = DecodeComponent(fragment);
        return builderLine;
    }

    public static string SerializeText(IEnumerable<UrlBuilderLine> lines)
    {
        List<string> serializedLines = [];
        foreach (UrlBuilderLine line in lines)
        {
            string serializedLine = SerializeLine(line);
            if (!string.IsNullOrWhiteSpace(serializedLine))
                serializedLines.Add(serializedLine);
        }

        return string.Join("\r\n", serializedLines);
    }

    public static string SerializeLine(UrlBuilderLine line)
    {
        StringBuilder builder = new();

        string scheme = line.Scheme.Trim().TrimEnd(':');
        string authority = line.Authority.Trim();
        string path = line.Path.Trim();
        string fragment = line.Fragment.Trim();

        if (scheme.Length > 0)
        {
            builder.Append(scheme);
            builder.Append(':');
        }

        bool hasAuthority = authority.Length > 0;
        if (hasAuthority)
        {
            if (scheme.Length > 0 || line.UsesAuthorityDelimiter)
                builder.Append("//");

            builder.Append(authority);

            if (path.Length > 0)
            {
                if (!path.StartsWith('/'))
                    builder.Append('/');

                builder.Append(EncodePath(path));
            }
        }
        else if (path.Length > 0)
        {
            builder.Append(EncodePath(path));
        }

        List<string> queryParts = [];
        foreach (UrlBuilderQueryParameter parameter in line.QueryParameters)
        {
            string name = parameter.Name.Trim();
            string value = parameter.Value.Trim();

            if (name.Length == 0 && value.Length == 0)
                continue;

            string encodedName = EncodeComponent(name);
            string encodedValue = EncodeComponent(value);
            queryParts.Add(value.Length == 0 && name.Length > 0
                ? $"{encodedName}="
                : $"{encodedName}={encodedValue}");
        }

        if (queryParts.Count > 0)
        {
            builder.Append('?');
            builder.Append(string.Join("&", queryParts));
        }

        if (fragment.Length > 0)
        {
            builder.Append('#');
            builder.Append(EncodeComponent(fragment));
        }

        return builder.ToString();
    }

    private static UrlBuilderLine ParseBaseParts(string basePart)
    {
        UrlBuilderLine builderLine = new();
        string working = basePart.Trim();

        Match schemeMatch = SchemeRegex().Match(working);
        if (schemeMatch.Success)
        {
            builderLine.Scheme = schemeMatch.Groups["scheme"].Value;
            working = working[schemeMatch.Length..];
        }

        if (working.StartsWith("//", StringComparison.Ordinal))
        {
            builderLine.UsesAuthorityDelimiter = true;
            working = working[2..];
            PopulateAuthorityAndPath(builderLine, working);
            return builderLine;
        }

        if (builderLine.Scheme.Length > 0 && working.Contains('/'))
        {
            PopulateAuthorityAndPath(builderLine, working);
            return builderLine;
        }

        if (builderLine.Scheme.Length == 0 && LooksLikeAuthority(working))
        {
            PopulateAuthorityAndPath(builderLine, working);
            return builderLine;
        }

        builderLine.Path = DecodePath(working);
        return builderLine;
    }

    private static void PopulateAuthorityAndPath(UrlBuilderLine builderLine, string text)
    {
        int slashIndex = text.IndexOf('/');
        if (slashIndex < 0)
        {
            builderLine.Authority = text.Trim();
            builderLine.Path = string.Empty;
            return;
        }

        builderLine.Authority = text[..slashIndex].Trim();
        builderLine.Path = DecodePath(text[slashIndex..]);
    }

    private static bool LooksLikeAuthority(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains(' '))
            return false;

        int slashIndex = text.IndexOf('/');
        string head = slashIndex >= 0 ? text[..slashIndex] : text;
        return head.Contains('.') || head.Contains(':');
    }

    private static List<UrlBuilderQueryParameter> ParseQueryParameters(string query)
    {
        List<UrlBuilderQueryParameter> parameters = [];
        if (string.IsNullOrEmpty(query))
            return parameters;

        foreach (string part in query.Split('&', StringSplitOptions.None))
        {
            if (part.Length == 0)
                continue;

            int equalsIndex = part.IndexOf('=');
            if (equalsIndex < 0)
            {
                parameters.Add(new UrlBuilderQueryParameter(DecodeComponent(part), string.Empty));
                continue;
            }

            string name = part[..equalsIndex];
            string value = part[(equalsIndex + 1)..];
            parameters.Add(new UrlBuilderQueryParameter(DecodeComponent(name), DecodeComponent(value)));
        }

        return parameters;
    }

    private static string DecodePath(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string[] segments = value.Split('/', StringSplitOptions.None);
        List<string> decodedSegments = [];
        foreach (string segment in segments)
            decodedSegments.Add(DecodeComponent(segment));

        return string.Join("/", decodedSegments);
    }

    private static string EncodePath(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string[] segments = value.Split('/', StringSplitOptions.None);
        List<string> encodedSegments = [];
        foreach (string segment in segments)
            encodedSegments.Add(EncodeComponent(segment));

        return string.Join("/", encodedSegments);
    }

    private static string EncodeComponent(string value) => Uri.EscapeDataString(value);

    private static string DecodeComponent(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private static (string Left, string Right) SplitFirst(string value, char separator)
    {
        int separatorIndex = value.IndexOf(separator);
        if (separatorIndex < 0)
            return (value, string.Empty);

        return (value[..separatorIndex], value[(separatorIndex + 1)..]);
    }

    [GeneratedRegex(@"^(?<scheme>[A-Za-z][A-Za-z0-9+\.-]*):")]
    private static partial Regex SchemeRegex();
}
