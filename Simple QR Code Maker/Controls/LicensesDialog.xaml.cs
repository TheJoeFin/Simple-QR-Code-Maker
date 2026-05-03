using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class LicensesDialog : ContentDialog
{
    public LicensesDialogViewModel ViewModel { get; }

    public LicensesDialog()
    {
        ViewModel = App.GetService<LicensesDialogViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}
