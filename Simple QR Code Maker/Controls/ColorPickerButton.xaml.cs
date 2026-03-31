using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class ColorPickerButton : UserControl
{
    // ── Color DependencyProperty ────────────────────────────────────────────
    public static readonly DependencyProperty ColorProperty =
        DependencyProperty.Register(
            nameof(Color),
            typeof(Windows.UI.Color),
            typeof(ColorPickerButton),
            new PropertyMetadata(Windows.UI.Color.FromArgb(255, 0, 0, 0)));

    public Windows.UI.Color Color
    {
        get => (Windows.UI.Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    // ── DefaultImagePath DependencyProperty ────────────────────────────────
    public static readonly DependencyProperty DefaultImagePathProperty =
        DependencyProperty.Register(
            nameof(DefaultImagePath),
            typeof(string),
            typeof(ColorPickerButton),
            new PropertyMetadata(null));

    public string? DefaultImagePath
    {
        get => (string?)GetValue(DefaultImagePathProperty);
        set => SetValue(DefaultImagePathProperty, value);
    }

    public ColorPickerButton()
    {
        InitializeComponent();
    }

    private bool _updatingColor = false;

    // ColorPicker → Color property
    private void OnColorPickerColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_updatingColor) return;
        _updatingColor = true;
        Color = args.NewColor;
        _updatingColor = false;
    }

    // ImageColorPickerControl wired up when its Case becomes active
    private void OnImagePickerLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ImageColorPickerControl picker) return;

        picker.RegisterPropertyChangedCallback(
            ImageColorPickerControl.ColorProperty, OnImagePickerColorChanged);

        picker.PickingImage += (_, _) => PickerFlyout.Hide();
    }

    // ImageColorPickerControl → Color property
    private void OnImagePickerColorChanged(DependencyObject d, DependencyProperty dp)
    {
        if (_updatingColor) return;
        if (d is not ImageColorPickerControl picker) return;
        _updatingColor = true;
        Color = picker.Color;
        _updatingColor = false;
    }
}
