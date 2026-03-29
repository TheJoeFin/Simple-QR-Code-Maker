using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Helpers;
using System.Drawing;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
using GdiColor = System.Drawing.Color;
using WinColor = Windows.UI.Color;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class ImageColorPickerControl : UserControl
{
    // ── Color DependencyProperty ────────────────────────────────────────────
    public static readonly DependencyProperty ColorProperty =
        DependencyProperty.Register(
            nameof(Color),
            typeof(WinColor),
            typeof(ImageColorPickerControl),
            new PropertyMetadata(WinColor.FromArgb(255, 0, 0, 0)));

    public WinColor Color
    {
        get => (WinColor)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    // ── DefaultImagePath DependencyProperty ────────────────────────────────
    public static readonly DependencyProperty DefaultImagePathProperty =
        DependencyProperty.Register(
            nameof(DefaultImagePath),
            typeof(string),
            typeof(ImageColorPickerControl),
            new PropertyMetadata(null, OnDefaultImagePathChanged));

    public string? DefaultImagePath
    {
        get => (string?)GetValue(DefaultImagePathProperty);
        set => SetValue(DefaultImagePathProperty, value);
    }

    private static void OnDefaultImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ImageColorPickerControl)d;
        if (e.NewValue is not string path || string.IsNullOrEmpty(path) || control._imageLoaded)
            return;

        if (control.IsLoaded)
            _ = control.LoadImageFromPathAsync(path);
        else
            control.Loaded += control.OnLoadedAutoLoad;
    }

    private void OnLoadedAutoLoad(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAutoLoad;
        if (!_imageLoaded && DefaultImagePath is string path && !string.IsNullOrEmpty(path))
            _ = LoadImageFromPathAsync(path);
    }

    // ── Private state ───────────────────────────────────────────────────────
    private Bitmap? _bitmap;
    private bool _imageLoaded;

    // Letterbox geometry — rendered image bounds inside the 280×200 container
    private double _renderWidth;
    private double _renderHeight;
    private double _offsetX;
    private double _offsetY;

    public ImageColorPickerControl()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _bitmap?.Dispose();
        _bitmap = null;
    }

    // ── Image loading ───────────────────────────────────────────────────────

    /// <summary>
    /// Fired just before the file picker opens so the parent flyout can close itself first,
    /// preventing the picker overlay from stacking on top of the open flyout.
    /// </summary>
    public event EventHandler? PickingImage;

    private async void PickImageButton_Click(object sender, RoutedEventArgs e)
    {
        PickingImage?.Invoke(this, EventArgs.Empty);

        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".webp");

        IntPtr windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, windowHandle);

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
            await LoadImageFromFileAsync(file);
    }

    private async Task LoadImageFromFileAsync(StorageFile file)
    {
        _imageLoaded = false;
        try
        {
            using MagickImage magick = await ImageProcessingHelper.LoadImageFromStorageFile(file);
            await LoadMagickImageAsync(magick);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ImageColorPicker load failed: {ex}");
        }
    }

    private async Task LoadImageFromPathAsync(string path)
    {
        if (!File.Exists(path)) return;
        _imageLoaded = false;
        try
        {
            using MagickImage magick = new(path);
            magick.AutoOrient();
            await LoadMagickImageAsync(magick);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ImageColorPicker auto-load failed: {ex}");
        }
    }

    private async Task LoadMagickImageAsync(MagickImage magick)
    {
        _bitmap?.Dispose();
        using Bitmap rawBitmap = ImageProcessingHelper.ConvertToBitmap(magick);
        _bitmap = new Bitmap(rawBitmap); // stream-independent copy

        PreviewImage.Source = await MagickImageToBitmapImageAsync(magick);
        ComputeLetterboxGeometry(_bitmap.Width, _bitmap.Height);

        NoImagePlaceholder.Visibility = Visibility.Collapsed;
        SuggestedColorsPanel.Visibility = Visibility.Visible;
        _imageLoaded = true;

        MagickImage clone = (MagickImage)magick.Clone();
        List<WinColor> dominantColors = await Task.Run(() =>
        {
            using var _ = clone;
            return ExtractDominantColors(clone);
        });
        PopulateSwatches(dominantColors);
    }

    private static async Task<BitmapImage> MagickImageToBitmapImageAsync(MagickImage magick)
    {
        using MemoryStream ms = new();
        await magick.WriteAsync(ms, MagickFormat.Png);
        ms.Position = 0;

        BitmapImage bitmapImage = new();
        using InMemoryRandomAccessStream stream = new();
        await stream.WriteAsync(ms.ToArray().AsBuffer());
        stream.Seek(0);
        await bitmapImage.SetSourceAsync(stream);
        return bitmapImage;
    }

    // ── Letterbox geometry ──────────────────────────────────────────────────

    private void ComputeLetterboxGeometry(int imageWidth, int imageHeight)
    {
        const double containerW = 280.0;
        const double containerH = 200.0;

        double scale = Math.Min(containerW / imageWidth, containerH / imageHeight);
        _renderWidth = imageWidth * scale;
        _renderHeight = imageHeight * scale;
        _offsetX = (containerW - _renderWidth) / 2.0;
        _offsetY = (containerH - _renderHeight) / 2.0;
    }

    // ── Coordinate mapping ──────────────────────────────────────────────────

    private System.Drawing.Point? CanvasToImagePixel(Windows.Foundation.Point canvasPoint)
    {
        if (_bitmap is null) return null;

        double relX = canvasPoint.X - _offsetX;
        double relY = canvasPoint.Y - _offsetY;

        if (relX < 0 || relX >= _renderWidth || relY < 0 || relY >= _renderHeight)
            return null;

        int px = Math.Clamp((int)(relX / _renderWidth * _bitmap.Width), 0, _bitmap.Width - 1);
        int py = Math.Clamp((int)(relY / _renderHeight * _bitmap.Height), 0, _bitmap.Height - 1);
        return new System.Drawing.Point(px, py);
    }

    // ── Pointer event handlers ──────────────────────────────────────────────

    private void PointerOverlay_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_imageLoaded) return;
        UpdateCrosshair(e.GetCurrentPoint(PointerOverlay).Position);
    }

    private void PointerOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_imageLoaded || _bitmap is null) return;

        System.Drawing.Point? pixel = CanvasToImagePixel(e.GetCurrentPoint(PointerOverlay).Position);
        if (!pixel.HasValue) return;

        GdiColor gdi = _bitmap.GetPixel(pixel.Value.X, pixel.Value.Y);
        Color = WinColor.FromArgb(gdi.A, gdi.R, gdi.G, gdi.B);
    }

    private void PointerOverlay_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        SetCrosshairVisible(false);
    }

    private void UpdateCrosshair(Windows.Foundation.Point pos)
    {
        const double W = 280.0;
        const double H = 200.0;

        CrosshairH.X1 = CrosshairHShadow.X1 = 0;
        CrosshairH.Y1 = CrosshairHShadow.Y1 = pos.Y;
        CrosshairH.X2 = CrosshairHShadow.X2 = W;
        CrosshairH.Y2 = CrosshairHShadow.Y2 = pos.Y;

        CrosshairV.X1 = CrosshairVShadow.X1 = pos.X;
        CrosshairV.Y1 = CrosshairVShadow.Y1 = 0;
        CrosshairV.X2 = CrosshairVShadow.X2 = pos.X;
        CrosshairV.Y2 = CrosshairVShadow.Y2 = H;

        SetCrosshairVisible(true);

        System.Drawing.Point? pixel = CanvasToImagePixel(pos);
        if (pixel.HasValue && _bitmap is not null)
        {
            GdiColor gdi = _bitmap.GetPixel(pixel.Value.X, pixel.Value.Y);
            WinColor preview = WinColor.FromArgb(gdi.A, gdi.R, gdi.G, gdi.B);
            ColorPreviewSwatch.Background = new SolidColorBrush(preview);
            ColorPreviewSwatch.Visibility = Visibility.Visible;

            double swatchX = Math.Min(pos.X + 14, W - 26);
            double swatchY = Math.Max(pos.Y - 36, 2);
            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(ColorPreviewSwatch, swatchX);
            Microsoft.UI.Xaml.Controls.Canvas.SetTop(ColorPreviewSwatch, swatchY);
        }
        else
        {
            ColorPreviewSwatch.Visibility = Visibility.Collapsed;
        }
    }

    private void SetCrosshairVisible(bool visible)
    {
        var v = visible ? Visibility.Visible : Visibility.Collapsed;
        CrosshairH.Visibility = v;
        CrosshairV.Visibility = v;
        CrosshairHShadow.Visibility = v;
        CrosshairVShadow.Visibility = v;
        if (!visible) ColorPreviewSwatch.Visibility = Visibility.Collapsed;
    }

    // ── Dominant color extraction ───────────────────────────────────────────

    private static List<WinColor> ExtractDominantColors(MagickImage source)
    {
        using MagickImage thumb = (MagickImage)source.Clone();
        thumb.Thumbnail(new MagickGeometry(100, 100));
        thumb.Quantize(new QuantizeSettings
        {
            Colors = 6,
            DitherMethod = DitherMethod.No,
        });

        // Magick.NET Q16: channels are ushort 0–65535; divide by 257 to get 0–255
        return thumb.Histogram()
            .OrderByDescending(kv => kv.Value)
            .Take(6)
            .Select(kv => WinColor.FromArgb(
                (byte)(kv.Key.A / 257),
                (byte)(kv.Key.R / 257),
                (byte)(kv.Key.G / 257),
                (byte)(kv.Key.B / 257)))
            .ToList();
    }

    // ── Swatch row ──────────────────────────────────────────────────────────

    private void PopulateSwatches(List<WinColor> colors)
    {
        SwatchRow.Children.Clear();

        foreach (WinColor color in colors)
        {
            Button btn = new()
            {
                Width = 32,
                Height = 32,
                MinWidth = 32,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.White),
                BorderThickness = new Thickness(1.5),
                Tag = color,
            };
            ToolTipService.SetToolTip(btn, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
            btn.Click += SwatchButton_Click;
            SwatchRow.Children.Add(btn);
        }
    }

    private void SwatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WinColor color)
            Color = color;
    }
}
