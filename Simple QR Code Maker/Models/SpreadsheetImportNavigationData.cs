namespace Simple_QR_Code_Maker.Models;

public class SpreadsheetImportNavigationData
{
    public required HistoryItem ReturnState { get; init; }

    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    public string SourceFileName { get; init; } = string.Empty;
}
