using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private void FolderFilesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FolderFileItem item)
            ViewModel?.OpenFolderFileItemCommand.Execute(item);
    }
}
