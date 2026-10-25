namespace Simple_QR_Code_Maker.Models;

public sealed class GeneratedIdOptions
{
    public SpreadsheetGeneratedIdFormat Format { get; init; } = SpreadsheetGeneratedIdFormat.Guid;

    public int NanoIdLength { get; init; } = 12;

    public string Prefix { get; init; } = string.Empty;

    public string Suffix { get; init; } = string.Empty;
}
