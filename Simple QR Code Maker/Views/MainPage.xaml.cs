using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Simple_QR_Code_Maker.Controls;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class MainPage : Page
{
    private const double ScrollToCodesButtonBottomMargin = 20;
    private const double ScrollToCodesButtonTopGap = 12;

    public MainViewModel ViewModel
    {
        get;
    }

    private bool _didSetCaretToEnd = false;
    private bool _isScrollToCodesEventsHooked = false;

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
        HookScrollToCodesEvents();
        UpdateScrollToCodesButton();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        UnhookScrollToCodesEvents();
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

        if (TryApplySelectedBrand(listView))
            BrandFlyout.Hide();
    }

    private void BrandPickerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView listView)
            return;

        if (TryApplySelectedBrand(listView))
            BrandPickerFlyout.Hide();
    }

    private void ToggleNewBrandForm_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsNewBrandFormVisible = true;
    }

    private void BrandRowItem_EditRequested(object sender, RoutedEventArgs e)
    {
        if (sender is BrandRowItem row)
            ViewModel.EditBrandCommand.Execute(row.Data);
    }

    private void BrandRowItem_SetDefaultRequested(object sender, RoutedEventArgs e)
    {
        if (sender is BrandRowItem row)
            ViewModel.SetDefaultBrandCommand.Execute(row.Data);
    }

    private void BrandRowItem_DeleteRequested(object sender, RoutedEventArgs e)
    {
        if (sender is BrandRowItem row)
            ViewModel.DeleteBrandCommand.Execute(row.Data);
    }

    private bool TryApplySelectedBrand(ListView listView)
    {
        if (listView.SelectedItem is not BrandItem brand || brand.Equals(ViewModel.SelectedBrand))
            return false;

        ViewModel.ApplyBrandCommand.Execute(brand);
        return true;
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
            ViewModel.SelectErrorCorrectionLevelCommand.Execute(option);
            ErrorCorrectionFlyout.Hide();
        }
    }

    private void WhatIsErrorCorrection_Click(object sender, RoutedEventArgs e)
    {
        ErrorCorrectionFlyout.Hide();
        WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, "error correction"));
    }

    private void LearnMoreAboutBrands_Click(object sender, RoutedEventArgs e)
    {
        BrandFlyout.Hide();
        WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, "brand"));
    }

    private void HookScrollToCodesEvents()
    {
        if (_isScrollToCodesEventsHooked)
            return;

        _isScrollToCodesEventsHooked = true;
        MainContentScrollViewer.ViewChanged += MainContentScrollViewer_ViewChanged;
        MainContentScrollViewer.SizeChanged += ScrollToCodesLayoutChanged;
        MainContentStackPanel.SizeChanged += ScrollToCodesLayoutChanged;
        CodesSection.SizeChanged += ScrollToCodesLayoutChanged;
        ScrollToCodesButton.SizeChanged += ScrollToCodesLayoutChanged;
        SizeChanged += MainPage_SizeChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void UnhookScrollToCodesEvents()
    {
        if (!_isScrollToCodesEventsHooked)
            return;

        _isScrollToCodesEventsHooked = false;
        MainContentScrollViewer.ViewChanged -= MainContentScrollViewer_ViewChanged;
        MainContentScrollViewer.SizeChanged -= ScrollToCodesLayoutChanged;
        MainContentStackPanel.SizeChanged -= ScrollToCodesLayoutChanged;
        CodesSection.SizeChanged -= ScrollToCodesLayoutChanged;
        ScrollToCodesButton.SizeChanged -= ScrollToCodesLayoutChanged;
        SizeChanged -= MainPage_SizeChanged;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void MainContentScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        UpdateScrollToCodesButton();
    }

    private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollToCodesButton();
    }

    private void ScrollToCodesLayoutChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollToCodesButton();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HasRequestedCodes) ||
            e.PropertyName == nameof(MainViewModel.ShowCodeInfoBar))
        {
            UpdateScrollToCodesButton();
        }
    }

    private void ScrollToCodesButton_Click(object sender, RoutedEventArgs e)
    {
        Point codesSectionPoint = CodesSection.TransformToVisual(MainContentStackPanel).TransformPoint(new Point(0, 0));
        double targetOffset = Math.Max(0, codesSectionPoint.Y - 12);
        MainContentScrollViewer.ChangeView(null, targetOffset, null, false);
    }

    private void UpdateScrollToCodesButton()
    {
        if (!_isScrollToCodesEventsHooked ||
            !ViewModel.HasRequestedCodes ||
            MainContentScrollViewer.ViewportHeight <= 0 ||
            CodesSection.ActualHeight <= 0)
        {
            ScrollToCodesButton.Visibility = Visibility.Collapsed;
            return;
        }

        Point codesSectionPoint = CodesSection.TransformToVisual(MainContentScrollViewer).TransformPoint(new Point(0, 0));
        double codesSectionTop = codesSectionPoint.Y;
        double codesSectionBottom = codesSectionTop + CodesSection.ActualHeight;
        bool areCodesVisibleInViewport = codesSectionBottom > 0 && codesSectionTop < MainContentScrollViewer.ViewportHeight;

        if (areCodesVisibleInViewport)
        {
            ScrollToCodesButton.Visibility = Visibility.Collapsed;
            return;
        }

        double buttonHeight = ScrollToCodesButton.ActualHeight > 0 ? ScrollToCodesButton.ActualHeight : ScrollToCodesButton.Height;
        Point scrollViewerPoint = MainContentScrollViewer.TransformToVisual(PageRootGrid).TransformPoint(new Point(0, 0));
        Point codesSectionRootPoint = CodesSection.TransformToVisual(PageRootGrid).TransformPoint(new Point(0, 0));
        double infoBarInset = LenghErrorInfoBar.IsOpen ? LenghErrorInfoBar.ActualHeight + 12 : 0;
        double stickyTop = scrollViewerPoint.Y + MainContentScrollViewer.ViewportHeight - buttonHeight - ScrollToCodesButtonBottomMargin - infoBarInset;
        double top = stickyTop;

        if (codesSectionTop >= MainContentScrollViewer.ViewportHeight)
        {
            double maximumTopBeforeCodes = codesSectionRootPoint.Y - buttonHeight - ScrollToCodesButtonTopGap;
            top = Math.Min(stickyTop, maximumTopBeforeCodes);
        }

        ScrollToCodesButton.Margin = new Thickness(0, Math.Max(scrollViewerPoint.Y, top), 20, 0);
        ScrollToCodesButton.Visibility = Visibility.Visible;
    }
}
