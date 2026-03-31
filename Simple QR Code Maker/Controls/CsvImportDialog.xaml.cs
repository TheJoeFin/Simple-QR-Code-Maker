using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Simple_QR_Code_Maker.Helpers;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.Controls;

[ObservableObject]
public sealed partial class CsvImportDialog : ContentDialog
{
    private readonly List<List<string>> _allRows;
    private string[] _headers = [];
    private List<List<string>> _dataRows = [];

    [ObservableProperty]
    private ObservableCollection<string> columnNames = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImportEnabled))]
    private int selectedColumnIndex = -1;

    [ObservableProperty]
    private string rowCountDescription = string.Empty;

    [ObservableProperty]
    private string importCountDescription = string.Empty;

    public bool IsImportEnabled => SelectedColumnIndex >= 0;

    /// <summary>
    /// The values extracted from the selected column, respecting the header toggle.
    /// Populated when the primary button is clicked.
    /// </summary>
    public IList<string> SelectedValues { get; private set; } = [];

    public CsvImportDialog(string csvContent)
    {
        _allRows = CsvParser.Parse(csvContent);
        InitializeComponent();
        HeaderToggle.Toggled += HeaderToggle_Toggled;
        ColumnComboBox.SelectionChanged += ColumnComboBox_SelectionChanged;
        PrimaryButtonClick += OnPrimaryButtonClick;
        Refresh(firstRowIsHeader: true);
    }

    private void Refresh(bool firstRowIsHeader)
    {
        PreviewGrid.Children.Clear();
        PreviewGrid.ColumnDefinitions.Clear();
        PreviewGrid.RowDefinitions.Clear();

        if (_allRows.Count == 0)
        {
            ColumnNames.Clear();
            RowCountDescription = "No data found";
            ImportCountDescription = string.Empty;
            return;
        }

        int columnCount = _allRows.Max(r => r.Count);

        if (firstRowIsHeader && _allRows.Count > 0)
        {
            List<string> headerRow = _allRows[0];
            _headers = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
                _headers[i] = i < headerRow.Count && !string.IsNullOrWhiteSpace(headerRow[i])
                    ? headerRow[i]
                    : $"Column {i + 1}";
            _dataRows = _allRows.Skip(1).ToList();
        }
        else
        {
            _headers = Enumerable.Range(1, columnCount).Select(i => $"Column {i}").ToArray();
            _dataRows = _allRows;
        }

        // Update column ComboBox.
        ColumnNames.Clear();
        foreach (string h in _headers)
            ColumnNames.Add(h);

        if (SelectedColumnIndex < 0 || SelectedColumnIndex >= ColumnNames.Count)
            SelectedColumnIndex = ColumnNames.Count > 0 ? 0 : -1;

        // Build column definitions.
        for (int c = 0; c < columnCount; c++)
            PreviewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Resolve alternating row brush once.
        Brush? altBrush = Application.Current.Resources.TryGetValue(
            "SubtleFillColorSecondaryBrush", out object? b) ? b as Brush : null;

        // Row 0: header.
        PreviewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < columnCount; c++)
        {
            TextBlock cell = new()
            {
                Text = _headers[c],
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 5, 8, 5),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, c);
            PreviewGrid.Children.Add(cell);
        }

        // Rows 1+: data (all rows).
        for (int r = 0; r < _dataRows.Count; r++)
        {
            PreviewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            List<string> fields = _dataRows[r];

            // Alternating background via a border spanning all columns.
            if (altBrush is not null && r % 2 == 0)
            {
                Border rowBg = new() { Background = altBrush };
                Grid.SetRow(rowBg, r + 1);
                Grid.SetColumnSpan(rowBg, columnCount);
                PreviewGrid.Children.Add(rowBg);
            }

            for (int c = 0; c < columnCount; c++)
            {
                TextBlock cell = new()
                {
                    Text = c < fields.Count ? fields[c] : string.Empty,
                    Padding = new Thickness(8, 3, 8, 3),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                PreviewGrid.Children.Add(cell);
            }
        }

        int totalDataRows = _dataRows.Count;
        RowCountDescription = $"{totalDataRows} row{(totalDataRows == 1 ? "" : "s")} in file";
        UpdateImportCount();
    }

    private void UpdateImportCount()
    {
        int colIdx = SelectedColumnIndex;
        if (colIdx < 0 || _dataRows.Count == 0)
        {
            ImportCountDescription = string.Empty;
            return;
        }

        int count = _dataRows.Count(row => colIdx < row.Count && !string.IsNullOrWhiteSpace(row[colIdx]));
        string colName = colIdx < _headers.Length ? _headers[colIdx] : $"Column {colIdx + 1}";
        ImportCountDescription = $"{count} value{(count == 1 ? "" : "s")} will be imported from \"{colName}\"";
    }

    private void HeaderToggle_Toggled(object sender, RoutedEventArgs e)
    {
        Refresh(HeaderToggle.IsOn);
    }

    private void ColumnComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateImportCount();
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        int colIdx = SelectedColumnIndex;
        if (colIdx < 0)
            return;

        SelectedValues = _dataRows
            .Select(row => colIdx < row.Count ? row[colIdx] : string.Empty)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }
}
