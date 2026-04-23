using Simple_QR_Code_Maker.Models;
using System.Security.Cryptography;

namespace Simple_QR_Code_Maker.Helpers;

public static class SpreadsheetIdGenerator
{
    private const string NanoIdAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz-";
    private const int NanoIdLength = 21;

    public static string Create(SpreadsheetGeneratedIdFormat format)
    {
        return format switch
        {
            SpreadsheetGeneratedIdFormat.Guid => Guid.NewGuid().ToString("D"),
            SpreadsheetGeneratedIdFormat.GuidWithoutDashes => Guid.NewGuid().ToString("N"),
            SpreadsheetGeneratedIdFormat.NanoId => CreateNanoId(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported generated ID format."),
        };
    }

    private static string CreateNanoId()
    {
        char[] characters = new char[NanoIdLength];
        byte[] randomBytes = new byte[NanoIdLength];
        RandomNumberGenerator.Fill(randomBytes);

        for (int index = 0; index < randomBytes.Length; index++)
            characters[index] = NanoIdAlphabet[randomBytes[index] % NanoIdAlphabet.Length];

        return new string(characters);
    }
}
