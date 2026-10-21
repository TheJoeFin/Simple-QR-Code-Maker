namespace Simple_QR_Code_Maker.Models;

public sealed record CommonBrandPreset(string Name, string IconName, int ForegroundRgb, params string[] UrlPatterns)
{
    public System.Drawing.Color Foreground => System.Drawing.Color.FromArgb(
        255,
        (ForegroundRgb >> 16) & 255,
        (ForegroundRgb >> 8) & 255,
        ForegroundRgb & 255);

    public string LoadLogoSvg()
    {
        using Stream stream = typeof(CommonBrandPreset).Assembly.GetManifestResourceStream($"CommonBrands.{IconName}.svg")
            ?? throw new FileNotFoundException($"The bundled {Name} logo could not be found.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}