using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class BrandEditDialog : ContentDialog
{
    public BrandEditDialogViewModel ViewModel { get; }

    public BrandItem? EditedItem { get; private set; }

    public BrandEditDialog(BrandItem original)
    {
        ViewModel = App.GetService<BrandEditDialogViewModel>();
        ViewModel.Initialize(original);
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        EditedItem = ViewModel.TryCreateEditedItem();
    }
}
