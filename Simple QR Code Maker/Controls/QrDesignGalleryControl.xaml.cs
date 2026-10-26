using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using System.Diagnostics;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class QrDesignGalleryControl : UserControl
{
    private QrDesignDialog? dialog;
    private bool isShowing;

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MainViewModel), typeof(QrDesignGalleryControl), new PropertyMetadata(null));

    public QrDesignGalleryControl()
    {
        InitializeComponent();
    }

    private async void DesignButton_Click(object sender, RoutedEventArgs args)
    {
        if (isShowing)
            return;

        isShowing = true;
        DesignButton.IsEnabled = false;
        try
        {
            MainViewModel viewModel = ViewModel ?? throw new InvalidOperationException("The QR designer requires a view model.");
            if (dialog is null || dialog.ViewModel != viewModel)
                dialog = new QrDesignDialog(viewModel);
            dialog.XamlRoot = XamlRoot;
            await dialog.ShowThemedAsync();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Unable to open the QR designer: {exception}");
            WeakReferenceMessenger.Default.Send(new RequestShowMessage(
                "Unable to open designer", "Please close any other dialog and try again.", InfoBarSeverity.Error));
        }
        finally
        {
            isShowing = false;
            DesignButton.IsEnabled = true;
        }
    }

    private void Control_Unloaded(object sender, RoutedEventArgs args)
    {
        if (isShowing)
            dialog?.Hide();
    }
}
