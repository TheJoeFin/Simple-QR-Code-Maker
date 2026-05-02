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
        ImageColorPickerControl control = (ImageColorPickerControl)d;
        string? path = e.NewValue as string;
        string? previousPath = e.OldValue as string;

        if (string.IsNullOrWhiteSpace(path))
        {
            if (control._loadedFromDefaultImagePath)
                control.ClearLoadedImage();
            return;
        }

        bool shouldLoadDefaultImage =
            !control._imageLoaded
            || control._loadedFromDefaultImagePath
            || string.Equals(control._loadedImagePath, previousPath, StringComparison.Ordinal);

        if (!shouldLoadDefaultImage)
            return;

        if (control.IsLoaded)
            _ = control.LoadImageFromPathAsync(path, loadedFromDefaultImagePath: true);
        else
            control.Loaded += control.OnLoadedAutoLoad;
    }

    private void OnLoadedAutoLoad(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAutoLoad;
        if (!_imageLoaded && DefaultImagePath is string path && !string.IsNullOrEmpty(path))
            _ = LoadImageFromPathAsync(path, loadedFromDefaultImagePath: true);
    }

    // ── Private state ───────────────────────────────────────────────────────
    private Bitmap? _bitmap;
    private bool _imageLoaded;
    private int _loadRequestVersion;
    private string? _loadedImagePath;
    private bool _loadedFromDefaultImagePath;

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
        _loadRequestVersion++;
        SetLoadingState(false);
        _bitmap?.Dispose();
        _bitmap = null;
        _loadedImagePath = null;
        _loadedFromDefaultImagePath = false;
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
        bool hadLoadedImage = _imageLoaded;
        int loadRequestVersion = BeginImageLoad();
        PreparedImageData? preparedImage = null;
        try
        {
            await Task.Yield();
            preparedImage = await PrepareImageFromStorageFileAsync(file);
            if (loadRequestVersion != _loadRequestVersion)
                return;

            await ApplyPreparedImageAsync(preparedImage, file.Path, loadedFromDefaultImagePath: false);
            preparedImage = null;
        }
        catch (Exception ex)
        {
            _imageLoaded = hadLoadedImage;
            System.Diagnostics.Debug.WriteLine($"ImageColorPicker load failed: {ex}");
        }
        finally
        {
            preparedImage?.Bitmap.Dispose();
            EndImageLoad(loadRequestVersion);
        }
    }

    private async Task LoadImageFromPathAsync(string path, bool loadedFromDefaultImagePath)
    {
        if (!File.Exists(path)) return;
        bool hadLoadedImage = _imageLoaded;
        int loadRequestVersion = BeginImageLoad();
        PreparedImageData? preparedImage = null;
        try
        {
            await Task.Yield();
            preparedImage = await PrepareImageFromPathAsync(path);
            if (loadRequestVersion != _loadRequestVersion)
                return;

            await ApplyPreparedImageAsync(preparedImage, path, loadedFromDefaultImagePath);
            preparedImage = null;
        }
        catch (Exception ex)
        {
            _imageLoaded = hadLoadedImage;
            System.Diagnostics.Debug.WriteLine($"ImageColorPicker auto-load failed: {ex}");
        }
        finally
        {
            preparedImage?.Bitmap.Dispose();
            EndImageLoad(loadRequestVersion);
        }
    }

    private int BeginImageLoad()
    {
        _loadRequestVersion++;
        _imageLoaded = false;
        SetCrosshairVisible(false);
        SetLoadingState(true);
        return _loadRequestVersion;
    }

    private void EndImageLoad(int loadRequestVersion)
    {
        if (loadRequestVersion != _loadRequestVersion)
            return;

        SetLoadingState(false);
    }

    private void SetLoadingState(bool isLoading)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        LoadingProgressRing.IsActive = isLoading;
        PickImageButton.IsEnabled = !isLoading;
        PointerOverlay.IsHitTestVisible = !isLoading;
    }

    private void ClearLoadedImage()
    {
        _loadRequestVersion++;
        _bitmap?.Dispose();
        _bitmap = null;
        _imageLoaded = false;
        _loadedImagePath = null;
        _loadedFromDefaultImagePath = false;
        PreviewImage.Source = null;
        NoImagePlaceholder.Visibility = Visibility.Visible;
        SuggestedColorsPanel.Visibility = Visibility.Collapsed;
        SwatchRow.Children.Clear();
        SetCrosshairVisible(false);
        SetLoadingState(false);
    }

    private async Task ApplyPreparedImageAsync(PreparedImageData preparedImage, string? imagePath, bool loadedFromDefaultImagePath)
    {
        _bitmap?.Dispose();
        _bitmap = preparedImage.Bitmap;
        _loadedImagePath = imagePath;
        _loadedFromDefaultImagePath = loadedFromDefaultImagePath;

        PreviewImage.Source = await CreateBitmapImageAsync(preparedImage.PreviewBytes);
        ComputeLetterboxGeometry(_bitmap.Width, _bitmap.Height);

        NoImagePlaceholder.Visibility = Visibility.Collapsed;
        SuggestedColorsPanel.Visibility = Visibility.Visible;
        _imageLoaded = true;
        PopulateSwatches(preparedImage.DominantColors);
    }

    private static async Task<PreparedImageData> PrepareImageFromStorageFileAsync(StorageFile file)
    {
        using Stream sourceStream = await file.OpenStreamForReadAsync();
        using MemoryStream bufferStream = new();
        await sourceStream.CopyToAsync(bufferStream);
        byte[] fileBytes = bufferStream.ToArray();

        return await Task.Run(() =>
        {
            using MemoryStream imageStream = new(fileBytes);
            using MagickImage magick = new(imageStream);
            magick.AutoOrient();
            return PrepareImageData(magick);
        });
    }

    private static Task<PreparedImageData> PrepareImageFromPathAsync(string path)
    {
        return Task.Run(() =>
        {
            using MagickImage magick = new(path);
            magick.AutoOrient();
            return PrepareImageData(magick);
        });
    }

    private static PreparedImageData PrepareImageData(MagickImage magick)
    {
        using Bitmap rawBitmap = ImageProcessingHelper.ConvertToBitmap(magick);
        Bitmap bitmapCopy = new(rawBitmap);
        using MemoryStream previewStream = new();
        magick.Write(previewStream, MagickFormat.Png);

        return new PreparedImageData(bitmapCopy, previewStream.ToArray(), ExtractDominantColors(magick));
    }

    private static async Task<BitmapImage> CreateBitmapImageAsync(byte[] previewBytes)
    {
        BitmapImage bitmapImage = new();
        using InMemoryRandomAccessStream stream = new();
        await stream.WriteAsync(previewBytes.AsBuffer());
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
        using MagickImage img = (MagickImage)source.Clone();

        // Larger thumbnail gives better color representation than 100×100
        img.Thumbnail(new MagickGeometry(150, 150));

        // Quantize to more colors so we have headroom to filter
        // Magick.NET Q16: channels are ushort 0–65535; divide by 257 to get 0–255
        img.Quantize(new QuantizeSettings
        {
            Colors = 24,
            DitherMethod = DitherMethod.No,
        });

        // Build candidates sorted by frequency; skip transparent pixels
        List<WinColor> candidates = img.Histogram()
            .OrderByDescending(kv => kv.Value)
            .Select(kv => WinColor.FromArgb(
                (byte)(kv.Key.A / 257),
                (byte)(kv.Key.R / 257),
                (byte)(kv.Key.G / 257),
                (byte)(kv.Key.B / 257)))
            .Where(c => c.A >= 200)
            .ToList();

        // Prefer saturated mid-tone colors (brand-like); skip whites, blacks, grays
        List<WinColor> pool = candidates
            .Where(c =>
            {
                (_, double s, double l) = ToHsl(c);
                return s >= 0.12 && l >= 0.07 && l <= 0.90;
            })
            .ToList();

        // Fall back to all opaque candidates if the image is desaturated/monochrome
        if (pool.Count < 2)
            pool = candidates;

        // Greedily pick visually distinct colors
        const double MinDistSq = 50.0 * 50.0;
        List<WinColor> result = [];
        foreach (WinColor color in pool)
        {
            if (result.Count >= 6) break;
            if (result.All(existing => ColorDistSq(existing, color) >= MinDistSq))
                result.Add(color);
        }

        // Relax constraints and draw from full pool if we still need more swatches
        if (result.Count < 6)
        {
            const double RelaxedDistSq = 30.0 * 30.0;
            foreach (WinColor color in candidates)
            {
                if (result.Count >= 6) break;
                if (result.All(existing => ColorDistSq(existing, color) >= RelaxedDistSq))
                    result.Add(color);
            }
        }

        return result;
    }

    private static (double H, double S, double L) ToHsl(WinColor c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0;
        double s = 0.0, h = 0.0;

        if (max != min)
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6.0;
            else if (max == g) h = ((b - r) / d + 2) / 6.0;
            else h = ((r - g) / d + 4) / 6.0;
        }

        return (h, s, l);
    }

    private static double ColorDistSq(WinColor a, WinColor b)
    {
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return dr * dr + dg * dg + db * db;
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

    private sealed class PreparedImageData(Bitmap bitmap, byte[] previewBytes, List<WinColor> dominantColors)
    {
        public Bitmap Bitmap { get; } = bitmap;

        public byte[] PreviewBytes { get; } = previewBytes;

        public List<WinColor> DominantColors { get; } = dominantColors;
    }
}
