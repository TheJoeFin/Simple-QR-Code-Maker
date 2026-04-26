using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class IconAndTextToggleButton : UserControl
{
    public IconAndTextToggleButton()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(IconAndTextToggleButton), new PropertyMetadata(string.Empty));

    public UIElement? Icon
    {
        get => (UIElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(UIElement), typeof(IconAndTextToggleButton), new PropertyMetadata(null));

    public bool? IsChecked
    {
        get => (bool?)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(nameof(IsChecked), typeof(bool?), typeof(IconAndTextToggleButton), new PropertyMetadata(false));

    public bool IsThreeState
    {
        get => (bool)GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    public static readonly DependencyProperty IsThreeStateProperty =
        DependencyProperty.Register(nameof(IsThreeState), typeof(bool), typeof(IconAndTextToggleButton), new PropertyMetadata(false));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(IconAndTextToggleButton), new PropertyMetadata(null));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(IconAndTextToggleButton), new PropertyMetadata(null));

    public event RoutedEventHandler? Click;

    public event RoutedEventHandler? Checked;

    public event RoutedEventHandler? Unchecked;

    public event RoutedEventHandler? Indeterminate;

    private void ToggleButton_Click(object sender, RoutedEventArgs e) => Click?.Invoke(this, e);

    private void ToggleButton_Checked(object sender, RoutedEventArgs e) => Checked?.Invoke(this, e);

    private void ToggleButton_Unchecked(object sender, RoutedEventArgs e) => Unchecked?.Invoke(this, e);

    private void ToggleButton_Indeterminate(object sender, RoutedEventArgs e) => Indeterminate?.Invoke(this, e);
}
