using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;
using System.ComponentModel;
using System.Diagnostics;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class PrintSettingsDialog : ContentDialog
{
    private static readonly int[] CodesPerPageOptions = [1, 2, 4, 6, 9, 12];
    private const double PreviewPageWidth = 420;
    private static readonly TimeSpan DeferredCleanupDelay = TimeSpan.FromMinutes(10);
    private readonly IPrintService printService;
    private readonly IReadOnlyList<RequestedQrCodeItem> codes;
    private readonly QrRenderSettingsSnapshot renderSettings;
    private readonly List<string> generatedPdfPaths = [];
    private CancellationTokenSource? previewCts;
    private bool isLoaded;
    private string? currentPdfPath;

    public PrintSettingsDialog(
        IPrintService printService,
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot renderSettings,
        PrintJobSettings initialSettings)
    {
        InitializeComponent();
        this.printService = printService;
        this.codes = codes;
        this.renderSettings = renderSettings;

        int index = Array.IndexOf(CodesPerPageOptions, initialSettings.CodesPerPage);
        CodesPerPageCombo.SelectedIndex = index >= 0 ? index : 2;
        MarginMmBox.Value = initialSettings.MarginMm;
        ShowLabelsSwitch.IsOn = initialSettings.ShowLabels;

        IsPrimaryButtonEnabled = false;
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public PrintJobSettings ResultSettings => new()
    {
        CodesPerPage = CodesPerPageCombo.SelectedIndex >= 0
            ? CodesPerPageOptions[CodesPerPageCombo.SelectedIndex]
            : 4,
        MarginMm = double.IsNaN(MarginMmBox.Value) ? 10 : MarginMmBox.Value,
        ShowLabels = ShowLabelsSwitch.IsOn,
    };

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (isLoaded)
            return;

        isLoaded = true;

        try
        {
            await RefreshPreviewAsync();
        }
        catch (Exception ex)
        {
            ShowPreviewError($"Couldn't start the PDF preview. {ex.Message}");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        previewCts?.Cancel();
        previewCts?.Dispose();
        previewCts = null;

        ScheduleDeferredCleanup(generatedPdfPaths);
    }

    private async void CodesPerPageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshPreviewIfReadyAsync();

    private async void MarginMmBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => await RefreshPreviewIfReadyAsync();

    private async void ShowLabelsSwitch_Toggled(object sender, RoutedEventArgs e) => await RefreshPreviewIfReadyAsync();

    private async Task RefreshPreviewIfReadyAsync()
    {
        if (!isLoaded)
            return;

        await RefreshPreviewAsync();
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

        for (uint index = 0; index < pdfDocument.PageCount; index++)
        {
            token.ThrowIfCancellationRequested();

            using PdfPage page = pdfDocument.GetPage(index);
            using InMemoryRandomAccessStream renderedStream = new();

            PdfPageRenderOptions renderOptions = new()
            {
                DestinationWidth = (uint)PreviewPageWidth,
                DestinationHeight = (uint)Math.Round(PreviewPageWidth * (page.Size.Height / page.Size.Width)),
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
                Padding = new Thickness(8),
                Child = new Image
                {
                    Source = bitmap,
                    Width = PreviewPageWidth,
                    Stretch = Stretch.Uniform,
                },
            };

            previewPages.Add(pageBorder);
        }

        token.ThrowIfCancellationRequested();

        PreviewPagesPanel.Children.Clear();
        foreach (FrameworkElement page in previewPages)
        {
            PreviewPagesPanel.Children.Add(page);
        }

        PreviewScrollViewer.ChangeView(null, 0, null, true);
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
            ProcessStartInfo printStartInfo = new(currentPdfPath)
            {
                UseShellExecute = true,
                Verb = "print",
            };

            Process.Start(printStartInfo);
            Hide();
        }
        catch (Win32Exception)
        {
            try
            {
                ProcessStartInfo openStartInfo = new(currentPdfPath)
                {
                    UseShellExecute = true,
                };

                Process.Start(openStartInfo);
                Hide();
            }
            catch (Exception ex)
            {
                ShowPreviewError($"Couldn't open the generated PDF. {ex.Message}");
            }
        }
        catch (InvalidOperationException)
        {
            try
            {
                ProcessStartInfo openStartInfo = new(currentPdfPath)
                {
                    UseShellExecute = true,
                };

                Process.Start(openStartInfo);
                Hide();
            }
            catch (Exception ex)
            {
                ShowPreviewError($"Couldn't open the generated PDF. {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            ShowPreviewError($"Couldn't send the generated PDF to the native PDF app. {ex.Message}");
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
    }

    private void ShowPreviewError(string message)
    {
        IsPrimaryButtonEnabled = false;
        PreviewLoadingPanel.Visibility = Visibility.Collapsed;
        PreviewErrorTextBlock.Text = message;
        PreviewErrorPanel.Visibility = Visibility.Visible;
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
