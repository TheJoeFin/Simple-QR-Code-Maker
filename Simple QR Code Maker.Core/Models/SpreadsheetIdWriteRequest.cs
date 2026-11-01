namespace Simple_QR_Code_Maker.Models;

public sealed class SpreadsheetIdWriteRequest
{
    public required bool FirstRowIsHeader { get; init; }

    public required int IdColumnIndex { get; init; }

    public required bool CreateIdColumn { get; init; }

    public required IReadOnlyList<SpreadsheetIdWriteRow> Rows { get; init; }
}
