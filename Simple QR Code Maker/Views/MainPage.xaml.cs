using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }

    private bool _didSetCaretToEnd = false;

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UrlTextBox.Focus(FocusState.Programmatic);
        // set the caret to the end of the text when navigating back to the page
        UrlTextBox.Select(UrlTextBox.Text.Length, 0);
    }

    private async void QrCodeImage_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is not Image image || image.Source is not WriteableBitmap bitmap)
            return;

        DragOperationDeferral deferral = args.GetDeferral();
        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        string? imageNameFileName = $"{ToolTipService.GetToolTip(image)}" ?? "QR_Code";
        // remove characters that are not allowed in file names
        imageNameFileName = imageNameFileName.ToSafeFileName();
        imageNameFileName += ".png";
        StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);
        bool success = await bitmap.SavePngToStorageFile(file);

        if (!success)
        {
            deferral.Complete();
            return;
        }

        ViewModel.SaveCurrentStateToHistory();
        args.Data.SetStorageItems(new[] { file });
        args.Data.RequestedOperation = DataPackageOperation.Copy;
        deferral.Complete();
    }

    private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // set the caret to the end of the text when loading for the first time
        if (!_didSetCaretToEnd)
        {
            _didSetCaretToEnd = true;
            UrlTextBox.Select(UrlTextBox.Text.Length, 0);
        }
    }
}
