using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not bool b)
            return false;

        return !b;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
