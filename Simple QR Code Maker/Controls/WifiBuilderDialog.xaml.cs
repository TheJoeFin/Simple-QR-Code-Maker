using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class WifiBuilderDialog : ContentDialog
{
    public WifiBuilderDialogViewModel ViewModel
    {
        get;
    }

    public string? ResultText { get; private set; }

    public WifiBuilderDialog(string? initialText)
    {
        ViewModel = App.GetService<WifiBuilderDialogViewModel>();
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;
        ViewModel.Initialize(initialText);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        SsidTextBox.Focus(FocusState.Programmatic);
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!ViewModel.TryCreateResultText(out string resultText))
        {
            args.Cancel = true;
            return;
        }

        ResultText = resultText;
    }
}
