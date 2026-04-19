using Simple_QR_Code_Maker.Models;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Contracts.Services;

public interface IQrExportService
{
    Task<IReadOnlyList<StorageFile>> CreateFilesAsync(
        StorageFolder folder,
        IReadOnlyList<RequestedQrCodeItem> requestedCodes,
        QrRenderSettingsSnapshot renderSettings,
        params FileKind[] fileKinds);

    Task<IReadOnlyList<string>> RenderSvgTextsAsync(
        IReadOnlyList<RequestedQrCodeItem> requestedCodes,
        QrRenderSettingsSnapshot renderSettings);

    Task<string?> SaveFilesAsync(
        IReadOnlyList<RequestedQrCodeItem> requestedCodes,
        QrRenderSettingsSnapshot renderSettings,
        string quickSaveLocation,
        string? overrideFolderPath,
        Action<int>? updateProgress,
        params FileKind[] fileKinds);

    Task<bool> SaveFilesAsZipAsync(
        IReadOnlyList<RequestedQrCodeItem> requestedCodes,
        QrRenderSettingsSnapshot renderSettings,
        Action<int>? updateProgress,
        params FileKind[] fileKinds);
}
