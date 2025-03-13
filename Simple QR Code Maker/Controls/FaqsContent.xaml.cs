using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class FaqsContent : UserControl
{
    public FaqsContentViewModel ViewModel { get; } = new FaqsContentViewModel();

    public FaqsContent()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }
}
