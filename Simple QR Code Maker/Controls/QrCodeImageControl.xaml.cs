using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Simple_QR_Code_Maker.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Simple_QR_Code_Maker.Extensions;


namespace Simple_QR_Code_Maker.Controls;

public sealed partial class QrCodeImageControl : UserControl
{
    public BarcodeImageItem Data
    {
        get { return (BarcodeImageItem)GetValue(DataProperty); }
        set { SetValue(DataProperty, value); }
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register("Data", typeof(BarcodeImageItem), typeof(QrCodeImageControl), new PropertyMetadata(null));


    public QrCodeImageControl()
    {
        InitializeComponent();
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

        // ViewModel.SaveCurrentStateToHistory();
        args.Data.SetStorageItems(new[] { file });
        args.Data.RequestedOperation = DataPackageOperation.Copy;
        deferral.Complete();
    }
}
