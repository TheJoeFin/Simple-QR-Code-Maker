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

    public static SkiaSharp.SKColor ToSkColor(this Windows.UI.Color color)
    {
        return new SkiaSharp.SKColor(color.R, color.G, color.B, color.A);
    }

    public static SkiaSharp.SKColor ToSkColor(this System.Drawing.Color color)
    {
        return new SkiaSharp.SKColor(color.R, color.G, color.B, color.A);
    }
}
