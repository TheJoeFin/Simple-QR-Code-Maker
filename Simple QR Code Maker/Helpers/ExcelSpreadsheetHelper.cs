using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Simple_QR_Code_Maker.Helpers;

public static class ExcelSpreadsheetHelper
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".tsv",
        ".xlsx",
        ".xls",
    };

    private static readonly object Missing = Type.Missing;
    private static bool? _cachedAvailability;

    public static Task<bool> CheckIsAvailableAsync()
    {
        if (_cachedAvailability.HasValue)
            return Task.FromResult(_cachedAvailability.Value);

        return RunStaAsync(() =>
        {
            try
            {
                _cachedAvailability = Type.GetTypeFromProgID("Excel.Application") is not null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Excel availability check failed: {ex.Message}");
                _cachedAvailability = false;
            }

            return _cachedAvailability.Value;
        });
    }

    public static async Task<List<List<string>>> ReadRowsAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A spreadsheet file path is required.", nameof(filePath));

        string extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
            throw new NotSupportedException($"Unsupported spreadsheet file type: {extension}");

        if (!await CheckIsAvailableAsync())
            throw new InvalidOperationException("Microsoft Excel is not installed.");

        return extension.ToLowerInvariant() switch
        {
            ".csv" => await ReadDelimitedRowsAsync(filePath, ','),
            ".tsv" => await ReadDelimitedRowsAsync(filePath, '\t'),
            ".xlsx" or ".xls" => await RunStaAsync(() => ReadWorkbookRowsInternal(filePath)),
            _ => throw new NotSupportedException($"Unsupported spreadsheet file type: {extension}"),
        };
    }

    private static async Task<List<List<string>>> ReadDelimitedRowsAsync(string filePath, char delimiter)
    {
        string contents = await File.ReadAllTextAsync(filePath);
        return CsvParser.Parse(contents, delimiter);
    }

    private static List<List<string>> ReadWorkbookRowsInternal(string filePath)
    {
        object? application = null;
        object? workbooks = null;
        object? workbook = null;
        object? worksheets = null;
        object? worksheet = null;
        object? usedRange = null;
        object? rowsCollection = null;
        object? columnsCollection = null;
        object? cellsCollection = null;

        try
        {
            Type excelType = Type.GetTypeFromProgID("Excel.Application")
                ?? throw new InvalidOperationException("Microsoft Excel is not installed.");

            application = Activator.CreateInstance(excelType)
                ?? throw new InvalidOperationException("Unable to start Microsoft Excel.");

            SetProperty(application, "Visible", false);
            SetProperty(application, "DisplayAlerts", false);
            SetProperty(application, "ScreenUpdating", false);
            SetProperty(application, "EnableEvents", false);

            workbooks = GetProperty(application, "Workbooks");
            workbook = InvokeMethod(
                workbooks,
                "Open",
                filePath,
                0,
                true,
                Missing,
                Missing,
                Missing,
                true,
                Missing,
                Missing,
                Missing,
                Missing,
                Missing,
                Missing,
                true,
                Missing);

            worksheets = GetProperty(workbook, "Worksheets");
            if (worksheets is null)
                return [];
            worksheet = GetFirstWorksheetWithData(worksheets)
                ?? throw new InvalidOperationException("Unable to find a worksheet with data.");

            usedRange = GetProperty(worksheet, "UsedRange");
            rowsCollection = GetProperty(usedRange, "Rows");
            columnsCollection = GetProperty(usedRange, "Columns");
            cellsCollection = GetProperty(usedRange, "Cells");

            int rowCount = Convert.ToInt32(GetProperty(rowsCollection, "Count"));
            int columnCount = Convert.ToInt32(GetProperty(columnsCollection, "Count"));

            if (rowCount <= 0 || columnCount <= 0)
                return [];

            List<List<string>> rows = [with(rowCount)];

            if (cellsCollection is not null)
            {
                for (int row = 1; row <= rowCount; row++)
                {
                    List<string> currentRow = [with(columnCount)];
                    bool hasContent = false;

                    for (int column = 1; column <= columnCount; column++)
                    {
                        string cellText = GetDisplayedCellText(cellsCollection, row, column);
                        if (!string.IsNullOrWhiteSpace(cellText))
                            hasContent = true;

                        currentRow.Add(cellText);
                    }

                    if (hasContent)
                        rows.Add(currentRow);
                }
            }

            return rows;
        }
        finally
        {
            TryInvokeMethod(workbook, "Close", false);
            TryInvokeMethod(application, "Quit");

            ReleaseComObject(cellsCollection);
            ReleaseComObject(columnsCollection);
            ReleaseComObject(rowsCollection);
            ReleaseComObject(usedRange);
            ReleaseComObject(worksheet);
            ReleaseComObject(worksheets);
            ReleaseComObject(workbook);
            ReleaseComObject(workbooks);
            ReleaseComObject(application);
        }
    }

    private static object? GetFirstWorksheetWithData(object worksheets)
    {
        int count = Convert.ToInt32(GetProperty(worksheets, "Count"));

        for (int index = 1; index <= count; index++)
        {
            object? worksheet = null;
            object? usedRange = null;
            object? rowsCollection = null;
            object? columnsCollection = null;
            bool keepWorksheet = false;

            try
            {
                worksheet = GetIndexedProperty(worksheets, "Item", index);
                if (worksheet is null)
                    continue;

                usedRange = GetProperty(worksheet, "UsedRange");
                rowsCollection = GetProperty(usedRange, "Rows");
                columnsCollection = GetProperty(usedRange, "Columns");

                int rowCount = Convert.ToInt32(GetProperty(rowsCollection, "Count"));
                int columnCount = Convert.ToInt32(GetProperty(columnsCollection, "Count"));
                if (rowCount <= 0 || columnCount <= 0)
                    continue;

                object? value = GetProperty(usedRange, "Value2");
                if (value is null || !WorksheetHasData(value))
                    continue;

                keepWorksheet = true;
                return worksheet;
            }
            finally
            {
                ReleaseComObject(columnsCollection);
                ReleaseComObject(rowsCollection);
                ReleaseComObject(usedRange);

                if (!keepWorksheet)
                    ReleaseComObject(worksheet);
            }
        }

        return null;
    }

    private static bool WorksheetHasData(object value)
    {
        if (value is object[,] cells)
        {
            for (int row = cells.GetLowerBound(0); row <= cells.GetUpperBound(0); row++)
            {
                for (int column = cells.GetLowerBound(1); column <= cells.GetUpperBound(1); column++)
                {
                    if (!string.IsNullOrWhiteSpace(cells[row, column]?.ToString()))
                        return true;
                }
            }

            return false;
        }

        return !string.IsNullOrWhiteSpace(value.ToString());
    }

    private static string GetDisplayedCellText(object cellsCollection, int row, int column)
    {
        object? cell = null;

        try
        {
            cell = GetIndexedProperty(cellsCollection, "Item", row, column);
            return GetProperty(cell, "Text")?.ToString() ?? string.Empty;
        }
        finally
        {
            ReleaseComObject(cell);
        }
    }

    private static object? GetProperty(object? target, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            binder: null,
            target,
            args: null);
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        target.GetType().InvokeMember(
            propertyName,
            BindingFlags.SetProperty,
            binder: null,
            target,
            [value]);
    }

    private static object? GetIndexedProperty(object target, string propertyName, params object[] indexes)
    {
        return target.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            binder: null,
            target,
            indexes);
    }

    private static object? InvokeMethod(object? target, string methodName, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(target);

        return target.GetType().InvokeMember(
            methodName,
            BindingFlags.InvokeMethod,
            binder: null,
            target,
            args);
    }

    private static void TryInvokeMethod(object? target, string methodName, params object[] args)
    {
        if (target is null)
            return;

        try
        {
            InvokeMethod(target, methodName, args);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Excel cleanup '{methodName}' failed: {ex.Message}");
        }
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
            Marshal.ReleaseComObject(comObject);
    }

    private static Task<T> RunStaAsync<T>(Func<T> func)
    {
        TaskCompletionSource<T> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Thread thread = new(() =>
        {
            try
            {
                completionSource.SetResult(func());
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return completionSource.Task;
    }
}
