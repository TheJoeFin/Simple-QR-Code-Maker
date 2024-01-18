using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Extensions;
using Windows.Storage;
using static ZXing.Rendering.SvgRenderer;
using Windows.Storage.Streams;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Models;
public class BarcodeImageItem
{
    public string CodeAsText { get; set; } = string.Empty;
    public WriteableBitmap? CodeAsBitmap { get; set; }

    public async Task<bool> SaveCodeAsPngFile(StorageFile file)
    {
        if (CodeAsBitmap is null)
            return false;

        return await CodeAsBitmap.SavePngToStorageFile(file);
    }

    public async Task<bool> SaveCodeAsSvgFile(StorageFile file, System.Drawing.Color foreground, System.Drawing.Color background)
    {
        try
        {
            SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(CodeAsText, ErrorCorrectionLevel.M, foreground, background);
            using IRandomAccessStream randomAccessStream = await file.OpenAsync(FileAccessMode.ReadWrite);
            DataWriter dataWriter = new(randomAccessStream);
            dataWriter.WriteString(svgImage.Content);
            await dataWriter.StoreAsync();
        }
        catch
        {
            return false;
        }

        return true;
    }
}
