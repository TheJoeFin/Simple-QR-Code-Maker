using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class DecodingHistoryPaneContentControl : UserControl
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
            typeof(DecodingHistoryPaneContentControl),
            new PropertyMetadata(null, OnViewModelChanged));

    public DecodingHistoryPaneContentControl()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DecodingHistoryPaneContentControl)d;
        control.Bindings.Update();

        if (e.NewValue is DecodingViewModel vm)
            control.HistoryListView.DataContext = vm;
    }

    private async void HistoryListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DecodingHistoryItem item && ViewModel is not null)
            await ViewModel.OpenDecodingHistoryItemCommand.ExecuteAsync(item);
    }

    private void RemoveDecodingHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        // The flyout item's DataContext is inherited from the Grid it was opened on.
        // ElementName bindings don't resolve inside a flyout's popup, so the removal is
        // performed here in code-behind instead of via a Command binding in XAML.
        if (sender is FrameworkElement { DataContext: DecodingHistoryItem item } && ViewModel is not null)
            ViewModel.RemoveDecodingHistoryItemCommand.Execute(item);
    }
}
