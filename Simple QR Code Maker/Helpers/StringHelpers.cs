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
        if (VCardBuilderHelper.TryParse(fullString, out VCardBuilderState state))
            fullString = $"{VCardBuilderHelper.GetDisplayName(state)}-vCard";

        bool userStringEndsInDash = fullString[^1] == '-';
        string fileNameContent = fullString.ReplaceReservedCharacters();
        if (fileNameContent.Length > 60)
            fileNameContent = $"{fileNameContent[..28]}...{fileNameContent[^28..]}";

        // trim off a dash if it is the last char because of ReplaceReservedCharacters()
        if (!userStringEndsInDash && fileNameContent[^1] == '-')
            fileNameContent = fileNameContent[..^1];

        return fileNameContent;
    }

    [GeneratedRegex(@"-+")]
    private static partial Regex TrimMultiSpaceRegex();
}
