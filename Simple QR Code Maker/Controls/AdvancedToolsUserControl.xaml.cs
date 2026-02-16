using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class AdvancedToolsUserControl : UserControl
{
    public AdvancedToolsViewModel ViewModel { get; }

    public AdvancedToolsUserControl()
    {
        ViewModel = new AdvancedToolsViewModel();
        InitializeComponent();
    }
}
