using ImageMagick;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace Simple_QR_Code_Maker.Helpers;

/// <summary>
/// Central cross-platform imaging helpers built on SkiaSharp. Replaces the previous
/// System.Drawing/GDI+ pipeline so encoding, decoding, and display all work on the Uno
/// Skia head as well as the WinUI head.
/// </summary>
public static class SkiaImaging
{
    public static SKBitmap DecodeStream(Stream stream) => SKBitmap.Decode(stream);

    public static SKBitmap DecodeFile(string path) => SKBitmap.Decode(path);

    /// <summary>
    /// Bridges a Magick.NET image (still used for loading and heavy processing) to an SKBitmap
    /// via an in-memory PNG, preserving the alpha channel.
    /// </summary>
    public static SKBitmap FromMagick(MagickImage image)
    {
        byte[] png = image.ToByteArray(MagickFormat.Png32);
        return SKBitmap.Decode(png);
    }

    public static byte[] EncodePng(SKBitmap bitmap)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        return EncodePng(image);
    }

    public static byte[] EncodePng(SKImage image)
    {
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Loads PNG bytes into a BitmapImage usable as an Image.Source on both heads.
    /// Uses SetSourceAsync (the synchronous SetSource is not implemented on Uno/Skia).
    /// </summary>
    public static async Task<BitmapImage> ToBitmapImageAsync(byte[] pngBytes)
    {
        BitmapImage bitmapImage = new();
        using InMemoryRandomAccessStream stream = new();
        await stream.WriteAsync(pngBytes.AsBuffer());
        stream.Seek(0);
        await bitmapImage.SetSourceAsync(stream);
        return bitmapImage;
    }
}
