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

        if (result.ResultPoints.Length > 0)
        {
            double left = result.ResultPoints.Min(point => point.X);
            double top = result.ResultPoints.Min(point => point.Y);
            double right = result.ResultPoints.Max(point => point.X);
            double bottom = result.ResultPoints.Max(point => point.Y);

            BorderRect = new Rect(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
        }
    }
}
