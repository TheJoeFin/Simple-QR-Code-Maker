using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Simple_QR_Code_Maker.Views;

public sealed partial class MainPage : Page
{
    private const double ScrollToCodesButtonBottomMargin = 20;
    private const double ScrollToCodesButtonTopGap = 12;
    private const double QrCodePreviewTileWidth = 308;
    private const double UrlTextBoxCompactWidth = 258;
    private const double UrlTextBoxChromeWidth = 32;
    private const double UrlInputRowSpacing = 6;

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
        UpdateUrlInputLayout();
        UpdateCodesLayout();
        UpdateFramePresetRadioButtons();
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

        UpdateUrlInputLayout();
    }

    private void QrCodeInputRow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateUrlInputLayout();
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

    private void ErrorCorrectionItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ErrorCorrectionOptions option)
        {
            ViewModel.SelectErrorCorrectionLevelCommand.Execute(option);
            ErrorCorrectionFlyout.Hide();
        }
    }

    private void FramePresetFlyout_Opened(object sender, object e)
    {
        UpdateFramePresetRadioButtons();
        UpdateFrameTextSourceRadioButtons();

        if (!ViewModel.IsFrameTextVisible)
            return;

        FrameTextTextBox.Focus(FocusState.Programmatic);
        FrameTextTextBox.SelectAll();
    }

    private void FramePresetRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton || radioButton.Tag is not string presetName)
            return;

        if (!Enum.TryParse(presetName, out QrFramePreset preset))
        {
            UpdateFramePresetRadioButtons();
            return;
        }

        if (ViewModel.FramePreset != preset)
            ViewModel.SelectFramePresetCommand.Execute(QrFramePresetCatalog.GetOption(preset));

        UpdateFramePresetRadioButtons();
    }

    private void FrameTextSourceRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton || radioButton.Tag is not string sourceName)
            return;

        if (!Enum.TryParse(sourceName, out QrFrameTextSource source))
        {
            UpdateFrameTextSourceRadioButtons();
            return;
        }

        if (ViewModel.FrameTextSource != source)
            ViewModel.FrameTextSource = source;

        UpdateFrameTextSourceRadioButtons();
    }

    private void WhatIsErrorCorrection_Click(object sender, RoutedEventArgs e)
    {
        ErrorCorrectionFlyout.Hide();
        WeakReferenceMessenger.Default.Send(new RequestPaneChange(MainViewPanes.Faq, PaneState.Open, "error correction"));
    }

    private void VCardSingleCodeDefault_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem toggleItem)
            ViewModel.SetVCardSingleCodeDefaultCommand.Execute(toggleItem.IsChecked);
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
        ViewModel.QrCodeBitmaps.CollectionChanged += QrCodeBitmaps_CollectionChanged;
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
        ViewModel.QrCodeBitmaps.CollectionChanged -= QrCodeBitmaps_CollectionChanged;
    }

    private void MainContentScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        UpdateScrollToCodesButton();
    }

    private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateUrlInputLayout();
        UpdateCodesLayout();
        UpdateScrollToCodesButton();
    }

    private void ScrollToCodesLayoutChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCodesLayout();
        UpdateScrollToCodesButton();
    }

    private void QrCodeBitmaps_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCodesLayout();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HasRequestedCodes) ||
            e.PropertyName == nameof(MainViewModel.ShowCodeInfoBar))
        {
            UpdateScrollToCodesButton();
        }

        if (e.PropertyName == nameof(MainViewModel.FramePreset))
        {
            UpdateFramePresetRadioButtons();
        }

        if (e.PropertyName == nameof(MainViewModel.FrameTextSource))
        {
            UpdateFrameTextSourceRadioButtons();
        }
    }

    private void UpdateFramePresetRadioButtons()
    {
        NoFramePresetRadioButton.IsChecked = ViewModel.FramePreset == QrFramePreset.None;
        BottomLabelFramePresetRadioButton.IsChecked = ViewModel.FramePreset == QrFramePreset.BottomLabel;
        RoundedFramePresetRadioButton.IsChecked = ViewModel.FramePreset == QrFramePreset.RoundedFrame;
        CornerCalloutFramePresetRadioButton.IsChecked = ViewModel.FramePreset == QrFramePreset.CornerCallout;
    }

    private void UpdateFrameTextSourceRadioButtons()
    {
        ManualFrameTextSourceRadioButton.IsChecked = ViewModel.FrameTextSource == QrFrameTextSource.Manual;
        ContentSummaryFrameTextSourceRadioButton.IsChecked = ViewModel.FrameTextSource == QrFrameTextSource.ContentSummary;
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

    private void UpdateUrlInputLayout()
    {
        if (MainContentStackPanel.ActualWidth <= 0)
            return;

        double importButtonWidth = UrlImportButton.ActualWidth > 0 ? UrlImportButton.ActualWidth : UrlImportButton.Width;
        double maxTextBoxWidth = Math.Max(0, Math.Min(UrlTextBox.MaxWidth, MainContentStackPanel.ActualWidth - importButtonWidth - UrlInputRowSpacing));
        double desiredTextBoxWidth = string.IsNullOrEmpty(UrlTextBox.Text)
            ? UrlTextBoxCompactWidth
            : Math.Max(UrlTextBoxCompactWidth, MeasureUrlTextWidth() + UrlTextBoxChromeWidth);
        double actualTextBoxWidth = Math.Min(desiredTextBoxWidth, maxTextBoxWidth);

        UrlTextBox.Width = actualTextBoxWidth;
        UrlTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        QrCodeInputRow.Width = actualTextBoxWidth + importButtonWidth + UrlInputRowSpacing;
        QrCodeInputRow.HorizontalAlignment = HorizontalAlignment.Center;
    }

    private void UpdateCodesLayout()
    {
        double availableWidth = MainContentScrollViewer.ViewportWidth;

        if (availableWidth <= 0)
            return;

        availableWidth = Math.Max(CodesBorder.MinWidth, availableWidth - 40);
        int codeCount = Math.Max(1, ViewModel.QrCodeBitmaps.Count);
        int maximumColumns = Math.Max(1, (int)Math.Floor(availableWidth / QrCodePreviewTileWidth));
        int displayedColumns = Math.Min(codeCount, maximumColumns);
        double preferredWidth = displayedColumns * QrCodePreviewTileWidth;

        double codesWidth = Math.Min(availableWidth, Math.Max(CodesBorder.MinWidth, preferredWidth));
        CodesBorder.Width = codesWidth;
        ListOfCodes.Width = codesWidth;
    }

    private double MeasureUrlTextWidth()
    {
        TextBlock measurementTextBlock = new()
        {
            FontFamily = UrlTextBox.FontFamily,
            FontSize = UrlTextBox.FontSize,
            FontStretch = UrlTextBox.FontStretch,
            FontStyle = UrlTextBox.FontStyle,
            FontWeight = UrlTextBox.FontWeight,
            Text = UrlTextBox.Text,
            TextWrapping = TextWrapping.NoWrap,
        };

        measurementTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return measurementTextBlock.DesiredSize.Width;
    }
}
