using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
    }

    private async void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        AboutDialog aboutWindow = new()
        {
            XamlRoot = Content.XamlRoot
        };

        _ = await aboutWindow.ShowAsync();
    }
}
