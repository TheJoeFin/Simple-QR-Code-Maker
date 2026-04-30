using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
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
    }

    private void FileNameCellRoot_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement cellRoot)
            return;

        if (cellRoot.FindName("OpenFileLinkButton") is not HyperlinkButton openFileLinkButton)
            return;

        TableViewRow? row = FindAncestor<TableViewRow>(cellRoot);
        if (row is null)
            return;

        BindingOperations.SetBinding(
            openFileLinkButton,
            VisibilityProperty,
            new Binding
            {
                Source = row,
                Path = new PropertyPath("IsPointerOver"),
                Converter = Resources["BoolToVisibilityConverter"] as IValueConverter,
            });
    }

    private static T? FindAncestor<T>(DependencyObject? dependencyObject)
        where T : DependencyObject
    {
        DependencyObject? current = dependencyObject;
        while (current is not null)
        {
            if (current is T target)
                return target;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
