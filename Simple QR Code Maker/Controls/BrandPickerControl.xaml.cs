using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class BrandPickerControl : UserControl
{
    public MainViewModel? ViewModel
    {
        get { return (MainViewModel?)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(BrandPickerControl), new PropertyMetadata(null, OnViewModelChanged));

    public BrandPickerControl()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((BrandPickerControl)d).Bindings.Update();
    }

    private void BrandPickerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView listView)
            return;

        if (TryApplySelectedBrand(listView))
            BrandPickerFlyout.Hide();
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
