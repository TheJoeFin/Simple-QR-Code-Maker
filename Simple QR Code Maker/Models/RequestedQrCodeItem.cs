using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Models;

public sealed record RequestedQrCodeItem(
    string CodeAsText,
    QrContentKind ContentKind = QrContentKind.PlainText,
    MultiLineCodeMode? MultiLineCodeModeOverride = null)
{
    public string SafeFileNameBase => CodeAsText.ToSafeFileName();
}
