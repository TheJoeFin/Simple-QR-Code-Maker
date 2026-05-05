using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class FolderSummaryPage : Page
{
    public FolderSummaryViewModel ViewModel { get; }

    public FolderSummaryPage()
    {
        ViewModel = App.GetService<FolderSummaryViewModel>();
        InitializeComponent();
    }

    private void OpenFileLinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { DataContext: FolderSummaryItem item })
            ViewModel.OpenFileCommand.Execute(item);
    }

    private void FileNameCellRoot_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Grid { Children: { } children })
        {
            foreach (UIElement child in children)
            {
                if (child is HyperlinkButton hyperlinkButton)
                {
                    hyperlinkButton.Visibility = Visibility.Visible;
                    break;
                }
            }
        }
    }

    private void FileNameCellRoot_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Grid { Children: { } children })
        {
            foreach (UIElement child in children)
            {
                if (child is HyperlinkButton hyperlinkButton)
                {
                    hyperlinkButton.Visibility = Visibility.Collapsed;
                    break;
                }
            }
        }
    }
}
