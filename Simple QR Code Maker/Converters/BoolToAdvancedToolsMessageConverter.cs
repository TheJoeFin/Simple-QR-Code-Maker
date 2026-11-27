using Microsoft.UI.Xaml.Data;

using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Converters;

public partial class BoolToAdvancedToolsMessageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isAdvancedToolsVisible = value is bool visible && visible;

        if (isAdvancedToolsVisible)
        {
            // Advanced tools are open - show the original message
            return "Read_NoCodesContact".GetLocalized();
        }
        else
        {
            // Advanced tools are not open - suggest using them
            return "Read_NoCodesAdvancedTools".GetLocalized();
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
