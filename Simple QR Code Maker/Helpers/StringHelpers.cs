using System.Text;
using System.Text.RegularExpressions;

namespace Simple_QR_Code_Maker.Helpers;
public static class StringHelpers
{
    public static readonly List<Char> ReservedChars = new()
    { ' ', '"', '*', '/', ':', '<', '>', '?', '\\', '|', '+', ',', '.', ';', '=', '[', ']', '!', '@' };

    public static string ReplaceReservedCharacters(this string stringToClean)
    {
        StringBuilder sb = new();
        sb.Append(stringToClean);

        foreach (Char reservedChar in ReservedChars)
            sb.Replace(reservedChar, '-');

        return Regex.Replace(sb.ToString(), @"-+", "-");
    }
}
