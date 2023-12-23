namespace Simple_QR_Code_Maker.Extensions;

public static class ColorExtensions
{
    public static System.Drawing.Color ToSystemDrawingColor(this Windows.UI.Color color)
    {
        return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    public static Windows.UI.Color ToWindowsUiColor(this System.Drawing.Color color)
    {
        return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
