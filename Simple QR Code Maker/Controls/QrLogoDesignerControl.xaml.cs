using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Simple_QR_Code_Maker.ViewModels;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class QrLogoDesignerControl : UserControl
{
    public MainViewModel? ViewModel
    {
        get { return (MainViewModel?)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(QrLogoDesignerControl), new PropertyMetadata(null, OnViewModelChanged));

    public QrLogoDesignerControl()
    {
        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((QrLogoDesignerControl)d).Bindings.Update();
    }

    private void LogoPreviewContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimateOverlayOpacity(0.9);
        PickImageOverlay.IsHitTestVisible = true;
    }

    private void LogoPreviewContainer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimateOverlayOpacity(0);
        PickImageOverlay.IsHitTestVisible = false;
    }

    private void AnimateOverlayOpacity(double targetOpacity)
    {
        DoubleAnimation animation = new()
        {
            To = targetOpacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(animation, PickImageOverlay);
        Storyboard.SetTargetProperty(animation, "Opacity");

        Storyboard storyboard = new();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }
}
