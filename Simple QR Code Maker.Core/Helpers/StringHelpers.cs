using Simple_QR_Code_Maker.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace Simple_QR_Code_Maker.Helpers;

public static partial class StringHelpers
{
    public static readonly List<char> ReservedChars =
        [' ', '"', '*', '/', ':', '<', '>', '?', '\\', '|', '+', ',', '.', ';', '=', '[', ']', '!', '@'];

    public static string ReplaceReservedCharacters(this string stringToClean)
    {
        StringBuilder sb = new();
        foreach (char character in stringToClean)
            sb.Append(char.IsControl(character) ? '-' : character);

        foreach (char reservedChar in ReservedChars)
            sb.Replace(reservedChar, '-');

        return TrimMultiSpaceRegex().Replace(sb.ToString(), "-");
    }

    public static string ToSafeFileName(this string fullString)
    {
        ArgumentNullException.ThrowIfNull(fullString);
        if (fullString.Length == 0)
            return "QR-Code";

        if (VCardBuilderHelper.TryParse(fullString, out VCardBuilderState state))
            fullString = $"{VCardBuilderHelper.GetDisplayName(state)}-vCard";
        else if (WifiBuilderHelper.TryParse(fullString, out WifiBuilderState wifiState))
            fullString = $"{WifiBuilderHelper.GetDisplayName(wifiState)}-WiFi";
        else if (EmailBuilderHelper.TryParse(fullString, out EmailBuilderState emailState))
            fullString = $"{EmailBuilderHelper.GetDisplayName(emailState)}-Email";

        bool userStringEndsInDash = fullString[^1] == '-';
        string fileNameContent = fullString.ReplaceReservedCharacters();
        if (fileNameContent.Length > 60)
        {
            int prefixLength = char.IsHighSurrogate(fileNameContent[27]) ? 27 : 28;
            int suffixStart = fileNameContent.Length - 28;
            if (char.IsLowSurrogate(fileNameContent[suffixStart]))
                suffixStart++;

            fileNameContent = $"{fileNameContent[..prefixLength]}...{fileNameContent[suffixStart..]}";
        }

        // trim off a dash if it is the last char because of ReplaceReservedCharacters()
        if (!userStringEndsInDash && fileNameContent[^1] == '-')
            fileNameContent = fileNameContent[..^1];

        if (fileNameContent.Length == 0)
            return "QR-Code";

        return WindowsDeviceNameRegex().IsMatch(fileNameContent)
            ? fileNameContent + "-QR"
            : fileNameContent;
    }

    [GeneratedRegex(@"-+")]
    private static partial Regex TrimMultiSpaceRegex();

    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsDeviceNameRegex();
}
