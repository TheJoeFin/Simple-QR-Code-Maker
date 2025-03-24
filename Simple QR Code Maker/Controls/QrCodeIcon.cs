using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Simple_QR_Code_Maker.Controls;

public partial class QrCodeIcon : FontIcon
{
    public QrCodeIcon()
    {
        this.FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"];
        this.Glyph = "\uED14";
    }
}

