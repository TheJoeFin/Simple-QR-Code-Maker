using Simple_QR_Code_Maker.Models;
using System.Text;

namespace Simple_QR_Code_Maker.Helpers;

public static class VCardBuilderHelper
{
    private const string BeginLine = "BEGIN:VCARD";
    private const string EndLine = "END:VCARD";
    private const string VersionLine = "VERSION:3.0";

    public static bool IsVCard(string? text) => TryParse(text, out _);

    public static bool TryParse(string? text, out VCardBuilderState state)
    {
        state = new VCardBuilderState();

        if (string.IsNullOrWhiteSpace(text))
            return false;

        List<string> unfoldedLines = UnfoldLines(text);
        if (unfoldedLines.Count < 3)
            return false;

        if (!unfoldedLines.Any(static line => line.Equals(BeginLine, StringComparison.OrdinalIgnoreCase))
            || !unfoldedLines.Any(static line => line.Equals(EndLine, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        foreach (string line in unfoldedLines)
        {
            if (line.Equals(BeginLine, StringComparison.OrdinalIgnoreCase)
                || line.Equals(EndLine, StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("VERSION:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
                continue;

            string propertyPart = line[..separatorIndex];
            string valuePart = line[(separatorIndex + 1)..];
            string propertyName = GetPropertyName(propertyPart);
            string propertyValue = UnescapeValue(valuePart);

            switch (propertyName)
            {
                case "FN":
                    state.FormattedName = propertyValue;
                    break;
                case "N":
                    ApplyStructuredName(state, propertyValue);
                    break;
                case "ORG":
                    state.Organization = propertyValue;
                    break;
                case "TITLE":
                    state.Title = propertyValue;
                    break;
                case "TEL":
                    ApplyPhoneValue(state, propertyPart, propertyValue);
                    break;
                case "EMAIL":
                    state.Email = propertyValue;
                    break;
                case "URL":
                    state.Url = propertyValue;
                    break;
                case "NICKNAME":
                    state.Nickname = propertyValue;
                    break;
                case "BDAY":
                    state.Birthday = propertyValue;
                    break;
                case "ADR":
                    ApplyAddress(state, propertyValue);
                    break;
                case "NOTE":
                    state.Note = propertyValue;
                    break;
            }
        }

        return !string.IsNullOrWhiteSpace(GetDisplayName(state));
    }

    public static string Serialize(VCardBuilderState state)
    {
        string formattedName = GetDisplayName(state);
        if (string.IsNullOrWhiteSpace(formattedName))
            return string.Empty;

        List<string> lines =
        [
            BeginLine,
            VersionLine,
            $"FN:{EscapeValue(formattedName)}",
        ];

        if (HasStructuredName(state))
        {
            lines.Add(string.Join(";",
            [
                "N:" + EscapeValue(state.LastName.Trim()),
                EscapeValue(state.FirstName.Trim()),
                EscapeValue(state.MiddleName.Trim()),
                EscapeValue(state.Prefix.Trim()),
                EscapeValue(state.Suffix.Trim())
            ]));
        }

        AppendLine(lines, "ORG", state.Organization);
        AppendLine(lines, "TITLE", state.Title);
        AppendTypedLine(lines, "TEL", "CELL", state.MobilePhone);
        AppendTypedLine(lines, "TEL", "WORK", state.WorkPhone);
        AppendTypedLine(lines, "TEL", "HOME", state.HomePhone);
        AppendTypedLine(lines, "EMAIL;TYPE=INTERNET", null, state.Email);
        AppendLine(lines, "URL", state.Url);
        AppendLine(lines, "NICKNAME", state.Nickname);
        AppendLine(lines, "BDAY", state.Birthday);

        if (HasAddress(state))
        {
            string addressValue = string.Join(";",
            [
                string.Empty,
                string.Empty,
                EscapeValue(state.StreetAddress.Trim()),
                EscapeValue(state.Locality.Trim()),
                EscapeValue(state.Region.Trim()),
                EscapeValue(state.PostalCode.Trim()),
                EscapeValue(state.Country.Trim())
            ]);
            lines.Add($"ADR;TYPE=HOME:{addressValue}");
        }

        AppendLine(lines, "NOTE", state.Note);
        lines.Add(EndLine);
        return string.Join("\r\n", lines);
    }

    public static string GetDisplayName(string? text)
    {
        return TryParse(text, out VCardBuilderState state)
            ? GetDisplayName(state)
            : "vCard contact";
    }

    public static string GetDisplayName(VCardBuilderState state)
    {
        string formattedName = state.FormattedName.Trim();
        if (!string.IsNullOrWhiteSpace(formattedName))
            return formattedName;

        string[] orderedNameParts =
        [
            state.Prefix.Trim(),
            state.FirstName.Trim(),
            state.MiddleName.Trim(),
            state.LastName.Trim(),
            state.Suffix.Trim(),
        ];

        string assembledName = string.Join(" ", orderedNameParts.Where(static part => !string.IsNullOrWhiteSpace(part))).Trim();
        if (!string.IsNullOrWhiteSpace(assembledName))
            return assembledName;

        if (!string.IsNullOrWhiteSpace(state.Organization))
            return state.Organization.Trim();

        if (!string.IsNullOrWhiteSpace(state.Email))
            return state.Email.Trim();

        if (!string.IsNullOrWhiteSpace(state.MobilePhone))
            return state.MobilePhone.Trim();

        return string.Empty;
    }

    private static void AppendLine(ICollection<string> lines, string propertyName, string value)
    {
        string trimmedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
            return;

        lines.Add($"{propertyName}:{EscapeValue(trimmedValue)}");
    }

    private static void AppendTypedLine(ICollection<string> lines, string propertyName, string? typeName, string value)
    {
        string trimmedValue = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
            return;

        string property = string.IsNullOrWhiteSpace(typeName)
            ? propertyName
            : $"{propertyName};TYPE={typeName}";
        lines.Add($"{property}:{EscapeValue(trimmedValue)}");
    }

    private static bool HasStructuredName(VCardBuilderState state)
    {
        return !string.IsNullOrWhiteSpace(state.FirstName)
            || !string.IsNullOrWhiteSpace(state.LastName)
            || !string.IsNullOrWhiteSpace(state.MiddleName)
            || !string.IsNullOrWhiteSpace(state.Prefix)
            || !string.IsNullOrWhiteSpace(state.Suffix);
    }

    private static bool HasAddress(VCardBuilderState state)
    {
        return !string.IsNullOrWhiteSpace(state.StreetAddress)
            || !string.IsNullOrWhiteSpace(state.Locality)
            || !string.IsNullOrWhiteSpace(state.Region)
            || !string.IsNullOrWhiteSpace(state.PostalCode)
            || !string.IsNullOrWhiteSpace(state.Country);
    }

    private static List<string> UnfoldLines(string text)
    {
        List<string> lines = [];
        foreach (string rawLine in text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(rawLine))
                continue;

            if ((rawLine.StartsWith(' ') || rawLine.StartsWith('\t')) && lines.Count > 0)
            {
                lines[^1] += rawLine[1..];
                continue;
            }

            lines.Add(rawLine.TrimEnd());
        }

        return lines;
    }

    private static string GetPropertyName(string propertyPart)
    {
        int parameterSeparatorIndex = propertyPart.IndexOf(';');
        string propertyName = parameterSeparatorIndex >= 0
            ? propertyPart[..parameterSeparatorIndex]
            : propertyPart;

        return propertyName.Trim().ToUpperInvariant();
    }

    private static void ApplyStructuredName(VCardBuilderState state, string value)
    {
        string[] parts = value.Split(';', StringSplitOptions.None);
        state.LastName = GetPart(parts, 0);
        state.FirstName = GetPart(parts, 1);
        state.MiddleName = GetPart(parts, 2);
        state.Prefix = GetPart(parts, 3);
        state.Suffix = GetPart(parts, 4);
    }

    private static void ApplyPhoneValue(VCardBuilderState state, string propertyPart, string value)
    {
        string normalizedProperty = propertyPart.ToUpperInvariant();
        if (normalizedProperty.Contains("CELL", StringComparison.Ordinal)
            || normalizedProperty.Contains("MOBILE", StringComparison.Ordinal))
        {
            state.MobilePhone = value;
            return;
        }

        if (normalizedProperty.Contains("WORK", StringComparison.Ordinal))
        {
            state.WorkPhone = value;
            return;
        }

        if (normalizedProperty.Contains("HOME", StringComparison.Ordinal))
        {
            state.HomePhone = value;
            return;
        }

        if (string.IsNullOrWhiteSpace(state.MobilePhone))
            state.MobilePhone = value;
        else if (string.IsNullOrWhiteSpace(state.WorkPhone))
            state.WorkPhone = value;
        else if (string.IsNullOrWhiteSpace(state.HomePhone))
            state.HomePhone = value;
    }

    private static void ApplyAddress(VCardBuilderState state, string value)
    {
        string[] parts = value.Split(';', StringSplitOptions.None);
        state.StreetAddress = GetPart(parts, 2);
        state.Locality = GetPart(parts, 3);
        state.Region = GetPart(parts, 4);
        state.PostalCode = GetPart(parts, 5);
        state.Country = GetPart(parts, 6);
    }

    private static string EscapeValue(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case ';':
                    builder.Append(@"\;");
                    break;
                case ',':
                    builder.Append(@"\,");
                    break;
                case '\r':
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string UnescapeValue(string value)
    {
        StringBuilder builder = new(value.Length);

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '\\' && index + 1 < value.Length)
            {
                char escapedCharacter = value[index + 1];
                switch (escapedCharacter)
                {
                    case 'n':
                    case 'N':
                        builder.Append('\n');
                        index++;
                        continue;
                    case '\\':
                    case ';':
                    case ',':
                        builder.Append(escapedCharacter);
                        index++;
                        continue;
                }
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string GetPart(IReadOnlyList<string> parts, int index)
    {
        return index < parts.Count ? UnescapeValue(parts[index]) : string.Empty;
    }
}
