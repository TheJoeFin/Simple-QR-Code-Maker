using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;

internal class IsZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is 0)
            return true;

        return false;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
