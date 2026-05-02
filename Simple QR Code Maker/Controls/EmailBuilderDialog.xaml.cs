using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class EmailBuilderDialog : ContentDialog
{
    public EmailBuilderDialogViewModel ViewModel
    {
        get;
    }

    public string? ResultText { get; private set; }

    public EmailBuilderDialog(string? initialText)
    {
        ViewModel = App.GetService<EmailBuilderDialogViewModel>();
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;
        ViewModel.Initialize(initialText);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ToTextBox.Focus(FocusState.Programmatic);
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
