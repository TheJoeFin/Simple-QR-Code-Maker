using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace Simple_QR_Code_Maker.Views;
public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog()
    {
        InitializeComponent();

        VersionNumber.Text = GetAppDescription();
    }

    private static string GetAppDescription()
    {
        Package package = Package.Current;
        PackageId packageId = package.Id;
        PackageVersion version = packageId.Version;

        return $"{package.DisplayName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    private async void ReviewBTN_Click(object sender, RoutedEventArgs e)
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9NCH56G3RQFC"));
    }
}
