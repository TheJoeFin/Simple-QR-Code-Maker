using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class BrandManagementControl : UserControl
{
    public MainViewModel? ViewModel
    {
        get { return (MainViewModel?)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(BrandManagementControl), new PropertyMetadata(null, OnViewModelChanged));

    public BrandManagementControl()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((BrandManagementControl)d).Bindings.Update();
    }

    private void BrandFlyout_Opened(object sender, object e)
    {
        ViewModel?.MarkBrandButtonUsedCommand.Execute(null);
    }

    private void BrandListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView listView)
            return;

        if (TryApplySelectedBrand(listView))
            BrandFlyout.Hide();
    }

    private void ToggleNewBrandForm_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            ViewModel.IsNewBrandFormVisible = true;
    }

    private void BrandRowItem_EditRequested(object sender, RoutedEventArgs e)
    {
        if (sender is BrandRowItem row && ViewModel is not null)
            ViewModel.EditBrandCommand.Execute(row.Data);
    }

    private void BrandRowItem_SetDefaultRequested(object sender, RoutedEventArgs e)
    {
        if (sender is BrandRowItem row && ViewModel is not null)
            ViewModel.SetDefaultBrandCommand.Execute(row.Data);
    }

    private void BrandRowItem_DeleteRequested(object sender, RoutedEventArgs e)
    {
        if (sender is BrandRowItem row && ViewModel is not null)
            ViewModel.DeleteBrandCommand.Execute(row.Data);
    }

    private void BrandNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && ViewModel is not null)
        {
            ViewModel.CreateNewBrandCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void BrandSaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel is not null)
            ViewModel.CreateNewBrandCommand.Execute(null);

        args.Handled = true;
    }

    private void LearnMoreAboutBrands_Click(object sender, RoutedEventArgs e)
    {
        BrandFlyout.Hide();
        WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, "brand"));
    }

    private bool TryApplySelectedBrand(ListView listView)
    {
        if (ViewModel is null ||
            listView.SelectedItem is not BrandItem brand ||
            brand.Equals(ViewModel.SelectedBrand))
        {
            return false;
        }

        ViewModel.ApplyBrandCommand.Execute(brand);
        return true;
    }
}
