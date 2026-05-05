using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Simple_QR_Code_Maker.ViewModels;
using System.ComponentModel;
using WinUI.TableView;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class SpreadsheetImportPage : Page
{
    public SpreadsheetImportViewModel ViewModel { get; }

    public SpreadsheetImportPage()
    {
        ViewModel = App.GetService<SpreadsheetImportViewModel>();
        InitializeComponent();
        Loaded += SpreadsheetImportPage_Loaded;
        Unloaded += SpreadsheetImportPage_Unloaded;
    }

    private void SpreadsheetImportPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        RebuildPreviewColumns();
    }

    private void SpreadsheetImportPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpreadsheetImportViewModel.Headers))
            RebuildPreviewColumns();
    }

    private void RebuildPreviewColumns()
    {
        PreviewTableView.Columns.Clear();

        Style? cellTextStyle = Resources["SpreadsheetPreviewCellTextStyle"] as Style;

        for (int columnIndex = 0; columnIndex < ViewModel.Headers.Count; columnIndex++)
        {
            Binding binding = new()
            {
                Mode = BindingMode.OneWay,
                Path = new PropertyPath($"Cells[{columnIndex}]"),
                TargetNullValue = string.Empty,
                FallbackValue = string.Empty,
            };

            TableViewTextColumn column = new()
            {
                Header = ViewModel.Headers[columnIndex],
                Binding = binding,
                ClipboardContentBinding = binding,
                MinWidth = 140,
                CanFilter = false,
                CanSort = false,
                IsReadOnly = true,
            };

            if (cellTextStyle is not null)
                column.ElementStyle = cellTextStyle;

            PreviewTableView.Columns.Add(column);
        }
    }
}
