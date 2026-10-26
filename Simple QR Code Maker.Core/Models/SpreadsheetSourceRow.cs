namespace Simple_QR_Code_Maker.Models;

public sealed class SpreadsheetSourceRow
{
    public required int SourceRowIndex { get; init; }

    public required IReadOnlyList<string> Cells { get; init; }
}
