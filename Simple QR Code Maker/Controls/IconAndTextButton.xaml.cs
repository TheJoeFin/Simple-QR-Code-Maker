using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class IconAndTextButton : Button
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
        DependencyProperty.Register("Text", typeof(string), typeof(IconAndTextButton), new PropertyMetadata(""));

    public UIElement Icon
    {
        get { return (UIElement)GetValue(IconProperty); }
        set { SetValue(IconProperty, value); }
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register("Icon", typeof(UIElement), typeof(IconAndTextButton), new PropertyMetadata(null));

}
