using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class AdvancedToolsUserControl : UserControl
{
    private AdvancedToolsViewModel viewModel = new();

    public AdvancedToolsViewModel ViewModel
    {
        get => viewModel;
        set
        {
            viewModel = value;

            if (Content is not null)
                Bindings.Update();
        }
    }

    public AdvancedToolsUserControl()
    {
        InitializeComponent();
    }
}
