using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Controls;
using System.Collections.ObjectModel;
using ZXing;

namespace Simple_QR_Code_Maker.Models;
public class DecodingImageItem
{
    public string ImagePath { get; set; } = string.Empty;

    public string FileName => Path.GetFileName(ImagePath);
    
    public BitmapImage? BitmapImage { get; set; }

    public List<(string, Result)> Strings { get; set; } = new();

    public ObservableCollection<TextBorder> CodeBorders { get; set; } = new();

}
