using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;

class EmptyCollectionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is "true")
        {
            if (value is not 0)
                return Visibility.Visible;

            return Visibility.Collapsed;
        }

        if (value is 0)
            return Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}