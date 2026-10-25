using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Helpers;

public static class DelimitedSpreadsheetHelper
{
    public static async Task<SpreadsheetReadResult> ReadAsync(string filePath, char delimiter)
    {
        string contents = await File.ReadAllTextAsync(filePath);
        List<List<string>> rows = CsvParser.Parse(contents, delimiter);
        return new SpreadsheetReadResult
        {
            Rows = [.. rows.Select((row, index) => new SpreadsheetSourceRow
            {
                SourceRowIndex = index,
                Cells = [.. row],
            })],
        };
    }

    public static async Task WriteGeneratedIdsAsync(string filePath, char delimiter, SpreadsheetIdWriteRequest request)
    {
        string contents = await File.ReadAllTextAsync(filePath);
        List<List<string>> rows = CsvParser.Parse(contents, delimiter);
        SpreadsheetImportPolicy.ApplyGeneratedIds(rows, request);
        string updatedContents = CsvParser.Serialize(rows, delimiter);
        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Unable to determine the spreadsheet file directory.");
        string tempFilePath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(tempFilePath, updatedContents);
            File.Replace(tempFilePath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }
}
