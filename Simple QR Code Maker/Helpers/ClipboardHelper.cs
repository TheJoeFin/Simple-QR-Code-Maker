using Windows.ApplicationModel.DataTransfer;

namespace Simple_QR_Code_Maker.Helpers;

public static class ClipboardHelper
{
    /// <summary>
    /// Sets clipboard content. On packaged (MSIX) builds this keeps the "allowed in clipboard
    /// history" option; <see cref="Clipboard.SetContentWithOptions"/> is not implemented on the
    /// Uno desktop head, so there we fall back to the plain <see cref="Clipboard.SetContent"/>.
    /// </summary>
    public static void SetContent(DataPackage dataPackage)
    {
        if (RuntimeHelper.IsMSIX)
        {
            Clipboard.SetContentWithOptions(dataPackage, new ClipboardContentOptions { IsAllowedInHistory = true });
        }
        else
        {
            Clipboard.SetContent(dataPackage);
        }
    }
}
