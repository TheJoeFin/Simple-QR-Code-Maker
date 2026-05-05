using Simple_QR_Code_Maker.Models;
using System.Security.Cryptography;

namespace Simple_QR_Code_Maker.Helpers;

public static class GeneratedIdGenerator
{
    private const string NanoIdAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public const int DefaultNanoIdLength = 12;

    public static string Create(GeneratedIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string coreValue = options.Format switch
        {
            SpreadsheetGeneratedIdFormat.Guid => Guid.NewGuid().ToString("D"),
            SpreadsheetGeneratedIdFormat.GuidWithoutDashes => Guid.NewGuid().ToString("N"),
            SpreadsheetGeneratedIdFormat.NanoId => CreateNanoId(options.NanoIdLength),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Format, "Unsupported generated ID format."),
        };

        return string.Concat(options.Prefix, coreValue, options.Suffix);
    }

    public static IReadOnlyList<string> CreateMany(int count, GeneratedIdOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentNullException.ThrowIfNull(options);

        List<string> values = new(count);
        for (int index = 0; index < count; index++)
            values.Add(Create(options));

        return values;
    }

    public static bool TryGetWholeNumber(double value, out int result)
    {
        result = 0;

        if (double.IsNaN(value) || double.IsInfinity(value))
            return false;

        if (value < 1 || value > int.MaxValue)
            return false;

        if (Math.Floor(value) != value)
            return false;

        result = Convert.ToInt32(value);
        return true;
    }

    private static string CreateNanoId(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        char[] characters = new char[length];
        for (int index = 0; index < characters.Length; index++)
            characters[index] = NanoIdAlphabet[RandomNumberGenerator.GetInt32(NanoIdAlphabet.Length)];

        return new string(characters);
    }
}
