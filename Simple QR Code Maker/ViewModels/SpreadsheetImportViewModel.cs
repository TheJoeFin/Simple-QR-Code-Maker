using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class SpreadsheetImportViewModel : ObservableRecipient, INavigationAware
{
    private const int PreviewRowLimit = 200;
    private const string IdColumnName = "id";

    private readonly INavigationService _navigationService;
    private readonly Dictionary<int, string> _generatedIds = [];
    private readonly List<SpreadsheetSourceRow> _allRows = [];
    private string[] _headers = [];
    private List<SpreadsheetSourceRow> _dataRows = [];
    private List<IReadOnlyList<string>> _displayRows = [];
    private HistoryItem? _returnState;
    private string _sourceFilePath = string.Empty;
    private int _importableValueCount;
    private int _idColumnIndex = -1;
    private bool _willCreateIdColumn;

    [ObservableProperty]
    public partial ObservableCollection<string> ColumnNames { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    public partial int SelectedColumnIndex { get; set; } = -1;

    [ObservableProperty]
    public partial bool FirstRowIsHeader { get; set; } = true;

    [ObservableProperty]
    public partial string RowCountDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ImportCountDescription { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileDescription))]
    public partial string SourceFileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TextBeforeImportedLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TextAfterImportedLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RemoveDuplicates { get; set; } = true;

    [ObservableProperty]
    public partial bool GenerateIdsToSpreadsheet { get; set; } = false;

    [ObservableProperty]
    public partial SpreadsheetGeneratedIdFormatOption? SelectedGeneratedIdFormatOption { get; set; } = SpreadsheetGeneratedIdFormatOption.All[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    public partial bool IsImportInProgress { get; set; } = false;

    [ObservableProperty]
    public partial string IdGenerationDescription { get; set; } = "Generated IDs will not be written back to the spreadsheet.";

    [ObservableProperty]
    public partial bool IsStatusVisible { get; set; } = false;

    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial string StatusTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public SpreadsheetImportViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanImport => !IsImportInProgress && SelectedColumnIndex >= 0 && _importableValueCount > 0;

    public string FileDescription => string.IsNullOrWhiteSpace(SourceFileName)
        ? "Review the detected rows and choose a column to import."
        : $"Review \"{SourceFileName}\" and choose a column to import.";

    public string ImportButtonText => GenerateIdsToSpreadsheet
        ? "Save IDs and import selected column"
        : "Import selected column";

    public bool CanConfigureGeneratedIdFormat => GenerateIdsToSpreadsheet && _dataRows.Count > 0;

    public IReadOnlyList<SpreadsheetGeneratedIdFormatOption> GeneratedIdFormatOptions => SpreadsheetGeneratedIdFormatOption.All;

    public IReadOnlyList<string> Headers => _headers;

    public IReadOnlyList<IReadOnlyList<string>> PreviewRows =>
        _displayRows.Count > PreviewRowLimit
            ? [.. _displayRows.Take(PreviewRowLimit)]
            : _displayRows;

    partial void OnFirstRowIsHeaderChanged(bool value)
    {
        ClearStatus();
        Refresh(value);
    }

    partial void OnSelectedColumnIndexChanged(int value)
    {
        UpdateImportCount();
        ImportCommand.NotifyCanExecuteChanged();
    }

    partial void OnTextBeforeImportedLineChanged(string value)
    {
        UpdateImportCount();
    }

    partial void OnTextAfterImportedLineChanged(string value)
    {
        UpdateImportCount();
    }

    partial void OnRemoveDuplicatesChanged(bool value)
    {
        UpdateImportCount();
    }

    partial void OnGenerateIdsToSpreadsheetChanged(bool value)
    {
        ClearStatus();
        Refresh(FirstRowIsHeader);

        if (value && _idColumnIndex >= 0)
            SelectedColumnIndex = _idColumnIndex;

        OnPropertyChanged(nameof(ImportButtonText));
        OnPropertyChanged(nameof(CanConfigureGeneratedIdFormat));
    }

    partial void OnSelectedGeneratedIdFormatOptionChanged(SpreadsheetGeneratedIdFormatOption? value)
    {
        _generatedIds.Clear();
        ClearStatus();
        Refresh(FirstRowIsHeader);
    }

    partial void OnIsImportInProgressChanged(bool value)
    {
        OnPropertyChanged(nameof(CanImport));
        ImportCommand.NotifyCanExecuteChanged();
    }

    public void OnNavigatedTo(object parameter)
    {
        _returnState = null;
        _allRows.Clear();
        _headers = [];
        _dataRows = [];
        _displayRows = [];
        _generatedIds.Clear();
        ColumnNames.Clear();
        SelectedColumnIndex = -1;
        SourceFileName = string.Empty;
        _sourceFilePath = string.Empty;
        TextBeforeImportedLine = string.Empty;
        TextAfterImportedLine = string.Empty;
        RemoveDuplicates = true;
        GenerateIdsToSpreadsheet = false;
        SelectedGeneratedIdFormatOption = SpreadsheetGeneratedIdFormatOption.All[0];
        IsImportInProgress = false;
        RowCountDescription = string.Empty;
        ImportCountDescription = string.Empty;
        IdGenerationDescription = "Generated IDs will not be written back to the spreadsheet.";
        ClearStatus();

        if (parameter is not SpreadsheetImportNavigationData data)
            return;

        _returnState = CloneHistoryItem(data.ReturnState);
        SourceFileName = data.SourceFileName;
        _sourceFilePath = data.SourceFilePath;
        _allRows.AddRange(data.Rows);
        FirstRowIsHeader = true;
        Refresh(FirstRowIsHeader);
    }

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateTo(typeof(MainViewModel).FullName!, _returnState);
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task Import()
    {
        if (_returnState is null)
            return;

        try
        {
            IsImportInProgress = true;
            ClearStatus();

            List<string> selectedValues = GetSelectedValues();
            if (selectedValues.Count == 0)
                return;

            if (GenerateIdsToSpreadsheet)
            {
                SpreadsheetIdWriteRequest request = BuildIdWriteRequest();
                await ExcelSpreadsheetHelper.WriteGeneratedIdsAsync(_sourceFilePath, request);
                WeakReferenceMessenger.Default.Send(new RequestShowMessage(
                    "IDs saved to spreadsheet",
                    $"Generated IDs were saved back to \"{SourceFileName}\".",
                    InfoBarSeverity.Success));
            }

            string imported = string.Join("\r", selectedValues);
            HistoryItem nextState = CloneHistoryItem(_returnState);
            nextState.CodesContent = string.IsNullOrWhiteSpace(nextState.CodesContent)
                ? imported
                : nextState.CodesContent + "\r" + imported;

            _navigationService.NavigateTo(typeof(MainViewModel).FullName!, nextState);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save generated spreadsheet IDs: {ex.Message}");
            ShowStatus(
                "Could not save the generated IDs back to the spreadsheet. Close the file if it is open elsewhere and try again.",
                "Could not save spreadsheet",
                InfoBarSeverity.Error);
        }
        finally
        {
            IsImportInProgress = false;
        }
    }

    private void Refresh(bool firstRowIsHeader)
    {
        int previousSelectedColumnIndex = SelectedColumnIndex;
        bool preferGeneratedColumn = GenerateIdsToSpreadsheet && previousSelectedColumnIndex == _idColumnIndex;

        if (_allRows.Count == 0)
        {
            _headers = [];
            _dataRows = [];
            _displayRows = [];
            ColumnNames.Clear();
            RowCountDescription = "No data found";
            ImportCountDescription = string.Empty;
            IdGenerationDescription = "Generated IDs will not be written back because no data was found.";
            _importableValueCount = 0;
            OnPropertyChanged(nameof(Headers));
            OnPropertyChanged(nameof(PreviewRows));
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(CanConfigureGeneratedIdFormat));
            ImportCommand.NotifyCanExecuteChanged();
            return;
        }

        int columnCount = _allRows.Max(row => row.Cells.Count);
        _dataRows = firstRowIsHeader ? [.. _allRows.Skip(1)] : [.. _allRows];

        List<string> headers = CreateBaseHeaders(columnCount, firstRowIsHeader);
        int existingIdColumnIndex = firstRowIsHeader ? FindIdColumnIndex(headers) : -1;
        _idColumnIndex = -1;
        _willCreateIdColumn = false;

        if (GenerateIdsToSpreadsheet && _dataRows.Count > 0)
        {
            if (existingIdColumnIndex >= 0)
            {
                _idColumnIndex = existingIdColumnIndex;
            }
            else
            {
                _idColumnIndex = headers.Count;
                _willCreateIdColumn = true;
                headers.Add(firstRowIsHeader ? IdColumnName : $"Column {headers.Count + 1}");
            }
        }

        _headers = [.. headers];

        ColumnNames.Clear();
        foreach (string header in _headers)
            ColumnNames.Add(header);

        BuildDisplayRows();

        if (GenerateIdsToSpreadsheet && _idColumnIndex >= 0 && (preferGeneratedColumn || previousSelectedColumnIndex < 0 || previousSelectedColumnIndex >= ColumnNames.Count))
            SelectedColumnIndex = GenerateIdsToSpreadsheet && _idColumnIndex >= 0
                ? _idColumnIndex
                : ColumnNames.Count > 0 ? 0 : -1;
        else if (SelectedColumnIndex < 0 || SelectedColumnIndex >= ColumnNames.Count)
            SelectedColumnIndex = ColumnNames.Count > 0 ? 0 : -1;

        RowCountDescription = _dataRows.Count > PreviewRowLimit
            ? $"{_dataRows.Count} rows in file (preview shows first {PreviewRowLimit})"
            : $"{_dataRows.Count} row{(_dataRows.Count == 1 ? "" : "s")} in file";

        OnPropertyChanged(nameof(Headers));
        OnPropertyChanged(nameof(PreviewRows));
        OnPropertyChanged(nameof(CanConfigureGeneratedIdFormat));
        OnPropertyChanged(nameof(ImportButtonText));
        UpdateImportCount();
    }

    private void UpdateImportCount()
    {
        int columnIndex = SelectedColumnIndex;
        if (columnIndex < 0 || _dataRows.Count == 0)
        {
            _importableValueCount = 0;
            ImportCountDescription = string.Empty;
            OnPropertyChanged(nameof(CanImport));
            ImportCommand.NotifyCanExecuteChanged();
            return;
        }

        List<string> selectedValues = GetSelectedValues();
        _importableValueCount = selectedValues.Count;
        string columnName = columnIndex < _headers.Length ? _headers[columnIndex] : $"Column {columnIndex + 1}";
        ImportCountDescription = $"{_importableValueCount} value{(_importableValueCount == 1 ? "" : "s")} will be imported from \"{columnName}\"";
        OnPropertyChanged(nameof(CanImport));
        ImportCommand.NotifyCanExecuteChanged();
    }

    private List<string> GetSelectedValues()
    {
        int columnIndex = SelectedColumnIndex;
        if (columnIndex < 0)
            return [];

        IEnumerable<string> values = _displayRows
            .Select(row => columnIndex < row.Count ? row[columnIndex] : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"{TextBeforeImportedLine}{value}{TextAfterImportedLine}");

        if (RemoveDuplicates)
            values = values.Distinct(StringComparer.Ordinal);

        return [.. values];
    }

    private List<string> CreateBaseHeaders(int columnCount, bool firstRowIsHeader)
    {
        if (firstRowIsHeader && _allRows.Count > 0)
        {
            SpreadsheetSourceRow headerRow = _allRows[0];
            List<string> headers = new(columnCount);
            for (int index = 0; index < columnCount; index++)
            {
                headers.Add(index < headerRow.Cells.Count && !string.IsNullOrWhiteSpace(headerRow.Cells[index])
                    ? headerRow.Cells[index]
                    : $"Column {index + 1}");
            }

            return headers;
        }

        return [.. Enumerable.Range(1, columnCount).Select(index => $"Column {index}")];
    }

    private void BuildDisplayRows()
    {
        List<IReadOnlyList<string>> displayRows = new(_dataRows.Count);
        int missingIdCount = 0;

        foreach (SpreadsheetSourceRow row in _dataRows)
        {
            List<string> currentRow = CreateNormalizedRow(row.Cells, _headers.Length);
            if (GenerateIdsToSpreadsheet && _idColumnIndex >= 0)
            {
                string existingId = GetCellValue(row.Cells, _idColumnIndex);
                if (string.IsNullOrWhiteSpace(existingId) && ShouldGenerateIdForRow(row))
                {
                    missingIdCount++;
                    currentRow[_idColumnIndex] = GetOrCreateGeneratedId(row.SourceRowIndex);
                }
                else
                {
                    currentRow[_idColumnIndex] = existingId;
                }
            }

            displayRows.Add(currentRow);
        }

        _displayRows = displayRows;
        IdGenerationDescription = BuildIdGenerationDescription(missingIdCount);
    }

    private static List<string> CreateNormalizedRow(IReadOnlyList<string> values, int columnCount)
    {
        List<string> row = new(columnCount);
        for (int index = 0; index < columnCount; index++)
            row.Add(index < values.Count ? values[index] : string.Empty);

        return row;
    }

    private string BuildIdGenerationDescription(int missingIdCount)
    {
        if (!GenerateIdsToSpreadsheet)
            return "Generated IDs will not be written back to the spreadsheet.";

        if (_dataRows.Count == 0)
            return "Generated IDs cannot be written because there are no data rows.";

        string formatName = SelectedGeneratedIdFormatOption?.DisplayName ?? "GUID";
        if (!FirstRowIsHeader)
            return $"{missingIdCount} row{(missingIdCount == 1 ? "" : "s")} will receive {formatName} values in a new last column because header row is off.";

        if (_willCreateIdColumn)
            return $"{missingIdCount} row{(missingIdCount == 1 ? "" : "s")} will receive {formatName} values in a new \"{IdColumnName}\" column. Existing cells in other columns will not change.";

        if (missingIdCount == 0)
            return $"The existing \"{_headers[_idColumnIndex]}\" column already has values in every row. Existing IDs will be preserved.";

        return $"{missingIdCount} blank cell{(missingIdCount == 1 ? "" : "s")} in \"{_headers[_idColumnIndex]}\" will be filled using {formatName}. Existing IDs will be preserved.";
    }

    private SpreadsheetIdWriteRequest BuildIdWriteRequest()
    {
        if (string.IsNullOrWhiteSpace(_sourceFilePath))
            throw new InvalidOperationException("A spreadsheet file path is required to save generated IDs.");
        if (_idColumnIndex < 0)
            throw new InvalidOperationException("An ID column must be available before generated IDs can be saved.");

        List<SpreadsheetIdWriteRow> rows = [];
        foreach (SpreadsheetSourceRow row in _dataRows)
        {
            if (!string.IsNullOrWhiteSpace(GetCellValue(row.Cells, _idColumnIndex)) || !ShouldGenerateIdForRow(row))
                continue;

            rows.Add(new SpreadsheetIdWriteRow
            {
                SourceRowIndex = row.SourceRowIndex,
                Value = GetOrCreateGeneratedId(row.SourceRowIndex),
            });
        }

        return new SpreadsheetIdWriteRequest
        {
            FirstRowIsHeader = FirstRowIsHeader,
            IdColumnIndex = _idColumnIndex,
            CreateIdColumn = _willCreateIdColumn,
            Rows = rows,
        };
    }

    private string GetOrCreateGeneratedId(int sourceRowIndex)
    {
        if (_generatedIds.TryGetValue(sourceRowIndex, out string? existingValue))
            return existingValue;

        SpreadsheetGeneratedIdFormat format = SelectedGeneratedIdFormatOption?.Format ?? SpreadsheetGeneratedIdFormat.Guid;
        string value = SpreadsheetIdGenerator.Create(format);
        _generatedIds[sourceRowIndex] = value;
        return value;
    }

    private bool ShouldGenerateIdForRow(SpreadsheetSourceRow row)
    {
        for (int index = 0; index < row.Cells.Count; index++)
        {
            if (index == _idColumnIndex)
                continue;
            if (!string.IsNullOrWhiteSpace(row.Cells[index]))
                return true;
        }

        return false;
    }

    private static string GetCellValue(IReadOnlyList<string> values, int index)
    {
        return index >= 0 && index < values.Count
            ? values[index]
            : string.Empty;
    }

    private static int FindIdColumnIndex(IReadOnlyList<string> headers)
    {
        for (int index = 0; index < headers.Count; index++)
        {
            if (string.Equals(headers[index].Trim(), IdColumnName, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private void ShowStatus(string message, string title, InfoBarSeverity severity)
    {
        StatusSeverity = severity;
        StatusTitle = title;
        StatusMessage = message;
        IsStatusVisible = true;
    }

    private void ClearStatus()
    {
        IsStatusVisible = false;
        StatusTitle = string.Empty;
        StatusMessage = string.Empty;
    }

    private static HistoryItem CloneHistoryItem(HistoryItem source)
    {
        return new HistoryItem
        {
            CodesContent = source.CodesContent,
            ContentKind = source.ContentKind,
            MultiLineCodeModeOverride = source.MultiLineCodeModeOverride,
            Foreground = source.Foreground,
            Background = source.Background,
            ErrorCorrection = source.ErrorCorrection,
            LogoImagePath = source.LogoImagePath,
            LogoEmoji = source.LogoEmoji,
            LogoEmojiStyle = source.LogoEmojiStyle,
            LogoSizePercentage = source.LogoSizePercentage,
            LogoPaddingPixels = source.LogoPaddingPixels,
        };
    }
}
