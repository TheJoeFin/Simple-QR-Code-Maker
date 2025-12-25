using ImageMagick;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Simple_QR_Code_Maker.Converters;

public class MagickColorToBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is MagickColor magickColor)
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(
                (byte)(magickColor.A / 257), // MagickColor uses 16-bit values (0-65535), convert to 8-bit (0-255)
                (byte)(magickColor.R / 257),
                (byte)(magickColor.G / 257),
                (byte)(magickColor.B / 257)
            ));
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
