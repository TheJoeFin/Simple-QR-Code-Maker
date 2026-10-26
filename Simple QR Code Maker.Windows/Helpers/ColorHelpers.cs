using Windows.UI;

namespace Simple_QR_Code_Maker.Helpers;

public class ColorHelpers
{
    public static double GetContrastRatio(Color foreground, Color background)
    {
        double luminance1 = GetLuminance(foreground);
        double luminance2 = GetLuminance(background);
        return (Math.Max(luminance1, luminance2) + 0.05) / (Math.Min(luminance1, luminance2) + 0.05);
    }
    private static double GetLuminance(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        r = (r <= 0.03928) ? (r / 12.92) : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = (g <= 0.03928) ? (g / 12.92) : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = (b <= 0.03928) ? (b / 12.92) : Math.Pow((b + 0.055) / 1.055, 2.4);

        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }
}
