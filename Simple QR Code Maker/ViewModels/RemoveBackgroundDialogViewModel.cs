using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
#if WINDOWS
using Microsoft.Windows.AI.Imaging;
#endif
using Simple_QR_Code_Maker.Helpers;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Simple_QR_Code_Maker.ViewModels;

public sealed partial class RemoveBackgroundDialogViewModel : ObservableRecipient
{
    private SKBitmap? _sourceImage;

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

    public SKBitmap? ResultBitmap { get; private set; }

    public void Initialize(SKBitmap sourceImage)
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

#if WINDOWS
            StatusText = "Removing background…";

            using SoftwareBitmap softwareBitmap = ConvertToSoftwareBitmap(_sourceImage);

            using ImageObjectExtractor extractor =
                await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(softwareBitmap);

            ImageObjectExtractorHint hint = new(
                includeRects: [new RectInt32(0, 0, _sourceImage.Width, _sourceImage.Height)],
                includePoints: [],
                excludePoints: []);

            using SoftwareBitmap maskBitmap = extractor.GetSoftwareBitmapObjectMask(hint);

            SKBitmap result = ApplyMaskToBitmap(_sourceImage, maskBitmap);
            ResultBitmap = result;
            ResultImageSource = await ConvertBitmapToBitmapImageAsync(result);

            IsPrimaryEnabled = true;
            StatusText = "Background removed successfully";
#endif
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

    private static SKBitmap ToBgra(SKBitmap bitmap) =>
        bitmap.ColorType == SKColorType.Bgra8888 ? bitmap.Copy() : bitmap.Copy(SKColorType.Bgra8888);

    private static SoftwareBitmap ConvertToSoftwareBitmap(SKBitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        using SKBitmap bgra = ToBgra(bitmap);
        byte[] pixels = bgra.Bytes;

        SoftwareBitmap softwareBitmap = new(
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Premultiplied);

        softwareBitmap.CopyFromBuffer(pixels.AsBuffer());
        return softwareBitmap;
    }

    private static SKBitmap ApplyMaskToBitmap(SKBitmap source, SoftwareBitmap mask)
    {
        using SoftwareBitmap convertedMask = SoftwareBitmap.Convert(mask, BitmapPixelFormat.Gray8);
        int maskWidth = convertedMask.PixelWidth;
        int maskHeight = convertedMask.PixelHeight;
        byte[] maskPixels = new byte[maskWidth * maskHeight];
        convertedMask.CopyToBuffer(maskPixels.AsBuffer());

        int srcWidth = source.Width;
        int srcHeight = source.Height;

        using SKBitmap argbSource = ToBgra(source);
        byte[] srcPixels = argbSource.Bytes;
        int stride = argbSource.RowBytes;
        byte[] dstPixels = new byte[srcPixels.Length];

        for (int y = 0; y < srcHeight; y++)
        {
            for (int x = 0; x < srcWidth; x++)
            {
                int srcIdx = y * stride + x * 4;

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

        SKBitmap result = new(new SKImageInfo(srcWidth, srcHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        Marshal.Copy(dstPixels, 0, result.GetPixels(), dstPixels.Length);
        return result;
    }

    private static async Task<BitmapImage> ConvertBitmapToBitmapImageAsync(SKBitmap bitmap)
    {
        return await SkiaImaging.ToBitmapImageAsync(SkiaImaging.EncodePng(bitmap));
    }
}
