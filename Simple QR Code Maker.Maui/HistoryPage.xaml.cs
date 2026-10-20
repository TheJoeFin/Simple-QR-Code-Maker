using Simple_QR_Code_Maker.Maui.ViewModels;

namespace Simple_QR_Code_Maker.Maui;

public partial class HistoryPage : ContentPage
{
    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        Loaded += async (_, _) => await viewModel.RefreshCommand.ExecuteAsync(null);
    }
}
