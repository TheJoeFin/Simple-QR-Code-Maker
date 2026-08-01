using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;
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

    private void RemoveHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        // The flyout item's DataContext is inherited from the HistoryRowItem it was opened on.
        // ElementName bindings don't resolve inside a flyout's popup, so the removal is
        // performed here in code-behind instead of via a Command binding in XAML.
        if (sender is FrameworkElement { DataContext: HistoryItem item } && ViewModel is not null)
            ViewModel.RemoveHistoryItemCommand.Execute(item);
    }
}
