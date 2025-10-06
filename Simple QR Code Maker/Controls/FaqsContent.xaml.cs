using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;
using Windows.System;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class FaqsContent : UserControl
{
    public FaqsContentViewModel ViewModel { get; } = new FaqsContentViewModel();

    public FaqsContent()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    private async void IconAndTextButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = await Launcher.LaunchUriAsync(new Uri("mailto:joe@joefinapps.com"));
    }
}
