using SkiaSharp;

namespace Simple_QR_Code_Maker.Models;

public sealed class LogoImageResult
{
    public required SKBitmap LogoImage { get; init; }

    public string? SvgContent { get; init; }

    public string? LogoPath { get; init; }
}
