using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class GenerateIdsDialog : ContentDialog
{
    public GenerateIdsDialogViewModel ViewModel
    {
        get;
    }

    public string? ResultText { get; private set; }

    public GenerateIdsDialog()
    {
        ViewModel = App.GetService<GenerateIdsDialogViewModel>();
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        CountNumberBox.Focus(FocusState.Programmatic);
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
