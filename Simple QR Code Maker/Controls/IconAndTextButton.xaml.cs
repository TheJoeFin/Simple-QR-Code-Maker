using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class IconAndTextButton : UserControl
{
    public IconAndTextButton()
    {
        this.InitializeComponent();
    }

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(IconAndTextButton), new PropertyMetadata(string.Empty, OnBindablePropertyChanged));

    public UIElement Icon
    {
        get { return (UIElement)GetValue(IconProperty); }
        set { SetValue(IconProperty, value); }
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(UIElement), typeof(IconAndTextButton), new PropertyMetadata(null, OnBindablePropertyChanged));

    public bool IsCheckable
    {
        get { return (bool)GetValue(IsCheckableProperty); }
        set { SetValue(IsCheckableProperty, value); }
    }

    public static readonly DependencyProperty IsCheckableProperty =
        DependencyProperty.Register(nameof(IsCheckable), typeof(bool), typeof(IconAndTextButton), new PropertyMetadata(false, OnBindablePropertyChanged));

    public bool? IsChecked
    {
        get { return (bool?)GetValue(IsCheckedProperty); }
        set { SetValue(IsCheckedProperty, value); }
    }

    public static readonly DependencyProperty IsCheckedProperty =
        DependencyProperty.Register(nameof(IsChecked), typeof(bool?), typeof(IconAndTextButton), new PropertyMetadata(false, OnBindablePropertyChanged));

    public bool IsThreeState
    {
        get { return (bool)GetValue(IsThreeStateProperty); }
        set { SetValue(IsThreeStateProperty, value); }
    }

    public static readonly DependencyProperty IsThreeStateProperty =
        DependencyProperty.Register(nameof(IsThreeState), typeof(bool), typeof(IconAndTextButton), new PropertyMetadata(false, OnBindablePropertyChanged));

    public ICommand? Command
    {
        get { return (ICommand?)GetValue(CommandProperty); }
        set { SetValue(CommandProperty, value); }
    }

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(IconAndTextButton), new PropertyMetadata(null, OnBindablePropertyChanged));

    public object? CommandParameter
    {
        get { return GetValue(CommandParameterProperty); }
        set { SetValue(CommandParameterProperty, value); }
    }

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(IconAndTextButton), new PropertyMetadata(null, OnBindablePropertyChanged));

    public bool IsNotCheckable => !this.IsCheckable;

    public event RoutedEventHandler? Click;

    public event RoutedEventHandler? Checked;

    public event RoutedEventHandler? Unchecked;

    public event RoutedEventHandler? Indeterminate;

    private static void OnBindablePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _)
    {
        IconAndTextButton control = (IconAndTextButton)dependencyObject;
        control.Bindings.Update();
    }

    private void StandardButton_Click(object sender, RoutedEventArgs e) => this.Click?.Invoke(this, e);

    private void CheckableButton_Click(object sender, RoutedEventArgs e) => this.Click?.Invoke(this, e);

    private void CheckableButton_Checked(object sender, RoutedEventArgs e) => this.Checked?.Invoke(this, e);

    private void CheckableButton_Unchecked(object sender, RoutedEventArgs e) => this.Unchecked?.Invoke(this, e);

    private void CheckableButton_Indeterminate(object sender, RoutedEventArgs e) => this.Indeterminate?.Invoke(this, e);

}
