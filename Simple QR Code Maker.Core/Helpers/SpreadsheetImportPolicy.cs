using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Helpers;

public static class SpreadsheetImportPolicy
{
    public static List<string> CreateHeaders(IReadOnlyList<SpreadsheetSourceRow> rows, bool firstRowIsHeader)
    {
        int columnCount = rows.Count == 0 ? 0 : rows.Max(row => row.Cells.Count);
        List<string> headers = new(columnCount);
        for (int index = 0; index < columnCount; index++)
        {
            headers.Add(firstRowIsHeader && index < rows[0].Cells.Count && !string.IsNullOrWhiteSpace(rows[0].Cells[index])
                ? rows[0].Cells[index]
                : $"Column {index + 1}");
        }

        return headers;
    }

    public static List<string> SelectValues(
        IEnumerable<IReadOnlyList<string>> rows, int columnIndex, string prefix, string suffix, bool removeDuplicates)
    {
        if (columnIndex < 0)
            return [];

        IEnumerable<string> values = rows
            .Select(row => columnIndex < row.Count ? row[columnIndex] : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"{prefix}{value}{suffix}");

        return [.. removeDuplicates ? values.Distinct(StringComparer.Ordinal) : values];
    }

    public static List<string> NormalizeRow(IReadOnlyList<string> cells, int columnCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnCount);
        return [.. Enumerable.Range(0, columnCount).Select(index => GetCellValue(cells, index))];
    }

    public static string GetCellValue(IReadOnlyList<string> cells, int index)
        => index >= 0 && index < cells.Count ? cells[index] : string.Empty;

    public static bool ShouldGenerateId(IReadOnlyList<string> cells, int idColumnIndex)
        => cells.Where((value, index) => index != idColumnIndex).Any(value => !string.IsNullOrWhiteSpace(value));

    public static int FindIdColumnIndex(IReadOnlyList<string> headers)
    {
        for (int index = 0; index < headers.Count; index++)
        {
            if (string.Equals(headers[index].Trim(), "id", StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    public static void ApplyGeneratedIds(List<List<string>> rows, SpreadsheetIdWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.IdColumnIndex);
        foreach (SpreadsheetIdWriteRow row in request.Rows)
            ArgumentOutOfRangeException.ThrowIfNegative(row.SourceRowIndex);

        if (request.CreateIdColumn && request.FirstRowIsHeader)
        {
            EnsureCell(rows, 0, request.IdColumnIndex);
            rows[0][request.IdColumnIndex] = "id";
        }

        foreach (SpreadsheetIdWriteRow row in request.Rows)
        {
            EnsureCell(rows, row.SourceRowIndex, request.IdColumnIndex);
            rows[row.SourceRowIndex][request.IdColumnIndex] = row.Value;
        }
    }

    private static void EnsureCell(List<List<string>> rows, int rowIndex, int columnIndex)
    {
        while (rows.Count <= rowIndex)
            rows.Add([]);
        while (rows[rowIndex].Count <= columnIndex)
            rows[rowIndex].Add(string.Empty);
    }
}
