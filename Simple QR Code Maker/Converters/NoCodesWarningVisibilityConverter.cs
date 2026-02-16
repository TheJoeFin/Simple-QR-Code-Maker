using Microsoft.UI.Xaml.Data;

namespace Simple_QR_Code_Maker.Converters;

public class NoCodesWarningVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // value is CodeBorders.Count
        int count = value is int c ? c : -1;

        // Show warning if count is 0 (no codes found)
        return count == 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
