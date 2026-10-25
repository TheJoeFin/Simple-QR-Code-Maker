using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Models;
using System.Drawing;
using System.Drawing.Imaging;
using Windows.Storage;
using ZXing;
using ZXing.QrCode.Internal;
using SvgImage = ZXing.Rendering.SvgRenderer.SvgImage;

namespace Simple_QR_Code_Maker.Helpers;

/// <summary>WinUI adapter for the headless, tested QR renderer.</summary>
public static class BarcodeHelpers
{
    internal static QRCode EncodeQrCode(string text, ErrorCorrectionLevel correctionLevel)
        => QrCodeRenderer.EncodeQrCode(text, correctionLevel);

    public static int GetMaxLogoSizePercentage(ErrorCorrectionLevel correctionLevel)
        => QrCodeRenderer.GetMaxLogoSizePercentage(correctionLevel);

    public static Bitmap RasterizeSvgToBitmap(string svgContent, int width, int height)
        => QrCodeRenderer.RasterizeSvgToBitmap(svgContent, width, height);

    public static WriteableBitmap GetQrCodeBitmapFromText(string text, ErrorCorrectionLevel correctionLevel, Color foreground, Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0, double logoPaddingPixels = 8.0, double qrPaddingModules = 2.0, QrFramePreset framePreset = QrFramePreset.None, string? frameText = null)
    {
        using Bitmap bitmap = QrCodeRenderer.GetQrCodeBitmapFromText(text, correctionLevel, foreground, background, logoImage, logoSizePercentage, logoPaddingPixels, qrPaddingModules, framePreset, frameText);
        using MemoryStream stream = new();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        WriteableBitmap result = new(bitmap.Width, bitmap.Height);
        result.SetSource(stream.AsRandomAccessStream());
        return result;
    }

    public static void SaveQrCodePngToStream(Stream outputStream, string text, ErrorCorrectionLevel correctionLevel, Color foreground, Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0, double logoPaddingPixels = 8.0, double qrPaddingModules = 2.0, QrFramePreset framePreset = QrFramePreset.None, string? frameText = null)
        => QrCodeRenderer.SaveQrCodePngToStream(outputStream, text, correctionLevel, foreground, background, logoImage, logoSizePercentage, logoPaddingPixels, qrPaddingModules, framePreset, frameText);

    internal static void SaveQrCodePngToStream(Stream outputStream, string text, ErrorCorrectionLevel correctionLevel, Color foreground, Color background, Bitmap? logoImage, double logoSizePercentage, double logoPaddingPixels, double qrPaddingModules, QrFramePreset framePreset, string? frameText, out QrImageLayoutMetrics imageLayout)
        => QrCodeRenderer.SaveQrCodePngToStream(outputStream, text, correctionLevel, foreground, background, logoImage, logoSizePercentage, logoPaddingPixels, qrPaddingModules, framePreset, frameText, out imageLayout);

    internal static QrImageLayoutMetrics GetQrImageLayoutMetrics(string text, ErrorCorrectionLevel correctionLevel, double qrPaddingModules = 2.0, QrFramePreset framePreset = QrFramePreset.None)
        => QrCodeRenderer.GetQrImageLayoutMetrics(text, correctionLevel, qrPaddingModules, framePreset);

    public static bool IsSizeRecommendationAvailableForPadding(double qrPaddingModules)
        => QrCodeRenderer.IsSizeRecommendationAvailableForPadding(qrPaddingModules);

    public static double SmallestCodeSide(double distance, int numberOfBlocks, double qrPaddingModules = 2.0)
        => QrCodeRenderer.SmallestCodeSide(distance, numberOfBlocks, qrPaddingModules);

    public static double ContrastRatioLossFrac(double contrastRatio)
        => QrCodeRenderer.ContrastRatioLossFrac(contrastRatio);

    public static bool TryGetMinimumRecommendedSideMillimeters(double distance, int numberOfBlocks, Windows.UI.Color foreground, Windows.UI.Color background, out double minimumSideMillimeters, double qrPaddingModules = 2.0)
        => QrCodeRenderer.TryGetMinimumRecommendedSideMillimeters(distance, numberOfBlocks, foreground, background, out minimumSideMillimeters, qrPaddingModules);

    public static QrCodeSizeRecommendation GetSizeRecommendation(double distance, int numberOfBlocks, Windows.UI.Color foreground, Windows.UI.Color background, double qrPaddingModules = 2.0)
        => QrCodeRenderer.GetSizeRecommendation(distance, numberOfBlocks, foreground, background, qrPaddingModules);

    public static SvgImage GetSvgQrCodeForText(string text, ErrorCorrectionLevel correctionLevel, Color foreground, Color background, Bitmap? logoImage = null, double logoSizePercentage = 20.0, double logoPaddingPixels = 8.0, string? logoSvgContent = null, double qrPaddingModules = 2.0, QrFramePreset framePreset = QrFramePreset.None, string? frameText = null)
        => QrCodeRenderer.GetSvgQrCodeForText(text, correctionLevel, foreground, background, logoImage, logoSizePercentage, logoPaddingPixels, logoSvgContent, qrPaddingModules, framePreset, frameText);

    public static IEnumerable<(string, Result)> GetStringsFromImageFile(StorageFile storageFile)
        => QrCodeRenderer.GetStringsFromImageFile(storageFile);

    public static IEnumerable<(string, Result)> GetStringsFromBitmap(Bitmap bitmap)
        => QrCodeRenderer.GetStringsFromBitmap(bitmap);

    public static int NormalizeQrPaddingModules(double qrPaddingModules)
        => QrCodeRenderer.NormalizeQrPaddingModules(qrPaddingModules);
}