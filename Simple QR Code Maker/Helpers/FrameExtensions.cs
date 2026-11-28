using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Views;

namespace Simple_QR_Code_Maker.Helpers;

public static class FrameExtensions
{
    public static object? GetPageViewModel(this Frame frame) => frame.Content switch
    {
        AboutQrCodesWebPage page => page.ViewModel,
        DecodingPage page => page.ViewModel,
        FolderSummaryPage page => page.ViewModel,
        MainPage page => page.ViewModel,
        SettingsPage page => page.ViewModel,
        ShellPage page => page.ViewModel,
        SpreadsheetImportPage page => page.ViewModel,
        _ => null,
    };
}
