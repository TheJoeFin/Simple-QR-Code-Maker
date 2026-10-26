using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Controls;

public sealed class QrDesignGalleryItem
{
    public QrDesignGalleryItem(string name, string description, ImageSource thumbnail, QrDesignPreset? design, QrFramePreset frame)
    {
        Name = name;
        Description = description;
        Thumbnail = thumbnail;
        Design = design;
        Frame = frame;
    }

    public string Name { get; }
    public string Description { get; }
    public ImageSource Thumbnail { get; }
    public QrDesignPreset? Design { get; }
    public QrFramePreset Frame { get; }
}

public sealed partial class QrDesignDialog : ContentDialog
{
    private bool updatingSelection;
    private bool loadingPreviews;
    private ObservableCollection<BarcodeImageItem>? previewItems;
    private XamlRoot? dialogRoot;

    public ObservableCollection<QrDesignGalleryItem> Presets { get; } = [];
    public ObservableCollection<QrDesignGalleryItem> Frames { get; } = [];

    public MainViewModel ViewModel { get; }

    public QrDesignDialog(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ModuleCombo.ItemsSource = Enum.GetValues<QrModuleStyle>();
        EyeCombo.ItemsSource = Enum.GetValues<QrEyeStyle>();
        IconCombo.ItemsSource = Enum.GetValues<QrCaptionIcon>();
        LabelSourceCombo.ItemsSource = new[] { "Manual text", "Content summary" };
        CaptionSuggestionCombo.ItemsSource = new[] { "Scan Me", "Join Wi-Fi", "Save my contact", "View the menu", "Get tickets", "Say hello!" };
    }

    private async void DesignDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        DetachViewModel();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        dialogRoot = XamlRoot;
        dialogRoot.Changed += DialogRoot_Changed;
        UpdateDialogLayout();
        AttachPreviewCollection();
        UpdateSelection();
        if (Presets.Count > 0 || loadingPreviews)
            return;

        loadingPreviews = true;
        PreviewProgress.IsActive = true;
        PreviewProgress.Visibility = Visibility.Visible;
        PreviewError.IsOpen = false;
        try
        {
            List<(QrDesignPreset? Design, QrFramePresetOption? Frame, byte[] Png)> previews = await Task.Run(BuildPreviews);
            foreach ((QrDesignPreset? design, QrFramePresetOption? frame, byte[] png) in previews)
            {
                using MemoryStream stream = new(png);
                BitmapImage image = new();
                await image.SetSourceAsync(stream.AsRandomAccessStream());
                QrDesignGalleryItem item;
                if (design is not null)
                {
                    item = new(design.Name, design.Description, image, design, design.Frame);
                    Presets.Add(item);
                }
                else if (frame is QrFramePresetOption frameOption)
                {
                    item = new(frameOption.Preset == QrFramePreset.None ? "No frame" : frameOption.ShortDescription,
                        frameOption.Description, image, null, frameOption.Preset);
                    Frames.Add(item);
                }
                else
                {
                    throw new InvalidOperationException("The design preview has no associated preset or frame.");
                }
            }
            UpdateSelection();
        }
        catch (Exception exception)
        {
            Presets.Clear();
            Frames.Clear();
            Debug.WriteLine($"Unable to generate QR design previews: {exception}");
            PreviewError.Message = "Close and reopen Design to try loading previews again.";
            PreviewError.IsOpen = true;
        }
        finally
        {
            loadingPreviews = false;
            PreviewProgress.IsActive = false;
            PreviewProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateDialogLayout()
    {
        DialogLayout.Width = Math.Max(240, Math.Min(760, XamlRoot.Size.Width - 96));
        DialogLayout.Height = Math.Max(180, Math.Min(620, XamlRoot.Size.Height - 200));
        bool sideBySide = DialogLayout.Width >= 620;
        DialogLayout.ColumnSpacing = sideBySide ? 24 : 0;
        PreviewColumn.Width = new GridLength(sideBySide ? 240 : 0);
        Grid.SetColumn(PreviewPane, sideBySide ? 1 : 0);
        Grid.SetRowSpan(PreviewPane, sideBySide ? 2 : 1);
        Grid.SetRow(DesignScroller, sideBySide ? 0 : 1);
        Grid.SetRowSpan(DesignScroller, sideBySide ? 2 : 1);
        LivePreviewImage.MaxHeight = sideBySide ? Math.Min(500, Math.Max(80, DialogLayout.Height - 140)) : 120;
        PreviewHelpText.Visibility = sideBySide ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DialogRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateDialogLayout();

    private void AttachPreviewCollection()
    {
        if (previewItems is not null)
            previewItems.CollectionChanged -= PreviewItems_CollectionChanged;
        previewItems = ViewModel.QrCodeBitmaps;
        previewItems.CollectionChanged += PreviewItems_CollectionChanged;
        UpdateLivePreview();
    }

    private void PreviewItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => UpdateLivePreview();

    private void UpdateLivePreview()
    {
        LivePreviewImage.Source = previewItems?.FirstOrDefault()?.CodeAsBitmap;
        NoPreviewText.Visibility = LivePreviewImage.Source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static List<(QrDesignPreset? Design, QrFramePresetOption? Frame, byte[] Png)> BuildPreviews()
    {
        List<(QrDesignPreset?, QrFramePresetOption?, byte[])> previews = [];
        foreach (QrDesignPreset design in QrDesignPresetCatalog.All)
            previews.Add((design, null, RenderThumbnail(design.Frame, design.Caption, design.Appearance, design.Foreground, design.Background)));

        foreach (QrFramePresetOption frame in QrFramePresetCatalog.All)
            previews.Add((null, frame, RenderThumbnail(frame.Preset, frame.DefaultText,
                new() { FrameAccent = Windows.UI.Color.FromArgb(255, 137, 112, 211) },
                Windows.UI.Color.FromArgb(255, 30, 25, 45), Windows.UI.Color.FromArgb(255, 255, 255, 255))));

        return previews;
    }

    private static byte[] RenderThumbnail(QrFramePreset frame, string caption, QrCodeAppearance appearance, Windows.UI.Color foreground, Windows.UI.Color background)
    {
        using System.Drawing.Bitmap fullSize = QrCodeRenderer.GetQrCodeBitmapFromText(
            "https://example.com", ErrorCorrectionLevel.M,
            System.Drawing.Color.FromArgb(foreground.A, foreground.R, foreground.G, foreground.B),
            System.Drawing.Color.FromArgb(background.A, background.R, background.G, background.B),
            qrPaddingModules: 4, framePreset: frame, frameText: caption, appearance: appearance);
        double scale = Math.Min(208.0 / fullSize.Width, 224.0 / fullSize.Height);
        using System.Drawing.Bitmap thumbnail = new(Math.Max(1, (int)(fullSize.Width * scale)), Math.Max(1, (int)(fullSize.Height * scale)));
        using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(thumbnail))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(fullSize, 0, 0, thumbnail.Width, thumbnail.Height);
        }
        using MemoryStream stream = new();
        thumbnail.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private void UpdateSelection()
    {
        MainViewModel viewModel = ViewModel;

        updatingSelection = true;
        try
        {
            ModuleCombo.SelectedItem = viewModel.ModuleStyle;
            EyeCombo.SelectedItem = viewModel.EyeStyle;
            IconCombo.SelectedItem = viewModel.CaptionIcon;
            LabelSourceCombo.SelectedIndex = (int)viewModel.FrameTextSource;
            FrameGrid.SelectedItem = Frames.FirstOrDefault(item => item.Frame == viewModel.FramePreset);
            PresetGrid.SelectedItem = Presets.FirstOrDefault(item => item.Design is QrDesignPreset design
                && design.Frame == viewModel.FramePreset && design.Appearance == viewModel.Appearance
                && design.Foreground == viewModel.ForegroundColor && design.Background == viewModel.BackgroundColor
                && design.Caption == viewModel.FrameText && viewModel.FrameTextSource == QrFrameTextSource.Manual
                && viewModel.QrPaddingModules == 4);
        }
        finally
        {
            updatingSelection = false;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainViewModel.QrCodeBitmaps))
            AttachPreviewCollection();
        if (args.PropertyName is nameof(MainViewModel.Appearance) or nameof(MainViewModel.FramePreset)
            or nameof(MainViewModel.ForegroundColor) or nameof(MainViewModel.BackgroundColor)
            or nameof(MainViewModel.FrameText) or nameof(MainViewModel.FrameTextSource)
            or nameof(MainViewModel.QrPaddingModules))
            UpdateSelection();
    }

    private void PresetGrid_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!updatingSelection && PresetGrid.SelectedItem is QrDesignGalleryItem { Design: QrDesignPreset design })
            ViewModel.ApplyDesignPresetCommand.Execute(design);
    }

    private void FrameGrid_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!updatingSelection && FrameGrid.SelectedItem is QrDesignGalleryItem item)
            ViewModel.SelectFramePresetCommand.Execute(QrFramePresetCatalog.GetOption(item.Frame));
    }

    private void ModuleCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!updatingSelection && ModuleCombo.SelectedItem is QrModuleStyle style)
            ViewModel.ModuleStyle = style;
    }

    private void EyeCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!updatingSelection && EyeCombo.SelectedItem is QrEyeStyle style)
            ViewModel.EyeStyle = style;
    }

    private void IconCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!updatingSelection && IconCombo.SelectedItem is QrCaptionIcon icon)
            ViewModel.CaptionIcon = icon;
    }

    private void LabelSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!updatingSelection && LabelSourceCombo.SelectedIndex >= 0)
            ViewModel.FrameTextSource = (QrFrameTextSource)LabelSourceCombo.SelectedIndex;
    }

    private void CaptionSuggestionCombo_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (CaptionSuggestionCombo.SelectedItem is string caption)
        {
            ViewModel.FrameTextSource = QrFrameTextSource.Manual;
            ViewModel.FrameText = caption;
            CaptionSuggestionCombo.SelectedIndex = -1;
        }
    }

    private void ClassicPattern_Click(object sender, RoutedEventArgs args)
    {
        ViewModel.Appearance = ViewModel.Appearance with { ModuleStyle = QrModuleStyle.Square, EyeStyle = QrEyeStyle.Square };
    }

    private void DesignSections_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (PresetGrid is null)
            return;

        PresetGrid.Visibility = sender.SelectedItem == PresetsSection ? Visibility.Visible : Visibility.Collapsed;
        PatternPanel.Visibility = sender.SelectedItem == PatternSection ? Visibility.Visible : Visibility.Collapsed;
        FramePanel.Visibility = sender.SelectedItem == FrameSection ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Gallery_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is QrDesignGalleryItem item)
        {
            AutomationProperties.SetName(args.ItemContainer, item.Name);
            AutomationProperties.SetAutomationId(args.ItemContainer,
                item.Design is not null ? $"DesignPreset_{item.Name.Replace(" ", "")}" : $"Frame_{item.Frame}");
        }
    }

    private void DetachViewModel()
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        if (previewItems is not null)
            previewItems.CollectionChanged -= PreviewItems_CollectionChanged;
        previewItems = null;
        if (dialogRoot is not null)
            dialogRoot.Changed -= DialogRoot_Changed;
        dialogRoot = null;
    }

    private void DesignDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args) => DetachViewModel();
}
