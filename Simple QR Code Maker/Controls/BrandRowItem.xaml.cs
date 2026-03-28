using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class BrandRowItem : UserControl
{
    public BrandItem Data
    {
        get { return (BrandItem)GetValue(DataProperty); }
        set { SetValue(DataProperty, value); }
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(BrandItem), typeof(BrandRowItem), new PropertyMetadata(null));

    public BrandRowItem()
    {
        this.InitializeComponent();
    }
}
