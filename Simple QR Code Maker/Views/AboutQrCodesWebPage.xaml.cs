using Microsoft.UI.Xaml.Controls;

using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Views;

// To learn more about WebView2, see https://docs.microsoft.com/microsoft-edge/webview2/.
public sealed partial class AboutQrCodesWebPage : Page
{
    public AboutQrCodesWebViewModel ViewModel
    {
        get;
    }

    public AboutQrCodesWebPage()
    {
        ViewModel = App.GetService<AboutQrCodesWebViewModel>();
        InitializeComponent();

        ViewModel.WebViewService.Initialize(WebView);
    }
}
