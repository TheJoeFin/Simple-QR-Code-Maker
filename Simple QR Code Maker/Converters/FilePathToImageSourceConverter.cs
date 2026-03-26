using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Simple_QR_Code_Maker.Converters;

public class FilePathToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        try
        {
            Uri uri = new(path);
            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                return new SvgImageSource(uri);

            BitmapImage bitmap = new();
            bitmap.UriSource = uri;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
