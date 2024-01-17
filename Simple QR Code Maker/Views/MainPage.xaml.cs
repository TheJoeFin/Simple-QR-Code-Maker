using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
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

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UrlTextBox.Focus(FocusState.Programmatic);
    }

    private async void QrCodeImage_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is not Image image || image.Source is not WriteableBitmap bitmap)
            return;

        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        string? imageNameFileName = $"{ToolTipService.GetToolTip(image)}" ?? "QR_Code";
        // remove charcters that are not allowed in file names
        imageNameFileName = imageNameFileName.ReplaceReservedCharacters();
        imageNameFileName += ".png";
        StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);
        bool success = await bitmap.SavePngToStorageFile(file);

        if (!success)
            return;

        args.Data.SetStorageItems(new[] { file });
        args.Data.RequestedOperation = DataPackageOperation.Copy;
    }
}
