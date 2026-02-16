using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.AI.Imaging;
using Simple_QR_Code_Maker.Helpers;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Simple_QR_Code_Maker.Controls;

[ObservableObject]
public sealed partial class RemoveBackgroundDialog : ContentDialog
{
    private readonly Bitmap _sourceImage;

    [ObservableProperty]
    private bool isPrimaryEnabled = false;

    /// <summary>
    /// The resulting bitmap with background removed, or null if cancelled / failed.
    /// </summary>
    public Bitmap? ResultBitmap { get; private set; }

    public RemoveBackgroundDialog(Bitmap sourceImage)
    {
        _sourceImage = sourceImage;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Show the original image
        OriginalImage.Source = await ConvertBitmapToBitmapImage(_sourceImage);

        // Start background removal
        StatusText.Visibility = Visibility.Visible;
        StatusText.Text = "Preparing AI model…";
        ProcessingRing.IsActive = true;

        try
        {
            // Ensure the AI model is available on this device
            if (!await BackgroundRemovalHelper.CheckIsAvailableAsync())
            {
                throw new InvalidOperationException("Background removal is not available on this device.");
            }

            StatusText.Text = "Removing background…";

            // Convert System.Drawing.Bitmap → SoftwareBitmap (BGRA8 Premultiplied)
            SoftwareBitmap softwareBitmap = ConvertToSoftwareBitmap(_sourceImage);

            // Create the extractor and get the foreground mask
            using ImageObjectExtractor extractor =
                await ImageObjectExtractor.CreateWithSoftwareBitmapAsync(softwareBitmap);

            // Hint with the entire image rect as the region of interest
            ImageObjectExtractorHint hint = new(
                includeRects: [new RectInt32(0, 0, _sourceImage.Width, _sourceImage.Height)],
                includePoints: [],
                excludePoints: []);
            SoftwareBitmap maskBitmap = extractor.GetSoftwareBitmapObjectMask(hint);

            // Apply the mask to produce a transparent-background image
            Bitmap result = ApplyMaskToBitmap(_sourceImage, maskBitmap);

            ResultBitmap = result;
            ResultImage.Source = await ConvertBitmapToBitmapImage(result);

            IsPrimaryEnabled = true;
            StatusText.Text = "Background removed successfully";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Background removal failed: {ex}");
            StatusText.Text = $"Failed: {ex.Message}";
            IsPrimaryEnabled = false;
        }
        finally
        {
            ProcessingRing.IsActive = false;
        }
    }

    /// <summary>
    /// Converts a <see cref="System.Drawing.Bitmap"/> to a WinRT <see cref="SoftwareBitmap"/> (BGRA8, Premultiplied).
    /// </summary>
    private static SoftwareBitmap ConvertToSoftwareBitmap(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        // Force a 32bpp ARGB copy so the pixel layout is predictable
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
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            // GDI+ stores pixels as BGRA which matches BitmapPixelFormat.Bgra8
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

    /// <summary>
    /// Applies a grayscale mask (from <see cref="ImageObjectExtractor"/>) to the source bitmap,
    /// producing a new bitmap with transparent background.
    /// </summary>
    private static Bitmap ApplyMaskToBitmap(Bitmap source, SoftwareBitmap mask)
    {
        // Read mask pixels
        SoftwareBitmap convertedMask = SoftwareBitmap.Convert(mask, BitmapPixelFormat.Gray8);
        int maskWidth = convertedMask.PixelWidth;
        int maskHeight = convertedMask.PixelHeight;
        byte[] maskPixels = new byte[maskWidth * maskHeight];
        convertedMask.CopyToBuffer(maskPixels.AsBuffer());

        // Create result bitmap with alpha
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
            System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcPixels, 0, srcPixels.Length);

            for (int y = 0; y < srcHeight; y++)
            {
                for (int x = 0; x < srcWidth; x++)
                {
                    int srcIdx = y * srcData.Stride + x * 4;

                    // Map source pixel to mask pixel (mask may differ in size)
                    int maskX = x * maskWidth / srcWidth;
                    int maskY = y * maskHeight / srcHeight;
                    int maskIdx = maskY * maskWidth + maskX;

                    byte maskValue = maskPixels[maskIdx];

                    // BGRA layout: B=0, G=1, R=2, A=3
                    // Mask: 0 = background, 255 = foreground object — invert for alpha
                    byte alpha = (byte)(255 - maskValue);
                    dstPixels[srcIdx + 0] = srcPixels[srcIdx + 0]; // B
                    dstPixels[srcIdx + 1] = srcPixels[srcIdx + 1]; // G
                    dstPixels[srcIdx + 2] = srcPixels[srcIdx + 2]; // R
                    dstPixels[srcIdx + 3] = alpha;                  // A
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(dstPixels, 0, dstData.Scan0, dstPixels.Length);
        }
        finally
        {
            argbSource.UnlockBits(srcData);
            result.UnlockBits(dstData);
        }

        return result;
    }

    private static async Task<BitmapImage> ConvertBitmapToBitmapImage(Bitmap bitmap)
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
