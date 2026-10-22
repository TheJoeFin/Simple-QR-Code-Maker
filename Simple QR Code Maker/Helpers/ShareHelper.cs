using Simple_QR_Code_Maker.Models;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT;
using WinRT.Interop;

namespace Simple_QR_Code_Maker.Helpers;

internal static class ShareHelper
{
    [ComImport]
    [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);
        void ShowShareUIForWindow(IntPtr appWindow);
    }

    private static readonly Guid DataTransferManagerIid =
        new(0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);

    private static DataTransferManager? pendingManager;
    private static TypedEventHandler<DataTransferManager, DataRequestedEventArgs>? pendingHandler;

    public static bool CanShareQrCode(BarcodeImageItem code)
    {
        return !string.IsNullOrWhiteSpace(code.CodeAsText)
            && code.CodeAsBitmap is { PixelWidth: > 0, PixelHeight: > 0 } bitmap
            && bitmap.PixelBuffer.Length >= (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
    }

    public static async Task ShareQrCodesAsync(IReadOnlyList<BarcodeImageItem> codes)
    {
        if (codes.Count == 0 || codes.Any(code => !CanShareQrCode(code)))
            throw new InvalidOperationException("No shareable QR Codes. Each code must have content and a rendered image.");

        StorageFolder folder = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(
            "SharedQrCodes", CreationCollisionOption.OpenIfExists);
        List<StorageFile> files = [];

        foreach (BarcodeImageItem code in codes)
        {
            StorageFile file = await folder.CreateFileAsync(
                $"{code.CodeAsText.ToSafeFileName()}.png", CreationCollisionOption.GenerateUniqueName);
            if (!await code.SaveCodeAsPngFile(file))
                throw new IOException("Could not prepare PNG for sharing.");

            files.Add(file);
        }

        ShowShareUI(data =>
        {
            data.Properties.Title = codes.Count == 1 ? "QR Code" : "QR Codes";
            data.Properties.Description = string.Join(Environment.NewLine, codes.Select(code => code.CodeAsText));
            data.SetStorageItems(files);
            if (files.Count == 1)
                data.SetBitmap(RandomAccessStreamReference.CreateFromFile(files[0]));
        });
    }

    public static void ShareDecodedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("No decoded content to share.");

        ShowShareUI(data =>
        {
            data.Properties.Title = "Decoded codes";
            data.SetText(text);
        });
    }

    private static void ShowShareUI(Action<DataPackage> populateData)
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        DataTransferManager manager = GetForWindow(hwnd);

        // A dismissed share dialog may never request data. Replace its handler before sharing again.
        ClearPendingRequest();
        pendingManager = manager;
        pendingHandler = (_, args) =>
        {
            ClearPendingRequest();
            try
            {
                populateData(args.Request.Data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to provide shared content: {ex}");
                args.Request.FailWithDisplayText("Could not prepare content for sharing. Please try again.");
            }
        };
        manager.DataRequested += pendingHandler;

        try
        {
            ShowShareUIForWindow(hwnd);
        }
        catch
        {
            ClearPendingRequest();
            throw;
        }
    }

    private static void ClearPendingRequest()
    {
        if (pendingManager is not null && pendingHandler is not null)
            pendingManager.DataRequested -= pendingHandler;

        pendingManager = null;
        pendingHandler = null;
    }

    public static DataTransferManager GetForWindow(IntPtr hwnd)
    {
        IDataTransferManagerInterop interop = DataTransferManager.As<IDataTransferManagerInterop>();
        Guid iid = DataTransferManagerIid;
        IntPtr abi = interop.GetForWindow(hwnd, ref iid);
        return MarshalInterface<DataTransferManager>.FromAbi(abi);
    }

    public static void ShowShareUIForWindow(IntPtr hwnd)
    {
        IDataTransferManagerInterop interop = DataTransferManager.As<IDataTransferManagerInterop>();
        interop.ShowShareUIForWindow(hwnd);
    }
}
