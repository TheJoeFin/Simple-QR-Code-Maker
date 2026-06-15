using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.ViewModels;
using SkiaSharp;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class RemoveBackgroundDialog : ContentDialog
{
    public RemoveBackgroundDialogViewModel ViewModel { get; }

    public SKBitmap? ResultBitmap => ViewModel.ResultBitmap;

    public RemoveBackgroundDialog(SKBitmap sourceImage)
    {
        ViewModel = App.GetService<RemoveBackgroundDialogViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        ViewModel.Initialize(sourceImage);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.ProcessAsync();
    }
}
