using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;
internal class MultiLineCodeModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string enumString)
            return MultiLineCodeMode.OneLineOneCode;

        bool couldParseValue = Enum.TryParse(enumString, out MultiLineCodeMode mode);
        bool couldParseParameter = Enum.TryParse(parameter as string, out MultiLineCodeMode parameterMode);

        if (!couldParseValue || !couldParseParameter)
            return false;

        return mode == parameterMode;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
