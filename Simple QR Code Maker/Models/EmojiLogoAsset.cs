using System.Drawing;

namespace Simple_QR_Code_Maker.Models;

public enum EmojiLogoSvgKind
{
    None = 0,
    Outline = 1,
    FontReference = 2,
}

public sealed partial class EmojiLogoAsset : IDisposable
{
    public Bitmap PreviewBitmap { get; }

    public string? SvgContent { get; }

    public EmojiLogoSvgKind SvgKind { get; }

    public EmojiLogoAsset(Bitmap previewBitmap, string? svgContent, EmojiLogoSvgKind svgKind)
    {
        PreviewBitmap = previewBitmap;
        SvgContent = svgContent;
        SvgKind = svgKind;
    }

    public void Dispose()
    {
        PreviewBitmap.Dispose();
    }
}
