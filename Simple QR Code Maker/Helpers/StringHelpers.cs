using System.Text;
using System.Text.RegularExpressions;

namespace Simple_QR_Code_Maker.Helpers;
public static partial class StringHelpers
{
    public static readonly List<Char> ReservedChars =
        [' ', '"', '*', '/', ':', '<', '>', '?', '\\', '|', '+', ',', '.', ';', '=', '[', ']', '!', '@'];

    public static string ReplaceReservedCharacters(this string stringToClean)
    {
        StringBuilder sb = new();
        sb.Append(stringToClean);

        foreach (Char reservedChar in ReservedChars)
            sb.Replace(reservedChar, '-');

        return TrimMultiSpaceRegex().Replace(sb.ToString(), "-");
    }

    public static string ToSafeFileName(this string fullString)
    {
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
