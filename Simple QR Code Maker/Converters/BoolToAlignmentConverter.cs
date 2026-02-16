using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;

public class BoolToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool hasImage = value is bool b && b;

        if (targetType == typeof(HorizontalAlignment))
            return hasImage ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;

        return hasImage ? VerticalAlignment.Stretch : VerticalAlignment.Center;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
