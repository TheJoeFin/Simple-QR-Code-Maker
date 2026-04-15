using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Simple_QR_Code_Maker.ViewModels;
using System.ComponentModel;

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
        RefreshPreviewListView();
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
            RefreshPreviewListView();
        }
    }

    private void RefreshPreviewListView()
    {
        PreviewListView.ItemsSource = null;

        IReadOnlyList<string> headers = ViewModel.Headers;
        int columnCount = headers.Count;

        if (columnCount == 0)
        {
            PreviewListView.Header = null;
            return;
        }

        DataTable dataTable = new() { ColumnSpacing = 0 };
        foreach (string header in headers)
        {
            dataTable.Children.Add(new DataColumn
            {
                Content = header,
                DesiredWidth = new GridLength(1, GridUnitType.Star),
                CanResize = true,
            });
        }

        Application.Current.Resources.TryGetValue("SubtleFillColorSecondaryBrush", out object? brush);
        PreviewListView.Header = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            Background = brush as Brush,
            Child = dataTable,
        };

        PreviewListView.ItemsSource = ViewModel.PreviewRows;
    }

    private void PreviewListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        args.Handled = true;

        if (args.Item is not IReadOnlyList<string> fields)
            return;

        int columnCount = ViewModel.Headers.Count;
        DataRow row = new();

        for (int i = 0; i < columnCount; i++)
        {
            row.Children.Add(new TextBlock
            {
                Text = i < fields.Count ? fields[i] : string.Empty,
                Padding = new Thickness(8, 3, 8, 3),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        args.ItemContainer.Content = row;
    }
}
