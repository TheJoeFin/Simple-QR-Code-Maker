using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Windows.Storage;
using Windows.Storage.Streams;
using ZXing.QrCode.Internal;
using static ZXing.Rendering.SvgRenderer;

namespace Simple_QR_Code_Maker.Models;
public class BarcodeImageItem
{
    public string CodeAsText { get; set; } = string.Empty;

    public bool IsCodeUrl => Uri.IsWellFormedUriString(CodeAsText, UriKind.Absolute);

    public bool IsAppShowingUrlWarnings { get; set; } = true;

    public bool UrlWarning => !IsCodeUrl && IsAppShowingUrlWarnings;

    public WriteableBitmap? CodeAsBitmap { get; set; }

    public async Task<bool> SaveCodeAsPngFile(StorageFile file)
    {
        if (CodeAsBitmap is null)
            return false;

        return await CodeAsBitmap.SavePngToStorageFile(file);
    }

    public async Task<bool> SaveCodeAsSvgFile(StorageFile file, System.Drawing.Color foreground, System.Drawing.Color background, ErrorCorrectionLevel correctionLevel)
    {
        try
        {
            SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(CodeAsText, correctionLevel, foreground, background);
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

    public string GetCodeAsSvgText(System.Drawing.Color foreground, System.Drawing.Color background, ErrorCorrectionLevel correctionLevel)
    {
        try
        {
            SvgImage svgImage = BarcodeHelpers.GetSvgQrCodeForText(CodeAsText, correctionLevel, foreground, background);
            return svgImage.Content;
        }
        catch
        {
            return string.Empty;
        }
    }
}
