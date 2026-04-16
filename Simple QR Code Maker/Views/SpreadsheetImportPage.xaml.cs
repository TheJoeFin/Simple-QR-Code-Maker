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
        Unloaded += SpreadsheetImportPage_Unloaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshPreviewTableView();
    }

    private void SpreadsheetImportPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        Unloaded -= SpreadsheetImportPage_Unloaded;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SpreadsheetImportViewModel.Headers)
            or nameof(SpreadsheetImportViewModel.PreviewRows))
        {
            RefreshPreviewTableView();
        }
    }

    private void RefreshPreviewTableView()
    {
        PreviewTableView.ItemsSource = null;
        PreviewTableView.Columns.Clear();

        IReadOnlyList<string> headers = ViewModel.Headers;
        if (headers.Count == 0)
        {
            return;
        }

        for (int i = 0; i < headers.Count; i++)
        {
            PreviewTableView.Columns.Add(new TableViewTextColumn
            {
                Header = headers[i],
                IsReadOnly = true,
                CanFilter = false,
                CanSort = false,
                Binding = new Binding
                {
                    Mode = BindingMode.OneWay,
                    Path = new PropertyPath($"[{i}]"),
                },
            });
        }

        PreviewTableView.ItemsSource = ViewModel.PreviewRows;
    }
}
