using Simple_QR_Code_Maker.Maui.ViewModels;

namespace Simple_QR_Code_Maker.Maui;

public partial class MainPage : ContentPage
{
    public MainPage(CreatorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
