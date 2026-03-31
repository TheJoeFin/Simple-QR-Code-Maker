using Microsoft.UI.Text;
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
        RefreshPreviewGrid();
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
            RefreshPreviewGrid();
        }
    }

    private void RefreshPreviewGrid()
    {
        PreviewGrid.Children.Clear();
        PreviewGrid.ColumnDefinitions.Clear();
        PreviewGrid.RowDefinitions.Clear();

        IReadOnlyList<string> headers = ViewModel.Headers;
        IReadOnlyList<IReadOnlyList<string>> rows = ViewModel.PreviewRows;
        int columnCount = headers.Count;

        if (columnCount == 0)
        {
            PreviewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock emptyState = new()
            {
                Margin = new Thickness(12),
                Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush,
                Text = "No preview available.",
            };
            Grid.SetRow(emptyState, 0);
            PreviewGrid.Children.Add(emptyState);
            return;
        }

        for (int column = 0; column < columnCount; column++)
            PreviewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Brush? altBrush = Application.Current.Resources.TryGetValue(
            "SubtleFillColorSecondaryBrush", out object? brush) ? brush as Brush : null;

        PreviewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int column = 0; column < columnCount; column++)
        {
            TextBlock cell = new()
            {
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 5, 8, 5),
                Text = headers[column],
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, column);
            PreviewGrid.Children.Add(cell);
        }

        for (int row = 0; row < rows.Count; row++)
        {
            PreviewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            IReadOnlyList<string> fields = rows[row];

            if (altBrush is not null && row % 2 == 0)
            {
                Border rowBackground = new() { Background = altBrush };
                Grid.SetRow(rowBackground, row + 1);
                Grid.SetColumnSpan(rowBackground, columnCount);
                PreviewGrid.Children.Add(rowBackground);
            }

            for (int column = 0; column < columnCount; column++)
            {
                TextBlock cell = new()
                {
                    Padding = new Thickness(8, 3, 8, 3),
                    Text = column < fields.Count ? fields[column] : string.Empty,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Grid.SetRow(cell, row + 1);
                Grid.SetColumn(cell, column);
                PreviewGrid.Children.Add(cell);
            }
        }
    }
}
