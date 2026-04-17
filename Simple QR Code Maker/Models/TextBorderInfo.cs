using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Foundation;

namespace Simple_QR_Code_Maker.Models;

public partial class TextBorderInfo : ObservableRecipient
{
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Rect BorderRect { get; set; } = Rect.Empty;

    public TextBorderInfo(ZXing.Result result)
    {
        Text = result.Text;

        if (result.ResultPoints.Length >= 3)
        {
            Point point1 = new(result.ResultPoints[0].X, result.ResultPoints[0].Y);
            Point point2 = new(result.ResultPoints[2].X, result.ResultPoints[2].Y);
            BorderRect = new(point1, point2);
        }
    }
}
