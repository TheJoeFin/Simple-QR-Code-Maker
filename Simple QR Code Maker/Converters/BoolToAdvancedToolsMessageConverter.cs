using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;

public class BoolToAdvancedToolsMessageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isAdvancedToolsVisible = value is bool visible && visible;

        if (isAdvancedToolsVisible)
        {
            // Advanced tools are open - show the original message
            return "Could be there are none present or content failed to read. If you believe this is an issue with the app, please email Joe@JoeFinApps.com";
        }
        else
        {
            // Advanced tools are not open - suggest using them
            return "No QR codes detected. Try using the Advanced Tools to isolate and improve detection with image adjustments like grayscale, contrast, and perspective correction.";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
