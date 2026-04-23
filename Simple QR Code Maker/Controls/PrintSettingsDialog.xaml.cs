using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Diagnostics;
using System.Globalization;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class PrintSettingsDialog : ContentDialog
{
    private static readonly int[] CodesPerPageOptions = PrintJobSettings.SupportedCodesPerPage;
    private static readonly PrintPageType[] PageTypeOptions = PrintPageTypeHelper.SelectablePageTypes;
    private static readonly PrintPageLayout[] PageLayoutOptions = [PrintPageLayout.Portrait, PrintPageLayout.Landscape];
    private const double MillimetersPerInch = 25.4;
    private const double PreviewPageDisplayWidth = 720;
    private const double PreviewPageRenderWidth = 1440;
    private const double PreviewPageInnerPadding = 16;
    private const double PreviewViewportPadding = 32;
    private const double ScanSizeToleranceMillimeters = 0.1;
    private const float PreviewZoomStep = 0.1f;
    private static readonly TimeSpan PreviewRefreshDebounceDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan DeferredCleanupDelay = TimeSpan.FromMinutes(10);
    private readonly IPrintService printService;
    private readonly IReadOnlyList<RequestedQrCodeItem> codes;
    private readonly QrRenderSettingsSnapshot renderSettings;
    private readonly double maxScanDistanceScaleFactor;
    private readonly int largestQrDimension;
    private readonly List<string> generatedPdfPaths = [];
    private readonly List<Border> previewPageBorders = [];
    private readonly bool useMetricUnits;
    private readonly string lengthUnitSuffix;
    private CancellationTokenSource? previewRefreshDebounceCts;
    private CancellationTokenSource? previewCts;
    private bool isLoaded;
    private bool isPreviewFitToWidth = true;
    private bool suppressRefresh;
    private bool suppressPresetSelectionChanged;
    private string? currentPdfPath;

    public PrintSettingsDialog(
        IPrintService printService,
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot renderSettings,
        PrintJobSettings initialSettings,
        double maxScanDistanceScaleFactor)
    {
        InitializeComponent();
        this.printService = printService;
        this.codes = codes;
        this.renderSettings = renderSettings;
        this.maxScanDistanceScaleFactor = maxScanDistanceScaleFactor;
        largestQrDimension = DetermineLargestQrDimension(codes, renderSettings.ErrorCorrectionLevel);
        useMetricUnits = UsesMetricLengths();
        lengthUnitSuffix = useMetricUnits ? "mm" : "in";

        PrintJobSettings normalizedInitialSettings = initialSettings.Normalize();
        ConfigureLengthInputs(normalizedInitialSettings);
        suppressRefresh = true;
        ApplySettingsToControls(normalizedInitialSettings);
        suppressRefresh = false;
        UpdateQuickPresetSelectionFromControls();
        UpdatePrintGuidance();

        IsPrimaryButtonEnabled = false;
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public PrintJobSettings ResultSettings => new PrintJobSettings()
    {
        CodesPerPage = CodesPerPageCombo.SelectedIndex > 0
            ? CodesPerPageOptions[CodesPerPageCombo.SelectedIndex - 1]
            : PrintJobSettings.DefaultCodesPerPage,
        PageType = PageTypeCombo.SelectedIndex >= 0
            ? PageTypeOptions[PageTypeCombo.SelectedIndex]
            : PrintPageTypeHelper.GetRegionalDefault(),
        PageLayout = PageLayoutCombo.SelectedIndex >= 0
            ? PageLayoutOptions[PageLayoutCombo.SelectedIndex]
            : PrintJobSettings.DefaultPageLayout,
        CodeSizeMm = ToMillimeters(CodeSizeMmBox.Value),
        SpacingMm = ToMillimeters(SpacingMmBox.Value),
        MarginMm = ToMillimeters(MarginMmBox.Value),
        ShowLabels = ShowLabelsSwitch.IsOn,
        FitAsManyAsPossible = CodesPerPageCombo.SelectedIndex == 0,
    }.Normalize();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (isLoaded)
            return;

        isLoaded = true;
        ResetDialogScrollPosition();

        try
        {
            await RefreshPreviewAsync();
            ResetDialogScrollPosition();
        }
        catch (Exception ex)
        {
            ShowPreviewError($"Couldn't start the PDF preview. {ex.Message}");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        previewRefreshDebounceCts?.Cancel();
        previewRefreshDebounceCts?.Dispose();
        previewRefreshDebounceCts = null;

        previewCts?.Cancel();
        previewCts?.Dispose();
        previewCts = null;

        ScheduleDeferredCleanup(generatedPdfPaths);
    }

    private void CodesPerPageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => QueuePreviewRefresh();

    private void PageTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => QueuePreviewRefresh();

    private void PageLayoutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => QueuePreviewRefresh();

    private void CodeSizeMmBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => QueuePreviewRefresh();

    private void SpacingMmBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => QueuePreviewRefresh();

    private void MarginMmBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => QueuePreviewRefresh();

    private void ShowLabelsSwitch_Toggled(object sender, RoutedEventArgs e) => QueuePreviewRefresh();

    private void QuickPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateQuickPresetDescription();

        if (suppressPresetSelectionChanged || QuickPresetCombo.SelectedIndex <= 0)
        {
            return;
        }

        ApplyQuickPreset(QuickPresetCombo.SelectedIndex);
    }

    private void PreviewScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!isLoaded || previewPageBorders.Count == 0)
            return;

        if (isPreviewFitToWidth)
        {
            _ = ApplyFitToWidthAsync();
        }
    }

    private void PreviewScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        PreviewZoomTextBlock.Text = $"{Math.Round(PreviewScrollViewer.ZoomFactor * 100)}%";
    }

    private void QueuePreviewRefresh()
    {
        if (suppressRefresh || !isLoaded)
            return;

        UpdateQuickPresetSelectionFromControls();
        UpdatePrintGuidance();

        previewRefreshDebounceCts?.Cancel();
        previewRefreshDebounceCts?.Dispose();

        previewRefreshDebounceCts = new CancellationTokenSource();
        _ = RefreshPreviewAfterDelayAsync(previewRefreshDebounceCts.Token);
    }

    private async Task RefreshPreviewAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(PreviewRefreshDebounceDelay, token);
            token.ThrowIfCancellationRequested();
            await RefreshPreviewAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshPreviewAsync()
    {
        previewCts?.Cancel();
        previewCts?.Dispose();

        previewCts = new CancellationTokenSource();
        CancellationToken token = previewCts.Token;

        ShowPreviewLoading();

        try
        {
            string pdfPath = await printService.GenerateQrPdfAsync(codes, renderSettings, ResultSettings, token);
            if (token.IsCancellationRequested)
            {
                TryDeleteFile(pdfPath);
                return;
            }

            generatedPdfPaths.Add(pdfPath);
            currentPdfPath = pdfPath;
            await RenderPreviewPagesAsync(pdfPath, token);
            if (!token.IsCancellationRequested)
            {
                PreviewLoadingPanel.Visibility = Visibility.Collapsed;
                PreviewErrorPanel.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = true;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowPreviewError($"Couldn't generate the PDF preview. {ex.Message}");
        }
    }

    private async Task RenderPreviewPagesAsync(string pdfPath, CancellationToken token)
    {
        StorageFile pdfFile = await StorageFile.GetFileFromPathAsync(pdfPath);
        PdfDocument pdfDocument = await PdfDocument.LoadFromFileAsync(pdfFile);
        List<FrameworkElement> previewPages = [];
        double pageWidth = PreviewPageDisplayWidth;
        double imageWidth = GetPreviewImageWidth(pageWidth);

        for (uint index = 0; index < pdfDocument.PageCount; index++)
        {
            token.ThrowIfCancellationRequested();

            using PdfPage page = pdfDocument.GetPage(index);
            using InMemoryRandomAccessStream renderedStream = new();
            double pageAspectRatio = page.Size.Height / page.Size.Width;

            PdfPageRenderOptions renderOptions = new()
            {
                DestinationWidth = (uint)Math.Round(PreviewPageRenderWidth),
                DestinationHeight = (uint)Math.Round(PreviewPageRenderWidth * pageAspectRatio),
                BackgroundColor = new Color { A = 255, R = 255, G = 255, B = 255 },
            };

            await page.RenderToStreamAsync(renderedStream, renderOptions);
            renderedStream.Seek(0);

            BitmapImage bitmap = new();
            await bitmap.SetSourceAsync(renderedStream);

            Border pageBorder = new()
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(PreviewPageInnerPadding / 2),
                Width = pageWidth,
                Child = new Image
                {
                    Source = bitmap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Stretch = Stretch.Uniform,
                    Width = imageWidth,
                },
            };

            previewPages.Add(pageBorder);
        }

        token.ThrowIfCancellationRequested();

        PreviewPagesPanel.Children.Clear();
        previewPageBorders.Clear();
        foreach (FrameworkElement page in previewPages)
        {
            PreviewPagesPanel.Children.Add(page);
            if (page is Border border)
            {
                previewPageBorders.Add(border);
            }
        }

        isPreviewFitToWidth = true;
        await ApplyFitToWidthAsync();
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (currentPdfPath is null)
        {
            args.Cancel = true;
            ShowPreviewError("Wait for the PDF preview to finish loading before printing.");
            return;
        }

        args.Cancel = true;
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();

        try
        {
            await printService.PrintPdfAsync(currentPdfPath);
            Hide();
        }
        catch (Exception ex)
        {
            ShowPreviewError($"Couldn't send the generated PDF to the printer. {ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void ShowPreviewLoading()
    {
        IsPrimaryButtonEnabled = false;
        PreviewLoadingPanel.Visibility = Visibility.Visible;
        PreviewErrorPanel.Visibility = Visibility.Collapsed;
        PreviewPagesPanel.Children.Clear();
        previewPageBorders.Clear();
        PreviewZoomTextBlock.Text = "100%";
    }

    private void ShowPreviewError(string message)
    {
        IsPrimaryButtonEnabled = false;
        PreviewLoadingPanel.Visibility = Visibility.Collapsed;
        PreviewErrorTextBlock.Text = message;
        PreviewErrorPanel.Visibility = Visibility.Visible;
        previewPageBorders.Clear();
    }

    private async void FitPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        isPreviewFitToWidth = true;
        await ApplyFitToWidthAsync();
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyZoomStep(-PreviewZoomStep);
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyZoomStep(PreviewZoomStep);
    }

    private static double GetPreviewImageWidth(double pageWidth) => Math.Max(pageWidth - PreviewPageInnerPadding, 1);

    private void ConfigureLengthInputs(PrintJobSettings settings)
    {
        CodeSizeMmBox.Header = GetLengthHeader("QR size");
        SpacingMmBox.Header = GetLengthHeader("Spacing");
        MarginMmBox.Header = GetLengthHeader("Page margin");

        ConfigureLengthBox(CodeSizeMmBox, settings.CodeSizeMm, PrintJobSettings.MinimumCodeSizeMm, PrintJobSettings.MaximumCodeSizeMm, useMetricUnits ? 1 : 0.05);
        ConfigureLengthBox(SpacingMmBox, settings.SpacingMm, PrintJobSettings.MinimumSpacingMm, PrintJobSettings.MaximumSpacingMm, useMetricUnits ? 1 : 0.05);
        ConfigureLengthBox(MarginMmBox, settings.MarginMm, PrintJobSettings.MinimumMarginMm, PrintJobSettings.MaximumMarginMm, useMetricUnits ? 1 : 0.05);
    }

    private void ConfigureLengthBox(NumberBox box, double valueMm, double minMm, double maxMm, double step)
    {
        box.Minimum = ToDisplayLength(minMm);
        box.Maximum = ToDisplayLength(maxMm);
        box.SmallChange = step;
        box.Value = ToDisplayLength(valueMm);
    }

    private string GetLengthHeader(string title) => $"{title} ({lengthUnitSuffix})";

    private double ToDisplayLength(double millimeters)
    {
        return useMetricUnits
            ? millimeters
            : Math.Round(millimeters / MillimetersPerInch, 2, MidpointRounding.AwayFromZero);
    }

    private double ToMillimeters(double displayValue)
    {
        return useMetricUnits
            ? displayValue
            : displayValue * MillimetersPerInch;
    }

    private static bool UsesMetricLengths()
    {
        try
        {
            RegionInfo region = new(CultureInfo.CurrentCulture.Name);
            return region.IsMetric;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void ResetDialogScrollPosition()
    {
        DialogScrollViewer.ChangeView(null, 0, null, true);
        QuickPresetCombo.Focus(FocusState.Programmatic);
    }

    private void ApplySettingsToControls(PrintJobSettings settings)
    {
        PrintJobSettings normalizedSettings = settings.Normalize();
        PrintPageType resolvedPageType = PrintPageTypeHelper.Resolve(normalizedSettings.PageType);
        int codesPerPageIndex = Array.IndexOf(CodesPerPageOptions, normalizedSettings.CodesPerPage);

        CodesPerPageCombo.SelectedIndex = normalizedSettings.FitAsManyAsPossible
            ? 0
            : codesPerPageIndex >= 0
                ? codesPerPageIndex + 1
                : Array.IndexOf(CodesPerPageOptions, PrintJobSettings.DefaultCodesPerPage) + 1;
        PageTypeCombo.SelectedIndex = Math.Max(Array.IndexOf(PageTypeOptions, resolvedPageType), 0);
        PageLayoutCombo.SelectedIndex = Math.Max(Array.IndexOf(PageLayoutOptions, normalizedSettings.PageLayout), 0);
        ShowLabelsSwitch.IsOn = normalizedSettings.ShowLabels;
        CodeSizeMmBox.Value = ToDisplayLength(normalizedSettings.CodeSizeMm);
        SpacingMmBox.Value = ToDisplayLength(normalizedSettings.SpacingMm);
        MarginMmBox.Value = ToDisplayLength(normalizedSettings.MarginMm);
    }

    private void ApplyQuickPreset(int presetIndex)
    {
        PrintJobSettings presetSettings = GetQuickPresetSettings(ResultSettings, presetIndex);

        suppressRefresh = true;
        ApplySettingsToControls(presetSettings);
        suppressRefresh = false;

        UpdatePrintGuidance();
        QueuePreviewRefresh();
    }

    private PrintJobSettings GetQuickPresetSettings(PrintJobSettings currentSettings, int presetIndex)
    {
        PrintJobSettings normalizedCurrent = currentSettings.Normalize();
        return presetIndex switch
        {
            1 => normalizedCurrent with
            {
                FitAsManyAsPossible = true,
                CodeSizeMm = 25,
                SpacingMm = 3,
                MarginMm = 6,
                ShowLabels = true,
            },
            2 => normalizedCurrent with
            {
                FitAsManyAsPossible = false,
                CodesPerPage = 4,
                CodeSizeMm = 45,
                SpacingMm = 8,
                MarginMm = 10,
                ShowLabels = true,
            },
            3 => normalizedCurrent with
            {
                FitAsManyAsPossible = false,
                CodesPerPage = 2,
                CodeSizeMm = 65,
                SpacingMm = 12,
                MarginMm = 12,
                ShowLabels = true,
            },
            4 => normalizedCurrent with
            {
                FitAsManyAsPossible = false,
                CodesPerPage = 1,
                CodeSizeMm = 140,
                SpacingMm = 6,
                MarginMm = 12,
                ShowLabels = false,
            },
            _ => normalizedCurrent,
        };
    }

    private void UpdateQuickPresetSelectionFromControls()
    {
        int matchingPresetIndex = GetMatchingQuickPresetIndex(ResultSettings);

        suppressPresetSelectionChanged = true;
        QuickPresetCombo.SelectedIndex = matchingPresetIndex;
        suppressPresetSelectionChanged = false;

        UpdateQuickPresetDescription();
    }

    private int GetMatchingQuickPresetIndex(PrintJobSettings settings)
    {
        for (int presetIndex = 1; presetIndex <= 4; presetIndex++)
        {
            if (MatchesQuickPreset(settings, presetIndex))
            {
                return presetIndex;
            }
        }

        return 0;
    }

    private bool MatchesQuickPreset(PrintJobSettings settings, int presetIndex)
    {
        PrintJobSettings normalizedSettings = settings.Normalize();
        PrintJobSettings presetSettings = GetQuickPresetSettings(normalizedSettings, presetIndex).Normalize();

        if (normalizedSettings.FitAsManyAsPossible != presetSettings.FitAsManyAsPossible)
        {
            return false;
        }

        if (!normalizedSettings.FitAsManyAsPossible && normalizedSettings.CodesPerPage != presetSettings.CodesPerPage)
        {
            return false;
        }

        return Math.Abs(normalizedSettings.CodeSizeMm - presetSettings.CodeSizeMm) <= ScanSizeToleranceMillimeters
            && Math.Abs(normalizedSettings.SpacingMm - presetSettings.SpacingMm) <= ScanSizeToleranceMillimeters
            && Math.Abs(normalizedSettings.MarginMm - presetSettings.MarginMm) <= ScanSizeToleranceMillimeters
            && normalizedSettings.ShowLabels == presetSettings.ShowLabels;
    }

    private void UpdateQuickPresetDescription()
    {
        QuickPresetDescriptionTextBlock.Text = QuickPresetCombo.SelectedIndex switch
        {
            1 => "Small, close-range QR Codes for packaging, shelf tags, and asset labels.",
            2 => "Medium QR Codes for inserts, flyers, invoices, and one-page handouts.",
            3 => "Larger QR Codes for restaurant menus, tabletop displays, and counter cards.",
            4 => "Single large QR Codes for doors, walls, posters, and event signage.",
            _ => "Choose a common QR printing scenario or keep the layout fully custom.",
        };
    }

    private void UpdatePrintGuidance()
    {
        PrintJobSettings settings = ResultSettings;
        PrintLayoutMetrics layoutMetrics = PrintLayoutHelper.CreateMetrics(settings);
        double actualCodeSizeMm = PrintLayoutHelper.PointsToMillimeters(layoutMetrics.ActualCodeSizePoints);
        double requestedCodeSizeMm = settings.CodeSizeMm;
        int pageCount = Math.Max(1, (int)Math.Ceiling((double)codes.Count / layoutMetrics.CodesPerPage));
        bool shrinksRequestedSize = actualCodeSizeMm + ScanSizeToleranceMillimeters < requestedCodeSizeMm;

        LayoutSummaryInfoBar.Severity = shrinksRequestedSize ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
        LayoutSummaryTextBlock.Text = settings.FitAsManyAsPossible
            ? $"This layout fits {layoutMetrics.CodesPerPage} codes per page in a {layoutMetrics.Rows} x {layoutMetrics.Columns} grid and uses {FormatCount(pageCount, "page")}. Each QR prints at {FormatLength(actualCodeSizeMm)}."
            : $"This layout prints {layoutMetrics.CodesPerPage} codes per page in a {layoutMetrics.Rows} x {layoutMetrics.Columns} grid and uses {FormatCount(pageCount, "page")}. Each QR prints at {FormatLength(actualCodeSizeMm)}.";

        if (shrinksRequestedSize)
        {
            LayoutSummaryTextBlock.Text += $" The requested size of {FormatLength(requestedCodeSizeMm)} is reduced to fit the page.";
        }

        Windows.UI.Color foreground = ToWindowsColor(renderSettings.ForegroundColor);
        Windows.UI.Color background = ToWindowsColor(renderSettings.BackgroundColor);
        double scanDistanceInches = 32 * Math.Max(maxScanDistanceScaleFactor, 0.35);

        if (BarcodeHelpers.TryGetMinimumRecommendedSideMillimeters(
            scanDistanceInches,
            largestQrDimension,
            foreground,
            background,
            out double recommendedMinimumMillimeters,
            renderSettings.QrPaddingModules))
        {
            bool belowRecommendedSize = actualCodeSizeMm + ScanSizeToleranceMillimeters < recommendedMinimumMillimeters;
            SizingGuidanceInfoBar.Title = "Scan reliability";
            SizingGuidanceInfoBar.Severity = belowRecommendedSize
                ? InfoBarSeverity.Warning
                : shrinksRequestedSize
                    ? InfoBarSeverity.Informational
                    : InfoBarSeverity.Success;

            SizingGuidanceTextBlock.Text = belowRecommendedSize
                ? $"The densest QR in this job needs about {FormatLength(recommendedMinimumMillimeters)} for the current max scan distance, but this layout prints at {FormatLength(actualCodeSizeMm)}. Increase QR size, reduce codes per page, turn off labels, or use a larger sheet."
                : shrinksRequestedSize
                    ? $"The page trims the requested QR size down to {FormatLength(actualCodeSizeMm)}, which still stays above the recommended minimum of {FormatLength(recommendedMinimumMillimeters)} for the densest QR in this job."
                    : $"The densest QR in this job stays above the recommended minimum of {FormatLength(recommendedMinimumMillimeters)} for the current max scan distance.";
            return;
        }

        QrCodeSizeRecommendation recommendation = BarcodeHelpers.GetSizeRecommendation(
            scanDistanceInches,
            largestQrDimension,
            foreground,
            background,
            renderSettings.QrPaddingModules);

        SizingGuidanceInfoBar.Title = "Scan guidance";
        SizingGuidanceInfoBar.Severity = recommendation.Kind is QrCodeSizeRecommendationKind.LowContrast or QrCodeSizeRecommendationKind.Error
            ? InfoBarSeverity.Warning
            : InfoBarSeverity.Informational;
        SizingGuidanceTextBlock.Text = $"{recommendation.Text} This layout prints at {FormatLength(actualCodeSizeMm)}.";
    }

    private string FormatLength(double millimeters)
    {
        double displayValue = ToDisplayLength(millimeters);
        return $"{displayValue:0.##} {lengthUnitSuffix}";
    }

    private static string FormatCount(int value, string singularNoun) => value == 1
        ? $"1 {singularNoun}"
        : $"{value} {singularNoun}s";

    private static int DetermineLargestQrDimension(IReadOnlyList<RequestedQrCodeItem> requestedCodes, ErrorCorrectionLevel errorCorrectionLevel)
    {
        int largestDimension = 21;

        foreach (RequestedQrCodeItem requestedCode in requestedCodes)
        {
            QRCode qrCode = Encoder.encode(requestedCode.CodeAsText, errorCorrectionLevel);
            largestDimension = Math.Max(largestDimension, qrCode.Version.DimensionForVersion);
        }

        return largestDimension;
    }

    private static Windows.UI.Color ToWindowsColor(System.Drawing.Color color)
    {
        return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    private void ApplyZoomStep(float delta)
    {
        if (previewPageBorders.Count == 0)
        {
            return;
        }

        isPreviewFitToWidth = false;
        float nextZoom = Math.Clamp(PreviewScrollViewer.ZoomFactor + delta, PreviewScrollViewer.MinZoomFactor, PreviewScrollViewer.MaxZoomFactor);
        PreviewScrollViewer.ChangeView(null, null, nextZoom, true);
    }

    private async Task ApplyFitToWidthAsync()
    {
        if (previewPageBorders.Count == 0)
        {
            return;
        }

        await Task.Yield();

        double availableWidth = Math.Max(PreviewScrollViewer.ActualWidth - PreviewViewportPadding, 1);
        float fitZoom = (float)Math.Clamp(availableWidth / PreviewPageDisplayWidth, PreviewScrollViewer.MinZoomFactor, PreviewScrollViewer.MaxZoomFactor);
        PreviewScrollViewer.ChangeView(0, 0, fitZoom, true);
    }

    private static void ScheduleDeferredCleanup(IEnumerable<string> paths)
    {
        string[] files = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DeferredCleanupDelay);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            foreach (string path in files)
            {
                TryDeleteFile(path);
            }
        });
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete temporary print PDF '{path}': {ex.Message}");
        }
    }
}
