namespace Simple_QR_Code_Maker.Models;

public sealed class SpreadsheetGeneratedIdFormatOption
{
    public string DisplayName { get; init; } = string.Empty;

    public SpreadsheetGeneratedIdFormat Format { get; init; }

    public static IReadOnlyList<SpreadsheetGeneratedIdFormatOption> All { get; } =
    [
        new SpreadsheetGeneratedIdFormatOption
        {
            DisplayName = "GUID",
            Format = SpreadsheetGeneratedIdFormat.Guid,
        },
        new SpreadsheetGeneratedIdFormatOption
        {
            DisplayName = "GUID without dashes",
            Format = SpreadsheetGeneratedIdFormat.GuidWithoutDashes,
        },
        new SpreadsheetGeneratedIdFormatOption
        {
            DisplayName = "NanoID",
            Format = SpreadsheetGeneratedIdFormat.NanoId,
        },
    ];
}
