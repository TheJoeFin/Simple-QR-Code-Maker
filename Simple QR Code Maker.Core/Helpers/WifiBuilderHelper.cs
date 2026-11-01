using Simple_QR_Code_Maker.Models;
using System.Text;

namespace Simple_QR_Code_Maker.Helpers;

public static class WifiBuilderHelper
{
    private const string WifiPrefix = "WIFI:";

    public static bool IsWifi(string? text) => TryParse(text, out _);

    public static bool TryParse(string? text, out WifiBuilderState state)
    {
        state = new WifiBuilderState();

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmedText = text.Trim();
        if (!trimmedText.StartsWith(WifiPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string payload = trimmedText[WifiPrefix.Length..];
        Dictionary<string, string> parsedFields = ParseFields(payload);
        if (!parsedFields.TryGetValue("S", out string? ssid) || string.IsNullOrWhiteSpace(ssid))
            return false;

        state.Ssid = ssid;
        state.Password = parsedFields.TryGetValue("P", out string? password) ? password : string.Empty;
        state.IsHiddenNetwork = parsedFields.TryGetValue("H", out string? hiddenValue)
            && bool.TryParse(hiddenValue, out bool isHidden)
            && isHidden;

        string? authenticationText = parsedFields.TryGetValue("T", out string? typeValue) ? typeValue : null;
        if (!TryParseAuthenticationType(authenticationText, out WifiAuthenticationType authenticationType))
            return false;

        state.AuthenticationType = authenticationType;
        return true;
    }

    public static string Serialize(WifiBuilderState state)
    {
        if (string.IsNullOrWhiteSpace(state.Ssid))
            return string.Empty;

        StringBuilder builder = new();
        builder.Append(WifiPrefix);
        builder.Append("S:");
        builder.Append(EscapeValue(state.Ssid));
        builder.Append(";T:");
        builder.Append(GetAuthenticationValue(state.AuthenticationType));

        if (state.AuthenticationType != WifiAuthenticationType.None)
        {
            builder.Append(";P:");
            builder.Append(EscapeValue(state.Password));
        }

        if (state.IsHiddenNetwork)
            builder.Append(";H:true");

        builder.Append(";;");
        return builder.ToString();
    }

    public static string GetDisplayName(string? text)
    {
        return TryParse(text, out WifiBuilderState state)
            ? GetDisplayName(state)
            : "WiFi network";
    }

    public static string GetDisplayName(WifiBuilderState state)
    {
        string ssid = state.Ssid.Trim();
        return string.IsNullOrWhiteSpace(ssid) ? "WiFi network" : ssid;
    }

    private static Dictionary<string, string> ParseFields(string payload)
    {
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);

        foreach (string field in SplitUnescaped(payload, ';'))
        {
            if (string.IsNullOrEmpty(field))
                continue;

            int separatorIndex = IndexOfUnescaped(field, ':');
            if (separatorIndex <= 0)
                continue;

            string key = field[..separatorIndex].Trim();
            string value = field[(separatorIndex + 1)..];
            fields[key] = UnescapeValue(value);
        }

        return fields;
    }

    private static bool TryParseAuthenticationType(string? value, out WifiAuthenticationType authenticationType)
    {
        authenticationType = WifiAuthenticationType.WPA;

        if (string.IsNullOrWhiteSpace(value))
        {
            authenticationType = WifiAuthenticationType.None;
            return true;
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Equals("nopass", StringComparison.OrdinalIgnoreCase))
        {
            authenticationType = WifiAuthenticationType.None;
            return true;
        }

        if (normalizedValue.Equals("WEP", StringComparison.OrdinalIgnoreCase))
        {
            authenticationType = WifiAuthenticationType.WEP;
            return true;
        }

        if (normalizedValue.StartsWith("WPA", StringComparison.OrdinalIgnoreCase))
        {
            authenticationType = WifiAuthenticationType.WPA;
            return true;
        }

        return false;
    }

    private static string GetAuthenticationValue(WifiAuthenticationType authenticationType)
    {
        return authenticationType switch
        {
            WifiAuthenticationType.WEP => "WEP",
            WifiAuthenticationType.None => "nopass",
            _ => "WPA",
        };
    }

    private static string EscapeValue(string value)
    {
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            if (character is '\\' or ';' or ',' or ':' or '"')
                builder.Append('\\');

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string UnescapeValue(string value)
    {
        StringBuilder builder = new(value.Length);
        bool isEscaped = false;

        foreach (char character in value)
        {
            if (isEscaped)
            {
                builder.Append(character);
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

            builder.Append(character);
        }

        if (isEscaped)
            builder.Append('\\');

        return builder.ToString();
    }

    private static List<string> SplitUnescaped(string value, char separator)
    {
        List<string> parts = [];
        StringBuilder currentPart = new();
        bool isEscaped = false;

        foreach (char character in value)
        {
            if (isEscaped)
            {
                currentPart.Append(character);
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                currentPart.Append(character);
                isEscaped = true;
                continue;
            }

            if (character == separator)
            {
                parts.Add(currentPart.ToString());
                currentPart.Clear();
                continue;
            }

            currentPart.Append(character);
        }

        parts.Add(currentPart.ToString());
        return parts;
    }

    private static int IndexOfUnescaped(string value, char separator)
    {
        bool isEscaped = false;

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];

            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

            if (character == separator)
                return index;
        }

        return -1;
    }
}
