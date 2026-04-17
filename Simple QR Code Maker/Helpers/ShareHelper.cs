using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using WinRT;

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
