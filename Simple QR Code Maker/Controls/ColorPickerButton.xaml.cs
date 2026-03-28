using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class ColorPickerButton : UserControl
{
    private bool _updatingColor = false;

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

    public ColorPickerButton()
    {
        InitializeComponent();
        InternalColorPicker.Color = Color;
        InternalColorPicker.ColorChanged += OnColorPickerColorChanged;
        RegisterPropertyChangedCallback(ColorProperty, OnColorPropertyChanged);
    }

    private void OnColorPickerColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_updatingColor) return;
        _updatingColor = true;
        Color = args.NewColor;
        _updatingColor = false;
    }

    private void OnColorPropertyChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (_updatingColor) return;
        _updatingColor = true;
        InternalColorPicker.Color = Color;
        _updatingColor = false;
    }
}
