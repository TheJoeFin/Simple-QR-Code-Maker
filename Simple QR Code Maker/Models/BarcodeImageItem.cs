using Microsoft.UI.Xaml.Media.Imaging;

namespace Simple_QR_Code_Maker.Models;
public class BarcodeImageItem
{
    public string CodeAsText { get; set; } = string.Empty;
    public WriteableBitmap? CodeAsBitmap { get; set; }
}
