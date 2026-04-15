using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Models;

public sealed record RequestedQrCodeItem(string CodeAsText)
{
    public string SafeFileNameBase => CodeAsText.ToSafeFileName();
}
