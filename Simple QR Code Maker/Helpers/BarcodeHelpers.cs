using Microsoft.UI.Xaml.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;
using ZXing.Rendering;
using ZXing.Windows.Compatibility;
using static ZXing.Rendering.SvgRenderer;

namespace Simple_QR_Code_Maker.Helpers;

public static class BarcodeHelpers
{
    public static WriteableBitmap GetQrCodeBitmapFromText(string text, ErrorCorrectionLevel correctionLevel, System.Drawing.Color foreground, System.Drawing.Color background)
    {
        BitmapRenderer bitmapRenderer = new()
        {
            Foreground = foreground,
            Background = background
        };

        BarcodeWriter barcodeWriter = new()
        {
            Format = ZXing.BarcodeFormat.QR_CODE,
            Renderer = bitmapRenderer,
        };

        EncodingOptions encodingOptions = new()
        {
            Width = 1024,
            Height = 1024,
            Margin = 2,
        };
        encodingOptions.Hints.Add(ZXing.EncodeHintType.ERROR_CORRECTION, correctionLevel);
        barcodeWriter.Options = encodingOptions;

        using Bitmap bitmap = barcodeWriter.Write(text);
        using MemoryStream ms = new();
        bitmap.Save(ms, ImageFormat.Png);
        WriteableBitmap bitmapImage = new(encodingOptions.Width, encodingOptions.Height);
        ms.Position = 0;
        bitmapImage.SetSource(ms.AsRandomAccessStream());

        return bitmapImage;
    }

    public static SvgImage GetSvgQrCodeForText(string text, ErrorCorrectionLevel correctionLevel)
    {
        BarcodeWriterSvg barcodeWriter = new()
        {
            Format = BarcodeFormat.QR_CODE,
            Renderer = new SvgRenderer()
        };

        EncodingOptions encodingOptions = new()
        {
            Width = 500,
            Height = 500,
            Margin = 5,
        };
        encodingOptions.Hints.Add(EncodeHintType.ERROR_CORRECTION, correctionLevel);
        barcodeWriter.Options = encodingOptions;

        SvgImage svg = barcodeWriter.Write(text);

        return svg;
    }
}