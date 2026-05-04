using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI.Imaging;
using Simple_QR_Code_Maker.Helpers;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Simple_QR_Code_Maker.ViewModels;

public sealed partial class RemoveBackgroundDialogViewModel : ObservableRecipient
{
    private Bitmap? _sourceImage;

    [ObservableProperty]
    public partial bool IsPrimaryEnabled { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusVisibility))]
    public partial bool IsStatusVisible { get; set; } = false;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsProcessing { get; set; } = false;

    [ObservableProperty]
    public partial BitmapImage? OriginalImageSource { get; set; }

    [ObservableProperty]
    public partial BitmapImage? ResultImageSource { get; set; }

    public Visibility StatusVisibility => IsStatusVisible ? Visibility.Visible : Visibility.Collapsed;

    public Bitmap? ResultBitmap { get; private set; }

    public void Initialize(Bitmap sourceImage)
    {
        _sourceImage = sourceImage;
    }

    public async Task ProcessAsync()
    {
        if (_sourceImage is null)
            return;

        OriginalImageSource = await ConvertBitmapToBitmapImageAsync(_sourceImage);

        IsStatusVisible = true;
        StatusText = "Preparing AI model…";
        IsProcessing = true;

        try
        {
            if (!await BackgroundRemovalHelper.CheckIsAvailableAsync())
            {
                StatusText = "Background removal is not available on this device.";
                IsPrimaryEnabled = false;
                return;
            }

            StatusText = "Removing background…";

            using SoftwareBitmap softwareBitmap = ConvertToSoftwareBitmap(_sourceImage);

            using ImageObjectExtractor extractor =
                await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(softwareBitmap);

            ImageObjectExtractorHint hint = new(
                includeRects: [new RectInt32(0, 0, _sourceImage.Width, _sourceImage.Height)],
                includePoints: [],
                excludePoints: []);

            using SoftwareBitmap maskBitmap = extractor.GetSoftwareBitmapObjectMask(hint);

            Bitmap result = ApplyMaskToBitmap(_sourceImage, maskBitmap);
            ResultBitmap = result;
            ResultImageSource = await ConvertBitmapToBitmapImageAsync(result);

            IsPrimaryEnabled = true;
            StatusText = "Background removed successfully";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Background removal failed: {ex}");
            StatusText = $"Failed: {ex.Message}";
            IsPrimaryEnabled = false;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private static SoftwareBitmap ConvertToSoftwareBitmap(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        using Bitmap argbBitmap = new(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(argbBitmap))
        {
            g.DrawImage(bitmap, 0, 0, width, height);
        }

        BitmapData data = argbBitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            int stride = data.Stride;
            byte[] pixels = new byte[stride * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            SoftwareBitmap softwareBitmap = new(
                BitmapPixelFormat.Bgra8,
                width,
                height,
                BitmapAlphaMode.Premultiplied);

            softwareBitmap.CopyFromBuffer(pixels.AsBuffer());
            return softwareBitmap;
        }
        finally
        {
            argbBitmap.UnlockBits(data);
        }
    }

    private static Bitmap ApplyMaskToBitmap(Bitmap source, SoftwareBitmap mask)
    {
        using SoftwareBitmap convertedMask = SoftwareBitmap.Convert(mask, BitmapPixelFormat.Gray8);
        int maskWidth = convertedMask.PixelWidth;
        int maskHeight = convertedMask.PixelHeight;
        byte[] maskPixels = new byte[maskWidth * maskHeight];
        convertedMask.CopyToBuffer(maskPixels.AsBuffer());

        int srcWidth = source.Width;
        int srcHeight = source.Height;
        Bitmap result = new(srcWidth, srcHeight, PixelFormat.Format32bppArgb);

        using Bitmap argbSource = new(srcWidth, srcHeight, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(argbSource))
        {
            g.DrawImage(source, 0, 0, srcWidth, srcHeight);
        }

        BitmapData srcData = argbSource.LockBits(
            new Rectangle(0, 0, srcWidth, srcHeight),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        BitmapData dstData = result.LockBits(
            new Rectangle(0, 0, srcWidth, srcHeight),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            byte[] srcPixels = new byte[srcData.Stride * srcHeight];
            byte[] dstPixels = new byte[dstData.Stride * srcHeight];
            Marshal.Copy(srcData.Scan0, srcPixels, 0, srcPixels.Length);

            for (int y = 0; y < srcHeight; y++)
            {
                for (int x = 0; x < srcWidth; x++)
                {
                    int srcIdx = y * srcData.Stride + x * 4;

                    int maskX = x * maskWidth / srcWidth;
                    int maskY = y * maskHeight / srcHeight;
                    int maskIdx = maskY * maskWidth + maskX;

                    // Mask: 0 = background, 255 = foreground — invert for alpha
                    byte alpha = (byte)(255 - maskPixels[maskIdx]);
                    dstPixels[srcIdx + 0] = srcPixels[srcIdx + 0]; // B
                    dstPixels[srcIdx + 1] = srcPixels[srcIdx + 1]; // G
                    dstPixels[srcIdx + 2] = srcPixels[srcIdx + 2]; // R
                    dstPixels[srcIdx + 3] = alpha;                  // A
                }
            }

            Marshal.Copy(dstPixels, 0, dstData.Scan0, dstPixels.Length);
        }
        finally
        {
            argbSource.UnlockBits(srcData);
            result.UnlockBits(dstData);
        }

        return result;
    }

    private static async Task<BitmapImage> ConvertBitmapToBitmapImageAsync(Bitmap bitmap)
    {
        using MemoryStream ms = new();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        BitmapImage bitmapImage = new();
        using InMemoryRandomAccessStream stream = new();
        await stream.WriteAsync(ms.ToArray().AsBuffer());
        stream.Seek(0);
        await bitmapImage.SetSourceAsync(stream);

        return bitmapImage;
    }
}
