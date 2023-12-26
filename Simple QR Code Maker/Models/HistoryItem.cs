using Windows.UI;
using ZXing;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Models;
public record HistoryItem
{
    public DateTime SavedDateTime { get; set; } = DateTime.Now;
    public string CodesContent { get; set; } = string.Empty;
    //public BarcodeFormat Format { get; set; } = BarcodeFormat.QR_CODE;
    //public Color Foreground { get; set; } = Color.FromArgb(255, 255, 255, 255);
    //public Color Background { get; set; } = Color.FromArgb(255, 0,0,0);
    //public ErrorCorrectionLevel ErrorCorrection { get; set; } = ErrorCorrectionLevel.M;


    public HistoryItem()
    {
        
    }
}
