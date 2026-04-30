using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class CutOutImagesPaneContentControl : UserControl
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
            typeof(CutOutImagesPaneContentControl),
            new PropertyMetadata(null, OnViewModelChanged));

    public CutOutImagesPaneContentControl()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (CutOutImagesPaneContentControl)d;
        control.Bindings.Update();

        if (e.NewValue is DecodingViewModel vm)
            control.CutOutImagesListView.DataContext = vm;
    }

    private void CutOutImagesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DecodingImageItem item)
            ViewModel?.SelectDecodingImageItem(item);
    }
}
