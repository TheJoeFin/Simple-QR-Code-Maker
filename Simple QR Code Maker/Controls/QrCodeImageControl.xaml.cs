using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class QrCodeImageControl : UserControl
{
    public BarcodeImageItem Data
    {
        get { return (BarcodeImageItem)GetValue(DataProperty); }
        set { SetValue(DataProperty, value); }
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(BarcodeImageItem), typeof(QrCodeImageControl), new PropertyMetadata(null, OnDataChanged));

    public QrCodeImageControl()
    {
        InitializeComponent();
    }

    // Assign the preview image from code-behind rather than via x:Bind. Binding the Image.Source
    // through the Data DependencyProperty to the non-observable CodeAsBitmap leaf does not reliably
    // re-evaluate on the Uno Skia head, leaving the preview blank even though the bitmap is valid.
    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not QrCodeImageControl control)
            return;

        ImageSource? source = (e.NewValue as BarcodeImageItem)?.CodeAsBitmap;
        control.QrCodeImage.Source = source;

        // Diagnostics: surface whether the file-backed BitmapImage actually loads on the Skia head.
        // Logged to a file (the Debug output isn't visible in all run configurations).
        if (source is BitmapImage bitmapImage)
        {
            DiagLog($"OnDataChanged source set, UriSource={bitmapImage.UriSource}");
            bitmapImage.ImageOpened += (s, _) =>
                DiagLog($"ImageOpened {(s as BitmapImage)?.UriSource}");
            bitmapImage.ImageFailed += (s, args) =>
                DiagLog($"ImageFailed {(s as BitmapImage)?.UriSource}: {args.ErrorMessage}");
        }
        else
        {
            DiagLog($"OnDataChanged source is not a BitmapImage: {source?.GetType().FullName ?? "null"}");
        }
    }

    private static void DiagLog(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[QR PREVIEW] {message}");
        try
        {
            string path = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "qr-preview-log.txt");
            System.IO.File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // best-effort diagnostics only
        }
    }

    private Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    private async void QrCodeImage_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (Data?.CodePngBytes is null)
            return;

        DragOperationDeferral deferral = args.GetDeferral();
        StorageFolder folder = ApplicationData.Current.LocalCacheFolder;
        string? imageNameFileName = $"{Data.CodeAsText}" ?? "QR_Code";
        // remove characters that are not allowed in file names
        imageNameFileName = imageNameFileName.ToSafeFileName();
        imageNameFileName += ".png";
        StorageFile file = await folder.CreateFileAsync(imageNameFileName, CreationCollisionOption.ReplaceExisting);
        bool success = await Data.SaveCodeAsPngFile(file);

        if (!success)
        {
            deferral.Complete();
            return;
        }

        WeakReferenceMessenger.Default.Send(new SaveHistoryMessage());
        args.Data.SetStorageItems(new[] { file });
        args.Data.RequestedOperation = DataPackageOperation.Copy;
        deferral.Complete();
    }
}
