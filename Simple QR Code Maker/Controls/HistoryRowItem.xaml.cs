using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;

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
}
