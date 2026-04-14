using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;
using WinColor = Windows.UI.Color;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class ColorPickerButton : UserControl
{
    private static readonly Regex ExactHexColorRegex = ExactHexColor();

    private static readonly Regex EmbeddedHexColorRegex = EmbeddedHexColor();

    private static readonly Regex RgbColorRegex = RgbColor();

    // ── Color DependencyProperty ────────────────────────────────────────────
    public static readonly DependencyProperty ColorProperty =
        DependencyProperty.Register(
            nameof(Color),
            typeof(WinColor),
            typeof(ColorPickerButton),
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
            typeof(ColorPickerButton),
            new PropertyMetadata(null, OnDefaultImagePathChanged));

    public string? DefaultImagePath
    {
        get => (string?)GetValue(DefaultImagePathProperty);
        set => SetValue(DefaultImagePathProperty, value);
    }

    public ColorPickerButton()
    {
        InitializeComponent();
        Loaded += ColorPickerButton_Loaded;
        Unloaded += ColorPickerButton_Unloaded;
        UpdateModeVisibility();
    }

    private bool _updatingColor = false;
    private int _clipboardRefreshVersion;
    private bool _isClipboardSubscriptionActive;
    private WinColor? _clipboardSuggestionColor;
    private ImageColorPickerControl? _imageModePanel;

    private static void OnDefaultImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerButton control)
            control.ApplyImagePickerDefaults();
    }

    // ColorPicker → Color property
    private void OnColorPickerColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        SetSelectedColor(args.NewColor);
    }

    // ImageColorPickerControl → Color property
    private void OnImagePickerColorChanged(DependencyObject d, DependencyProperty dp)
    {
        if (d is not ImageColorPickerControl picker) return;
        SetSelectedColor(picker.Color);
    }

    private void ModePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateModeVisibility();
    }

    private void ColorPickerButton_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateModeVisibility();
        EnsureClipboardSubscription();
        _ = RefreshClipboardSuggestionAsync();
    }

    private void ColorPickerButton_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_isClipboardSubscriptionActive)
            return;

        Clipboard.ContentChanged -= Clipboard_ContentChanged;
        _isClipboardSubscriptionActive = false;
    }

    private void EnsureClipboardSubscription()
    {
        if (_isClipboardSubscriptionActive)
            return;

        Clipboard.ContentChanged += Clipboard_ContentChanged;
        _isClipboardSubscriptionActive = true;
    }

    private void Clipboard_ContentChanged(object? sender, object e)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            _ = RefreshClipboardSuggestionAsync();
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() => _ = RefreshClipboardSuggestionAsync());
    }

    private void PasteClipboardColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_clipboardSuggestionColor is WinColor clipboardColor)
            SetSelectedColor(clipboardColor);
    }

    private async Task RefreshClipboardSuggestionAsync()
    {
        int refreshVersion = ++_clipboardRefreshVersion;
        ClearClipboardSuggestion();

        DataPackageView clipboardContent = Clipboard.GetContent();
        if (!clipboardContent.Contains(StandardDataFormats.Text))
            return;

        string clipboardText = await clipboardContent.GetTextAsync();
        if (refreshVersion != _clipboardRefreshVersion)
            return;

        if (!TryParseClipboardColor(clipboardText, out WinColor parsedColor, out string displayText))
            return;

        _clipboardSuggestionColor = parsedColor;
        PasteClipboardColorSwatch.Background = new SolidColorBrush(parsedColor);
        ClipboardSuggestionContainer.Opacity = 1;
        ClipboardSuggestionContainer.IsHitTestVisible = true;
        PasteClipboardColorButton.IsEnabled = true;
        PasteClipboardColorButton.IsTabStop = true;

        string accessibleLabel = $"Paste {displayText} from clipboard";
        ToolTipService.SetToolTip(PasteClipboardColorButton, accessibleLabel);
        AutomationProperties.SetName(PasteClipboardColorButton, accessibleLabel);
    }

    private void ClearClipboardSuggestion()
    {
        _clipboardSuggestionColor = null;
        PasteClipboardColorSwatch.Background = null;
        ClipboardSuggestionContainer.Opacity = 0;
        ClipboardSuggestionContainer.IsHitTestVisible = false;
        PasteClipboardColorButton.IsEnabled = false;
        PasteClipboardColorButton.IsTabStop = false;
        ToolTipService.SetToolTip(PasteClipboardColorButton, null);
        AutomationProperties.SetName(PasteClipboardColorButton, "Paste color from clipboard");
    }

    private void SetSelectedColor(WinColor color)
    {
        if (_updatingColor) return;
        _updatingColor = true;
        Color = color;
        if (_imageModePanel is not null && _imageModePanel.Color != color)
            _imageModePanel.Color = color;
        _updatingColor = false;
    }

    private void UpdateModeVisibility()
    {
        if (!IsLoaded) return;

        bool showImagePicker = ModePicker.SelectedIndex == 1;
        if (showImagePicker)
        {
            _ = EnsureImageModePanel();
            ApplyImagePickerDefaults();
        }

        ColorModePanel.Visibility = showImagePicker ? Visibility.Collapsed : Visibility.Visible;
        ImageModeHost.Visibility = showImagePicker ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyImagePickerDefaults()
    {
        if (!IsLoaded || ModePicker.SelectedIndex != 1 || _imageModePanel is null)
            return;

        if (!string.Equals(_imageModePanel.DefaultImagePath, DefaultImagePath, StringComparison.Ordinal))
            _imageModePanel.DefaultImagePath = DefaultImagePath;
    }

    private ImageColorPickerControl EnsureImageModePanel()
    {
        if (_imageModePanel is not null)
            return _imageModePanel;

        ImageColorPickerControl picker = new()
        {
            Color = Color,
        };

        picker.RegisterPropertyChangedCallback(
            ImageColorPickerControl.ColorProperty, OnImagePickerColorChanged);
        picker.PickingImage += (_, _) => PickerFlyout.Hide();

        _imageModePanel = picker;
        ImageModeHost.Content = picker;
        return picker;
    }

    private static bool TryParseClipboardColor(string? clipboardText, out WinColor color, out string displayText)
    {
        color = default;
        displayText = string.Empty;

        if (string.IsNullOrWhiteSpace(clipboardText))
            return false;

        string trimmedText = clipboardText.Trim();

        Match exactHexMatch = ExactHexColorRegex.Match(trimmedText);
        if (TryCreateColorFromHexMatch(exactHexMatch, out color))
        {
            displayText = FormatColor(color);
            return true;
        }

        Match exactRgbMatch = RgbColorRegex.Match(trimmedText);
        if (IsFullMatch(exactRgbMatch, trimmedText) && TryCreateColorFromRgbMatch(exactRgbMatch, out color, out displayText))
            return true;

        Match embeddedRgbMatch = RgbColorRegex.Match(clipboardText);
        if (TryCreateColorFromRgbMatch(embeddedRgbMatch, out color, out displayText))
            return true;

        Match embeddedHexMatch = EmbeddedHexColorRegex.Match(clipboardText);
        if (TryCreateColorFromHexMatch(embeddedHexMatch, out color))
        {
            displayText = FormatColor(color);
            return true;
        }

        return false;
    }

    private static bool IsFullMatch(Match match, string input)
    {
        return match.Success && match.Index == 0 && match.Length == input.Length;
    }

    private static bool TryCreateColorFromRgbMatch(Match match, out WinColor color, out string displayText)
    {
        color = default;
        displayText = string.Empty;

        if (!match.Success)
            return false;

        if (!byte.TryParse(match.Groups["r"].Value, out byte r)
            || !byte.TryParse(match.Groups["g"].Value, out byte g)
            || !byte.TryParse(match.Groups["b"].Value, out byte b))
        {
            return false;
        }

        color = WinColor.FromArgb(255, r, g, b);
        displayText = $"rgb({r}, {g}, {b})";
        return true;
    }

    private static bool TryCreateColorFromHexMatch(Match match, out WinColor color)
    {
        color = default;
        if (!match.Success)
            return false;

        string hexValue = match.Groups["hex"].Value;
        switch (hexValue.Length)
        {
            case 3:
                color = WinColor.FromArgb(
                    255,
                    ExpandHexDigit(hexValue[0]),
                    ExpandHexDigit(hexValue[1]),
                    ExpandHexDigit(hexValue[2]));
                return true;
            case 4:
                color = WinColor.FromArgb(
                    ExpandHexDigit(hexValue[0]),
                    ExpandHexDigit(hexValue[1]),
                    ExpandHexDigit(hexValue[2]),
                    ExpandHexDigit(hexValue[3]));
                return true;
            case 6:
                color = WinColor.FromArgb(
                    255,
                    ParseHexByte(hexValue[0], hexValue[1]),
                    ParseHexByte(hexValue[2], hexValue[3]),
                    ParseHexByte(hexValue[4], hexValue[5]));
                return true;
            case 8:
                color = WinColor.FromArgb(
                    ParseHexByte(hexValue[0], hexValue[1]),
                    ParseHexByte(hexValue[2], hexValue[3]),
                    ParseHexByte(hexValue[4], hexValue[5]),
                    ParseHexByte(hexValue[6], hexValue[7]));
                return true;
            default:
                return false;
        }
    }

    private static byte ExpandHexDigit(char hexDigit)
    {
        return ParseHexByte(hexDigit, hexDigit);
    }

    private static byte ParseHexByte(char high, char low)
    {
        return Convert.ToByte($"{high}{low}", 16);
    }

    private static string FormatColor(WinColor color)
    {
        return color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    [GeneratedRegex(@"rgb\s*\(\s*(?<r>\d{1,3})\s*,\s*(?<g>\d{1,3})\s*,\s*(?<b>\d{1,3})\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RgbColor();

    [GeneratedRegex(@"(?:#|0x)(?<hex>[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{4}|[0-9a-fA-F]{3})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddedHexColor();

    [GeneratedRegex(@"^(?:#|0x)?(?<hex>[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{4}|[0-9a-fA-F]{3})$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ExactHexColor();
}
