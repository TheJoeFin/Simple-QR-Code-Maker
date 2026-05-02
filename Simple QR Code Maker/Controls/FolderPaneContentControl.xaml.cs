using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class FolderPaneContentControl : UserControl
{
    public DecodingViewModel? ViewModel
    {
        get { return (DecodingViewModel?)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(DecodingViewModel),
            typeof(FolderPaneContentControl),
            new PropertyMetadata(null, OnViewModelChanged));

    public FolderPaneContentControl()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FolderPaneContentControl)d;
        control.Bindings.Update();

        if (e.NewValue is DecodingViewModel vm)
            control.FolderFilesListView.DataContext = vm;
    }

    private void FolderFileItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: FolderFileItem folderItem })
            WeakReferenceMessenger.Default.Send(new OpenFolderFileItemMessage(folderItem));
    }

    private void CutOutItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DecodingImageItem cutOut })
            WeakReferenceMessenger.Default.Send(new SelectDecodingImageItemMessage(cutOut));
    }

    private void CutOutItem_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            SetSaveButtonOpacity(grid, 1);
    }

    private void CutOutItem_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            SetSaveButtonOpacity(grid, 0);
    }

    private static void SetSaveButtonOpacity(Grid grid, double opacity)
    {
        foreach (UIElement child in grid.Children)
            if (child is Grid innerGrid)
                foreach (UIElement innerChild in innerGrid.Children)
                    if (innerChild is Button btn)
                    {
                        btn.Opacity = opacity;
                        return;
                    }
    }

    private async void SaveCutOutButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DecodingImageItem item && ViewModel is not null)
            await ViewModel.SaveCutOutCommand.ExecuteAsync(item);
    }

    private void SaveCutOutButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }
}
