using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Contracts.ViewModels;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class SpreadsheetImportViewModel : ObservableRecipient, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly List<List<string>> _allRows = [];
    private string[] _headers = [];
    private List<List<string>> _dataRows = [];
    private HistoryItem? _returnState;
    private int _importableValueCount;

    [ObservableProperty]
    private ObservableCollection<string> columnNames = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private int selectedColumnIndex = -1;

    [ObservableProperty]
    private bool firstRowIsHeader = true;

    [ObservableProperty]
    private string rowCountDescription = string.Empty;

    [ObservableProperty]
    private string importCountDescription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileDescription))]
    private string sourceFileName = string.Empty;

    [ObservableProperty]
    private string textBeforeImportedLine = string.Empty;

    [ObservableProperty]
    private string textAfterImportedLine = string.Empty;

    [ObservableProperty]
    private bool removeDuplicates = true;

    public SpreadsheetImportViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public bool CanImport => SelectedColumnIndex >= 0 && _importableValueCount > 0;

    public string FileDescription => string.IsNullOrWhiteSpace(SourceFileName)
        ? "Review the detected rows and choose a column to import."
        : $"Review \"{SourceFileName}\" and choose a column to import.";

    public IReadOnlyList<string> Headers => _headers;

    public IReadOnlyList<IReadOnlyList<string>> PreviewRows => _dataRows;

    partial void OnFirstRowIsHeaderChanged(bool value)
    {
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

    public void OnNavigatedTo(object parameter)
    {
        _returnState = null;
        _allRows.Clear();
        _headers = [];
        _dataRows = [];
        ColumnNames.Clear();
        SelectedColumnIndex = -1;
        SourceFileName = string.Empty;
        TextBeforeImportedLine = string.Empty;
        TextAfterImportedLine = string.Empty;
        RemoveDuplicates = true;
        RowCountDescription = string.Empty;
        ImportCountDescription = string.Empty;

        if (parameter is not SpreadsheetImportNavigationData data)
            return;

        _returnState = CloneHistoryItem(data.ReturnState);
        SourceFileName = data.SourceFileName;
        _allRows.AddRange(data.Rows.Select(row => row.ToList()));
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
    private void Import()
    {
        if (_returnState is null)
            return;

        List<string> selectedValues = GetSelectedValues();
        if (selectedValues.Count == 0)
            return;

        string imported = string.Join("\r", selectedValues);
        HistoryItem nextState = CloneHistoryItem(_returnState);
        nextState.CodesContent = string.IsNullOrWhiteSpace(nextState.CodesContent)
            ? imported
            : nextState.CodesContent + "\r" + imported;

        _navigationService.NavigateTo(typeof(MainViewModel).FullName!, nextState);
    }

    private void Refresh(bool firstRowIsHeader)
    {
        if (_allRows.Count == 0)
        {
            _headers = [];
            _dataRows = [];
            ColumnNames.Clear();
            RowCountDescription = "No data found";
            ImportCountDescription = string.Empty;
            _importableValueCount = 0;
            OnPropertyChanged(nameof(Headers));
            OnPropertyChanged(nameof(PreviewRows));
            OnPropertyChanged(nameof(CanImport));
            ImportCommand.NotifyCanExecuteChanged();
            return;
        }

        int columnCount = _allRows.Max(r => r.Count);

        if (firstRowIsHeader && _allRows.Count > 0)
        {
            List<string> headerRow = _allRows[0];
            _headers = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                _headers[i] = i < headerRow.Count && !string.IsNullOrWhiteSpace(headerRow[i])
                    ? headerRow[i]
                    : $"Column {i + 1}";
            }

            _dataRows = _allRows.Skip(1).ToList();
        }
        else
        {
            _headers = Enumerable.Range(1, columnCount).Select(i => $"Column {i}").ToArray();
            _dataRows = _allRows.ToList();
        }

        ColumnNames.Clear();
        foreach (string header in _headers)
            ColumnNames.Add(header);

        if (SelectedColumnIndex < 0 || SelectedColumnIndex >= ColumnNames.Count)
            SelectedColumnIndex = ColumnNames.Count > 0 ? 0 : -1;

        RowCountDescription = $"{_dataRows.Count} row{(_dataRows.Count == 1 ? "" : "s")} in file";

        OnPropertyChanged(nameof(Headers));
        OnPropertyChanged(nameof(PreviewRows));
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

        IEnumerable<string> values = _dataRows
            .Select(row => columnIndex < row.Count ? row[columnIndex] : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"{TextBeforeImportedLine}{value}{TextAfterImportedLine}");

        if (RemoveDuplicates)
            values = values.Distinct(StringComparer.Ordinal);

        return values.ToList();
    }

    private static HistoryItem CloneHistoryItem(HistoryItem source)
    {
        return new HistoryItem
        {
            CodesContent = source.CodesContent,
            Foreground = source.Foreground,
            Background = source.Background,
            ErrorCorrection = source.ErrorCorrection,
            LogoImagePath = source.LogoImagePath,
            LogoSizePercentage = source.LogoSizePercentage,
            LogoPaddingPixels = source.LogoPaddingPixels,
        };
    }
}
