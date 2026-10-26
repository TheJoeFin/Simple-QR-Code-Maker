using Simple_QR_Code_Maker.Models;
using System.IO.Compression;
using System.Text;

namespace Simple_QR_Code_Maker.Helpers;

internal static class QrExportWriter
{
    internal static Task<byte[]> RenderPngAsync(RequestedQrCodeItem code, QrRenderSettingsSnapshot settings)
        => Task.Run(() =>
        {
            using QrRenderSettingsSnapshot? commonSettings = settings.CreateCommonBrandOverride(code);
            QrRenderSettingsSnapshot effectiveSettings = commonSettings ?? settings;
            using MemoryStream output = new();
            QrCodeRenderer.SaveQrCodePngToStream(
                output, code.CodeAsText, effectiveSettings.ErrorCorrectionLevel,
                effectiveSettings.ForegroundColor, effectiveSettings.BackgroundColor,
                effectiveSettings.LogoImage, effectiveSettings.LogoSizePercentage,
                effectiveSettings.LogoPaddingPixels, effectiveSettings.QrPaddingModules,
                effectiveSettings.FramePreset, ResolveFrameText(code, effectiveSettings),
                effectiveSettings.Appearance);
            return output.ToArray();
        });

    internal static Task<string> RenderSvgAsync(RequestedQrCodeItem code, QrRenderSettingsSnapshot settings)
        => Task.Run(() =>
        {
            using QrRenderSettingsSnapshot? commonSettings = settings.CreateCommonBrandOverride(code);
            QrRenderSettingsSnapshot effectiveSettings = commonSettings ?? settings;
            return QrCodeRenderer.GetSvgQrCodeForText(
                code.CodeAsText, effectiveSettings.ErrorCorrectionLevel,
                effectiveSettings.ForegroundColor, effectiveSettings.BackgroundColor,
                effectiveSettings.LogoImage, effectiveSettings.LogoSizePercentage,
                effectiveSettings.LogoPaddingPixels, effectiveSettings.LogoSvgContent,
                effectiveSettings.QrPaddingModules, effectiveSettings.FramePreset,
                ResolveFrameText(code, effectiveSettings), effectiveSettings.Appearance).Content;
        });

    internal static async Task WriteAsync(
        RequestedQrCodeItem code, Stream output, FileKind kind, QrRenderSettingsSnapshot settings)
    {
        switch (kind)
        {
            case FileKind.None:
                return;
            case FileKind.PNG:
                await output.WriteAsync(await RenderPngAsync(code, settings));
                break;
            case FileKind.SVG:
                using (StreamWriter writer = new(output, new UTF8Encoding(false), 1024, leaveOpen: true))
                {
                    await writer.WriteAsync(await RenderSvgAsync(code, settings));
                    await writer.FlushAsync();
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported QR export format.");
        }
    }

    internal static async Task WriteZipAsync(
        Stream output,
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot settings,
        Action<int>? updateProgress,
        params FileKind[] kinds)
    {
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int index = 0; index < codes.Count; index++)
            {
                RequestedQrCodeItem code = codes[index];
                if (!string.IsNullOrWhiteSpace(code.SafeFileNameBase))
                {
                    foreach (FileKind kind in kinds)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(code.SafeFileNameBase + GetFileExtension(kind), CompressionLevel.Optimal);
                        using Stream entryStream = entry.Open();
                        await WriteAsync(code, entryStream, kind, settings);
                    }
                }
                updateProgress?.Invoke(index + 1);
            }
        }

        await output.FlushAsync();
    }

    internal static string GetFileExtension(FileKind kind) => $".{kind.ToString().ToLowerInvariant()}";

    private static string? ResolveFrameText(RequestedQrCodeItem code, QrRenderSettingsSnapshot settings)
        => QrFrameTextResolver.Resolve(
            settings.FramePreset, settings.FrameTextSource, settings.FrameText,
            code.CodeAsText, code.ContentKind, code.MultiLineCodeModeOverride);
}
