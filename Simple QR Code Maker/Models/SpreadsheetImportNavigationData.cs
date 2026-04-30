namespace Simple_QR_Code_Maker.Models;

public class SpreadsheetImportNavigationData
{
    public required HistoryItem ReturnState { get; init; }

    public NavigationRestoreState? BackNavigationState { get; set; }

    public required IReadOnlyList<SpreadsheetSourceRow> Rows { get; init; }

    public string SourceFileName { get; init; } = string.Empty;

    public string SourceFilePath { get; init; } = string.Empty;
}
