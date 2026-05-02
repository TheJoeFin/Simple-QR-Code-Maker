using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class UrlBuilderDialog : ContentDialog
{
    public UrlBuilderDialogViewModel ViewModel
    {
        get;
    }

    public string? ResultText { get; private set; }

    public UrlBuilderDialog(string? initialText)
    {
        ViewModel = App.GetService<UrlBuilderDialogViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;
        ViewModel.Initialize(initialText);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        AuthorityTextBox.Focus(FocusState.Programmatic);
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ResultText = ViewModel.CreateResultText();
    }
}
