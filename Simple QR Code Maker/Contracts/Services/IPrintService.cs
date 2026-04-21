using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Contracts.Services;

public interface IPrintService
{
    Task PrintQrCodesAsync(
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot renderSettings,
        PrintJobSettings printSettings);
}
