using Microsoft.UI.Xaml.Controls;

using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class DecodingPage : Page
{
    public DecodingViewModel ViewModel
    {
        get;
    }

    public DecodingPage()
    {
        ViewModel = App.GetService<DecodingViewModel>();
        InitializeComponent();
    }
}
