using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Simple_QR_Code_Maker.ViewModels;
using WinUI.TableView;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class FolderSummaryPage : Page
{
    public FolderSummaryViewModel ViewModel { get; }

    public FolderSummaryPage()
    {
        ViewModel = App.GetService<FolderSummaryViewModel>();
        InitializeComponent();
        SetupTableColumns();
        Loaded += FolderSummaryPage_Loaded;
    }

    private void FolderSummaryPage_Loaded(object sender, RoutedEventArgs e)
    {
        SummaryTableView.ItemsSource = ViewModel.SummaryItems;
    }

    private void SetupTableColumns()
    {
        SummaryTableView.Columns.Add(new TableViewTextColumn
        {
            Header = "File Name",
            IsReadOnly = true,
            CanFilter = false,
            CanSort = true,
            MinWidth = 200,
            Binding = new Binding
            {
                Mode = BindingMode.OneWay,
                Path = new Microsoft.UI.Xaml.PropertyPath(nameof(Models.FolderSummaryItem.FileName)),
            },
        });

        SummaryTableView.Columns.Add(new TableViewTextColumn
        {
            Header = "QR Codes",
            IsReadOnly = true,
            CanFilter = false,
            CanSort = true,
            MinWidth = 80,
            Binding = new Binding
            {
                Mode = BindingMode.OneWay,
                Path = new Microsoft.UI.Xaml.PropertyPath(nameof(Models.FolderSummaryItem.QrCodeCount)),
            },
        });

        SummaryTableView.Columns.Add(new TableViewTextColumn
        {
            Header = "Contents",
            IsReadOnly = true,
            CanFilter = false,
            CanSort = false,
            MinWidth = 300,
            Binding = new Binding
            {
                Mode = BindingMode.OneWay,
                Path = new Microsoft.UI.Xaml.PropertyPath(nameof(Models.FolderSummaryItem.QrCodeContents)),
            },
        });
    }
}
