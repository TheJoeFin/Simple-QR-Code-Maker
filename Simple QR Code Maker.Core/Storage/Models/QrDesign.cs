#nullable enable

using Simple_QR_Code_Maker.Core.Models;

namespace Simple_QR_Code_Maker.Core.Storage.Models;

public sealed record QrDesign
{
    public string Content { get; init; } = string.Empty;

    public QrErrorCorrectionLevel ErrorCorrectionLevel { get; init; } = QrErrorCorrectionLevel.Medium;

    public int PixelSize { get; init; } = 1024;

    public int QuietZoneModules { get; init; } = 4;

    public QrColor Foreground { get; init; } = QrColor.Black;

    public QrColor Background { get; init; } = QrColor.White;

    public LogoAssetId? LogoAssetId { get; init; }
}
