using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class BrandRowItem : UserControl
{
    public BrandItem Data
    {
        get { return (BrandItem)GetValue(DataProperty); }
        set { SetValue(DataProperty, value); }
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(BrandItem), typeof(BrandRowItem), new PropertyMetadata(null));

    public bool ShowActions
    {
        get { return (bool)GetValue(ShowActionsProperty); }
        set { SetValue(ShowActionsProperty, value); }
    }

    public static readonly DependencyProperty ShowActionsProperty =
        DependencyProperty.Register(nameof(ShowActions), typeof(bool), typeof(BrandRowItem), new PropertyMetadata(false));

    public event RoutedEventHandler? EditRequested;

    public event RoutedEventHandler? SetDefaultRequested;

    public event RoutedEventHandler? DeleteRequested;

    public BrandRowItem()
    {
        this.InitializeComponent();
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditRequested?.Invoke(this, e);
    }

    private void SetDefaultMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetDefaultRequested?.Invoke(this, e);
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, e);
    }
}
