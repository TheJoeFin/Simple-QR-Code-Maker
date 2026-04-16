using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Controls;
using Simple_QR_Code_Maker.ViewModels;
using Windows.System;

namespace Simple_QR_Code_Maker.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    private bool _processingRating;

    private async void AppRatingControl_ValueChanged(RatingControl sender, object args)
    {
        if (_processingRating) return;
        double rating = sender.Value;
        if (rating < 1) return;

        _processingRating = true;
        sender.Value = -1;
        _processingRating = false;

        if (rating >= 4)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9NCH56G3RQFC"));
        }
        else
        {
            FeedbackDialog dialog = new()
            {
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private void SettingsExpander_Expanded(object sender, EventArgs e)
    {

    }

    private void ImportExportInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.ImportExportStatusMessage = string.Empty;
    }
}
