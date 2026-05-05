using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;

public partial class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isVisible = value is bool boolValue && boolValue;

        if (parameter is "true" or "True")
            isVisible = !isVisible;

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
