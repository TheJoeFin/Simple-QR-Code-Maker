using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;

class BoolToVisibilityInvertedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true)
            return Visibility.Collapsed;

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility.Collapsed)
            return true;

        return false;
    }
}
