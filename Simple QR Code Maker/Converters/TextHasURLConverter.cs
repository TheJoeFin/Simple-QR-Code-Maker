using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;
internal class TextHasURLConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string text && Uri.IsWellFormedUriString(text, UriKind.Absolute))
            return true;

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
