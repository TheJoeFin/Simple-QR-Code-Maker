using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class HistoryPaneContentControl : UserControl
{
    public MainViewModel? ViewModel
    {
        get { return (MainViewModel?)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(HistoryPaneContentControl), new PropertyMetadata(null, OnViewModelChanged));

    public HistoryPaneContentControl()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((HistoryPaneContentControl)d).Bindings.Update();
    }
}
