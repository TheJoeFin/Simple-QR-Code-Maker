using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;
public class EnumIsEqualConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language
        )
    {
        if (parameter is not string parameterString)
            return DependencyProperty.UnsetValue;

        if (Enum.IsDefined(value.GetType(), value) == false)
            return DependencyProperty.UnsetValue;

        object parameterValue = Enum.Parse(value.GetType(), parameterString);

        return parameterValue.Equals(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language
        )
    {
        if (parameter is not string parameterString)
            return DependencyProperty.UnsetValue;

        return Enum.Parse(targetType, parameterString);
    }
}
