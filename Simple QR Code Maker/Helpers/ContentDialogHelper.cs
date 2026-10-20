using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Simple_QR_Code_Maker.Helpers;

public static class ContentDialogHelper
{
    public static async Task<ContentDialogResult> ShowThemedAsync(this ContentDialog dialog)
    {
        if (dialog.XamlRoot?.Content is not FrameworkElement rootElement)
            throw new InvalidOperationException("A dialog must have a XamlRoot before it is shown.");

        // ContentDialog popups do not inherit the window content's requested theme.
        dialog.RequestedTheme = rootElement.ActualTheme;
        rootElement.ActualThemeChanged += OnActualThemeChanged;
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            rootElement.ActualThemeChanged -= OnActualThemeChanged;
        }

        void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            dialog.RequestedTheme = sender.ActualTheme;
        }
    }
}
