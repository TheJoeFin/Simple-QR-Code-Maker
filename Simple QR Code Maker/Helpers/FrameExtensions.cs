using Microsoft.UI.Xaml.Controls;
using System.Diagnostics.CodeAnalysis;

namespace Simple_QR_Code_Maker.Helpers;

public static class FrameExtensions
{
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "All pages in this app expose a ViewModel property; this method is only called on known page types.")]
    public static object? GetPageViewModel(this Frame frame) => frame?.Content?.GetType().GetProperty("ViewModel")?.GetValue(frame.Content, null);
}
