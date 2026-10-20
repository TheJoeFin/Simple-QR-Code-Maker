using Simple_QR_Code_Maker.Core.Models;

namespace Simple_QR_Code_Maker.Rendering;

public sealed record QrRenderRequest(
    string Content,
    QrErrorCorrectionLevel ErrorCorrectionLevel,
    int PixelSize,
    int QuietZoneModules,
    QrColor Foreground,
    QrColor Background)
{
    public static QrRenderRequest CreateDefault(string content) =>
        new(content, QrErrorCorrectionLevel.Medium, 1024, 4, QrColor.Black, QrColor.White);
}
