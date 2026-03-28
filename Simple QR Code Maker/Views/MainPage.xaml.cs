using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }

    private bool _didSetCaretToEnd = false;

    private readonly string appStoreUrl = "https://apps.microsoft.com/detail/9nch56g3rqfc";

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UrlTextBox.Focus(FocusState.Programmatic);
        // set the caret to the end of the text when navigating back to the page
        UrlTextBox.Select(UrlTextBox.Text.Length, 0);
    }

    private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // set the caret to the end of the text when loading for the first time
        if (!_didSetCaretToEnd)
        {
            _didSetCaretToEnd = true;
            UrlTextBox.Select(UrlTextBox.Text.Length, 0);
        }
    }

    private void CopyLinkButton_Click(object sender, RoutedEventArgs e)
    {
        DataPackage dataPackage = new();
        dataPackage.SetText(appStoreUrl);
        Clipboard.SetContent(dataPackage);
    }

    private async void VisitLinkButton_Click(object sender, RoutedEventArgs e)
    {
        _ = await Windows.System.Launcher.LaunchUriAsync(new Uri(appStoreUrl));
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

    private void BrandForegroundColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is BrandItem brand)
        {
            ViewModel.ApplyBrandForegroundCommand.Execute(brand);
        }
    }

    private void BrandBackgroundColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is BrandItem brand)
        {
            ViewModel.ApplyBrandBackgroundCommand.Execute(brand);
        }
    }

    private void BrandListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView listView)
            return;

        if (listView.SelectedItem is BrandItem brand && !brand.Equals(ViewModel.SelectedBrand))
        {
            ViewModel.ApplyBrandCommand.Execute(brand);
            BrandFlyout.Hide();
        }
    }

    private void BrandPickerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not BrandItem brand)
            return;

        comboBox.SelectedItem = null;
        ViewModel.ApplyBrandCommand.Execute(brand);
    }

    private void ToggleNewBrandForm_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsNewBrandFormVisible = true;
    }

    private void EditBrand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is BrandItem brand)
            ViewModel.EditBrandCommand.Execute(brand);
    }

    private void SetDefaultBrand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is BrandItem brand)
            ViewModel.SetDefaultBrandCommand.Execute(brand);
    }

    private void DeleteBrand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is BrandItem brand)
        {
            ViewModel.DeleteBrandCommand.Execute(brand);
        }
    }

    private void BrandNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ViewModel.CreateNewBrandCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void BrandSaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.CreateNewBrandCommand.Execute(null);
        args.Handled = true;
    }

    private void ErrorCorrectionItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ErrorCorrectionOptions option)
        {
            ViewModel.SelectedOption = option;
            ErrorCorrectionFlyout.Hide();
        }
    }

    private void WhatIsErrorCorrection_Click(object sender, RoutedEventArgs e)
    {
        ErrorCorrectionFlyout.Hide();
        WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, "error correction"));
    }
}
