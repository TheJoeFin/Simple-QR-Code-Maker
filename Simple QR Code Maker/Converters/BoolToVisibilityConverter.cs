using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;

class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true)
            return Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility.Visible)
            return true;

        return false;
    }
}
