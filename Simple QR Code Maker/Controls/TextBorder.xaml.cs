using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class TextBorder : UserControl
{
    public event RoutedEventHandler? TextBorder_Click;

    public TextBorderInfo BorderInfo { get; set; }

    public TextBorder(ZXing.Result result)
    {
        InitializeComponent();

        BorderInfo = new(result);
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        TextBorder_Click?.Invoke(this, e);
    }
}
