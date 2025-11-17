using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class IconAndTextContent : StackPanel
{
    public IconAndTextContent()
    {
        InitializeComponent();
    }

    public Symbol Icon
    { get => (Symbol)GetValue(IconProperty); set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(Symbol), typeof(IconAndTextContent), new PropertyMetadata(Symbol.Placeholder));

    public string ContentText
    { get => (string)GetValue(ContentTextProperty); set => SetValue(ContentTextProperty, value);
    }

    public static readonly DependencyProperty ContentTextProperty =
        DependencyProperty.Register(nameof(ContentText), typeof(string), typeof(IconAndTextContent), new PropertyMetadata(string.Empty));
}
