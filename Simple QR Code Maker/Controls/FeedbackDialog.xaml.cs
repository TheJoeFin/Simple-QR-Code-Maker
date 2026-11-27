using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class FeedbackDialog : ContentDialog
{
    private const string Email = "joe@joefinapps.com";

    public FeedbackDialog()
    {
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        try
        {
            string subject = Uri.EscapeDataString("Feedback_EmailSubject".GetLocalized());
            string body = Uri.EscapeDataString(FeedbackTextBox.Text);
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri($"mailto:{Email}?subject={subject}&body={body}"));
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void CopyEmailButton_Click(object sender, RoutedEventArgs e)
    {
        DataPackage dataPackage = new();
        dataPackage.SetText(Email);
        Clipboard.SetContent(dataPackage);

        if (sender is Button btn)
        {
            btn.Content = "Feedback_CopyDone".GetLocalized();
            await Task.Delay(2000);
            btn.Content = "Feedback_Copy".GetLocalized();
        }
    }
}
