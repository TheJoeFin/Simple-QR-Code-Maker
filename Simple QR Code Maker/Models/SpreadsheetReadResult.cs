namespace Simple_QR_Code_Maker.Models;

public sealed class SpreadsheetReadResult
{
    public required IReadOnlyList<SpreadsheetSourceRow> Rows { get; init; }
}
