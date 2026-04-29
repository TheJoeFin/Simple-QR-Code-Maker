using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

    private void UndoAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.UndoCommand.CanExecute(null))
            ViewModel.UndoCommand.Execute(null);
    }

    private void RedoAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.RedoCommand.CanExecute(null))
            ViewModel.RedoCommand.Execute(null);
    }
}
