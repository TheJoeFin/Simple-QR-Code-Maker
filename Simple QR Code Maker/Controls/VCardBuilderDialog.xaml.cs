using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class VCardBuilderDialog : ContentDialog
{
    public VCardBuilderDialogViewModel ViewModel
    {
        get;
    }

    public string? ResultText { get; private set; }

    public VCardBuilderDialog(string? initialText)
    {
        ViewModel = App.GetService<VCardBuilderDialogViewModel>();
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;
        ViewModel.Initialize(initialText);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        FormattedNameTextBox.Focus(FocusState.Programmatic);
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
