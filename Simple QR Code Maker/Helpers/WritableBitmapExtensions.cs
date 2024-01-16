using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using Windows.Storage;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace Simple_QR_Code_Maker.Helpers;

public static class WritableBitmapExtensions
{
    public static async Task<bool> SavePngToStorageFile(this WriteableBitmap writeableBitmap, StorageFile storageFile)
    {
        try
        {
            using IRandomAccessStream stream = await storageFile.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder pngEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            Stream pixelStream = writeableBitmap.PixelBuffer.AsStream();
            byte[] bytes = new byte[pixelStream.Length];
            await pixelStream.ReadAsync(bytes);

            pngEncoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                (uint)writeableBitmap.PixelWidth,
                (uint)writeableBitmap.PixelHeight,
                96.0,
                96.0,
                bytes);
            await pngEncoder.FlushAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save PNG of QR Code: {ex.Message}");
            return false;
        }

        return true;
    }
}