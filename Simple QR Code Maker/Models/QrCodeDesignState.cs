using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Models;

public sealed record QrCodeDesignState
{
    public required string CodesContent { get; init; }

    public required Windows.UI.Color Foreground { get; init; }

    public required Windows.UI.Color Background { get; init; }

    public required ErrorCorrectionLevel ErrorCorrection { get; init; }

    public string? LogoImagePath { get; init; }

    public string? LogoEmoji { get; init; }

    public EmojiLogoStyle? LogoEmojiStyle { get; init; }

    public double LogoSizePercentage { get; init; }

    public double LogoPaddingPixels { get; init; }
}
