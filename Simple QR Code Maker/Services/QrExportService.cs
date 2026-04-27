using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.IO.Compression;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Simple_QR_Code_Maker.Services;

public class QrExportService : IQrExportService
{
    private const int MaxBulkSaveConcurrency = 4;

    public async Task<IReadOnlyList<StorageFile>> CreateFilesAsync(
        StorageFolder folder,
        IReadOnlyList<RequestedQrCodeItem> requestedCodes,
        QrRenderSettingsSnapshot renderSettings,
        params FileKind[] fileKinds)
    {
        List<StorageFile> files = [];

        foreach (RequestedQrCodeItem requestedCode in requestedCodes)
        {
            if (string.IsNullOrWhiteSpace(requestedCode.SafeFileNameBase))
                continue;

            foreach (FileKind kindOfFile in fileKinds)
            {
                StorageFile file = await folder.CreateFileAsync(
                    requestedCode.SafeFileNameBase + GetFileExtension(kindOfFile),
                    CreationCollisionOption.ReplaceExisting);
                await WriteRequestedCodeToFileAsync(requestedCode, file, kindOfFile, renderSettings);
                files.Add(file);
            }
        }

        return files;
    }

    public async Task<IReadOnlyList<string>> RenderSvgTextsAsync(
        IReadOnlyList<RequestedQrCodeItem> requestedCodes,
        QrRenderSettingsSnapshot renderSettings)
    {
        List<string> textStrings = [];

        foreach (RequestedQrCodeItem requestedCode in requestedCodes)
        {
            string svgText = await GetRequestedCodeAsSvgTextAsync(requestedCode, renderSettings);
            if (!string.IsNullOrWhiteSpace(svgText))
                textStrings.Add(svgText);
        }

        return textStrings;
    }

    public async Task<string?> SaveFilesAsync(
        IReadOnlyList<RequestedQrCodeItem> requestedCodes,
        QrRenderSettingsSnapshot renderSettings,
        string quickSaveLocation,
        string? overrideFolderPath,
        Action<int>? updateProgress,
        params FileKind[] fileKinds)
    {
        StorageFolder? folder = await PickSaveFolderAsync(overrideFolderPath, quickSaveLocation);
        if (folder is null)
            return null;

        int completedItemCount = 0;
        int maxConcurrency = renderSettings.LogoImage is null ? MaxBulkSaveConcurrency : 1;
        using SemaphoreSlim semaphore = new(maxConcurrency, maxConcurrency);
        List<Task> tasks = [];

        foreach (RequestedQrCodeItem requestedCode in requestedCodes)
        {
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(requestedCode.SafeFileNameBase))
                    {
                        foreach (FileKind kindOfFile in fileKinds)
                        {
                            StorageFile file = await folder.CreateFileAsync(
                                requestedCode.SafeFileNameBase + GetFileExtension(kindOfFile),
                                CreationCollisionOption.ReplaceExisting);
                            await WriteRequestedCodeToFileAsync(requestedCode, file, kindOfFile, renderSettings);
                        }
                    }
                }
                finally
                {
                    int completed = Interlocked.Increment(ref completedItemCount);
                    updateProgress?.Invoke(completed);
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        return folder.Path;
    }

    public async Task<bool> SaveFilesAsZipAsync(
        IReadOnlyList<RequestedQrCodeItem> requestedCodes,
        QrRenderSettingsSnapshot renderSettings,
        Action<int>? updateProgress,
        params FileKind[] fileKinds)
    {
        StorageFile? zipFile = await PickSaveZipFileAsync();
        if (zipFile is null)
            return false;

        using IRandomAccessStream outputStream = await zipFile.OpenAsync(FileAccessMode.ReadWrite);
        outputStream.Size = 0;
        using Stream output = outputStream.AsStreamForWrite();
        using ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true);

        for (int index = 0; index < requestedCodes.Count; index++)
        {
            RequestedQrCodeItem requestedCode = requestedCodes[index];

            if (!string.IsNullOrWhiteSpace(requestedCode.SafeFileNameBase))
            {
                foreach (FileKind kindOfFile in fileKinds)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(
                        requestedCode.SafeFileNameBase + GetFileExtension(kindOfFile),
                        CompressionLevel.Optimal);
                    using Stream entryStream = entry.Open();
                    await WriteRequestedCodeToZipEntryAsync(requestedCode, entryStream, kindOfFile, renderSettings);
                }
            }

            updateProgress?.Invoke(index + 1);
        }

        await output.FlushAsync();
        return true;
    }

    private static async Task<byte[]> RenderRequestedCodePngBytesAsync(RequestedQrCodeItem requestedCode, QrRenderSettingsSnapshot renderSettings)
    {
        return await Task.Run(() =>
        {
            string? resolvedFrameText = ResolveFrameText(requestedCode, renderSettings);
            using MemoryStream ms = new();
            BarcodeHelpers.SaveQrCodePngToStream(
                ms,
                requestedCode.CodeAsText,
                renderSettings.ErrorCorrectionLevel,
                renderSettings.ForegroundColor,
                renderSettings.BackgroundColor,
                renderSettings.LogoImage,
                renderSettings.LogoSizePercentage,
                renderSettings.LogoPaddingPixels,
                renderSettings.QrPaddingModules,
                renderSettings.FramePreset,
                resolvedFrameText);
            return ms.ToArray();
        });
    }

    private static async Task<string> GetRequestedCodeAsSvgTextAsync(RequestedQrCodeItem requestedCode, QrRenderSettingsSnapshot renderSettings)
    {
        string? resolvedFrameText = ResolveFrameText(requestedCode, renderSettings);
        return await Task.Run(() =>
            BarcodeHelpers.GetSvgQrCodeForText(
                requestedCode.CodeAsText,
                renderSettings.ErrorCorrectionLevel,
                renderSettings.ForegroundColor,
                renderSettings.BackgroundColor,
                renderSettings.LogoImage,
                renderSettings.LogoSizePercentage,
                renderSettings.LogoPaddingPixels,
                renderSettings.LogoSvgContent,
                renderSettings.QrPaddingModules,
                renderSettings.FramePreset,
                resolvedFrameText).Content);
    }

    private static string? ResolveFrameText(RequestedQrCodeItem requestedCode, QrRenderSettingsSnapshot renderSettings)
    {
        return QrFrameTextResolver.Resolve(
            renderSettings.FramePreset,
            renderSettings.FrameTextSource,
            renderSettings.FrameText,
            requestedCode.CodeAsText,
            requestedCode.ContentKind,
            requestedCode.MultiLineCodeModeOverride);
    }

    private static async Task WriteRequestedCodeToFileAsync(RequestedQrCodeItem requestedCode, StorageFile file, FileKind kindOfFile, QrRenderSettingsSnapshot renderSettings)
    {
        switch (kindOfFile)
        {
            case FileKind.None:
                return;
            case FileKind.PNG:
                {
                    byte[] pngBytes = await RenderRequestedCodePngBytesAsync(requestedCode, renderSettings);
                    using IRandomAccessStream outputStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                    outputStream.Size = 0;
                    using Stream output = outputStream.AsStreamForWrite();
                    await output.WriteAsync(pngBytes);
                    await output.FlushAsync();
                    break;
                }
            case FileKind.SVG:
                {
                    string svgText = await GetRequestedCodeAsSvgTextAsync(requestedCode, renderSettings);
                    using IRandomAccessStream outputStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                    outputStream.Size = 0;
                    using Stream output = outputStream.AsStreamForWrite();
                    using StreamWriter writer = new(output, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true);
                    await writer.WriteAsync(svgText);
                    await writer.FlushAsync();
                    break;
                }
            default:
                return;
        }
    }

    private static async Task WriteRequestedCodeToZipEntryAsync(RequestedQrCodeItem requestedCode, Stream entryStream, FileKind kindOfFile, QrRenderSettingsSnapshot renderSettings)
    {
        switch (kindOfFile)
        {
            case FileKind.None:
                return;
            case FileKind.PNG:
                {
                    byte[] pngBytes = await RenderRequestedCodePngBytesAsync(requestedCode, renderSettings);
                    await entryStream.WriteAsync(pngBytes);
                    await entryStream.FlushAsync();
                    break;
                }
            case FileKind.SVG:
                {
                    string svgText = await GetRequestedCodeAsSvgTextAsync(requestedCode, renderSettings);
                    using StreamWriter writer = new(entryStream, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true);
                    await writer.WriteAsync(svgText);
                    await writer.FlushAsync();
                    break;
                }
            default:
                return;
        }
    }

    private static async Task<StorageFolder?> PickSaveFolderAsync(string? overrideFolderPath, string quickSaveLocation)
    {
        if (overrideFolderPath is not null)
            return await StorageFolder.GetFolderFromPathAsync(overrideFolderPath);

        if (!string.IsNullOrWhiteSpace(quickSaveLocation) && Directory.Exists(quickSaveLocation))
            return await StorageFolder.GetFolderFromPathAsync(quickSaveLocation);

        FolderPicker folderPicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        InitializeWithWindow.Initialize(folderPicker, WindowNative.GetWindowHandle(App.MainWindow));
        return await folderPicker.PickSingleFolderAsync();
    }

    private static async Task<StorageFile?> PickSaveZipFileAsync()
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = $"QR Codes {DateTime.Now:yyyy-MM-dd}",
        };
        savePicker.FileTypeChoices.Add("ZIP Archive", [".zip"]);

        InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(App.MainWindow));
        return await savePicker.PickSaveFileAsync();
    }

    private static string GetFileExtension(FileKind kindOfFile)
    {
        return $".{kindOfFile.ToString().ToLowerInvariant()}";
    }
}
