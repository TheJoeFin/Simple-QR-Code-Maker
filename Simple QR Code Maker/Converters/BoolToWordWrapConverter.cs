using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;
internal class BoolToWordWrapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true)
            return TextWrapping.Wrap;

        return TextWrapping.NoWrap;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is TextWrapping wrapping && wrapping == TextWrapping.Wrap)
            return true;

        return false;
    }
}
