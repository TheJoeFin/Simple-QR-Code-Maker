using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Windows.Input;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class IconAndTextButton : UserControl
{
    public IconAndTextButton()
    {
        InitializeComponent();
        UpdatePresentation();
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(IconAndTextButton), new PropertyMetadata(null, OnPresentationPropertyChanged));

    public UIElement? Icon
    {
        get => (UIElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(UIElement), typeof(IconAndTextButton), new PropertyMetadata(null, OnPresentationPropertyChanged));

    public IconSource? IconSource
    {
        get => (IconSource?)GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public static readonly DependencyProperty IconSourceProperty =
        DependencyProperty.Register(nameof(IconSource), typeof(IconSource), typeof(IconAndTextButton), new PropertyMetadata(null, OnPresentationPropertyChanged));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(IconAndTextButton), new PropertyMetadata(null, OnPresentationPropertyChanged));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(IconAndTextButton), new PropertyMetadata(null));

    public event RoutedEventHandler? Click;

    public string DisplayText => Text ?? UICommand?.Label ?? string.Empty;

    private XamlUICommand? UICommand => Command as XamlUICommand;

    private IconSource? DisplayIconSource => Icon is null ? IconSource ?? UICommand?.IconSource : null;

    private void UpdatePresentation()
    {
        Bindings.Update();
        ExplicitIconPresenter.Content = Icon;
        ExplicitIconPresenter.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible;
        IconSourcePresenter.IconSource = DisplayIconSource;
        IconSourcePresenter.Visibility = DisplayIconSource is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void OnPresentationPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
    {
        ((IconAndTextButton)dependencyObject).UpdatePresentation();
    }

    private void Button_Click(object sender, RoutedEventArgs e) => Click?.Invoke(this, e);
}
