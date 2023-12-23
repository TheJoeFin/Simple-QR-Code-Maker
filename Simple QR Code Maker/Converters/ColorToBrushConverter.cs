using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Simple_QR_Code_Maker.Converters;
public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Color color)
            return new SolidColorBrush(color);

        return new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
