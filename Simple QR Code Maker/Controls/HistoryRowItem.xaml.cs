using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Models;
using Windows.UI;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class HistoryRowItem : UserControl
{

    public HistoryItem Data
    {
        get { return (HistoryItem)GetValue(DataProperty); }
        set { SetValue(DataProperty, value); }
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register("Data", typeof(HistoryItem), typeof(HistoryRowItem), new PropertyMetadata(null));

    public HistoryRowItem()
    {
        this.InitializeComponent();
    }

    private Brush DisplayForeground(Color foreground, CommonBrandPreset? brand)
        => new SolidColorBrush(brand?.Foreground.ToWindowsUiColor() ?? foreground);

    private Brush DisplayBackground(Color background, CommonBrandPreset? brand)
        => new SolidColorBrush(brand is null ? background : CommonBrandPreset.Background.ToWindowsUiColor());

    private string DisplayErrorCorrection(string errorCorrection, CommonBrandPreset? brand)
        => brand is null ? errorCorrection : CommonBrandPreset.ErrorCorrection.ToString();
}
