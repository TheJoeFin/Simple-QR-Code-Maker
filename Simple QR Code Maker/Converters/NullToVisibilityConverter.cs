using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;

public partial class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isInverted = parameter is string param && param.Equals("Inverted", StringComparison.OrdinalIgnoreCase);
        bool isNull = value == null;

        if (isInverted)
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        else
            return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
