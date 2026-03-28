using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker;
using Simple_QR_Code_Maker.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Simple_QR_Code_Maker.Controls;

[ObservableObject]
public sealed partial class BrandEditDialog : ContentDialog
{
    private readonly BrandItem _original;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNameValid))]
    private string editName = string.Empty;

    [ObservableProperty] private bool includeForeground;
    [ObservableProperty] private Windows.UI.Color foregroundColor;

    [ObservableProperty] private bool includeBackground;
    [ObservableProperty] private Windows.UI.Color backgroundColor;

    [ObservableProperty] private bool includeUrl;
    [ObservableProperty] private string urlContent = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogoPath))]
    private bool includeLogo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogoPath))]
    private string? logoPath;

    [ObservableProperty] private double logoSize;
    [ObservableProperty] private double logoPadding;

    public bool IsNameValid => !string.IsNullOrWhiteSpace(EditName);
    public bool HasLogoPath => IncludeLogo && LogoPath is not null;

    public BrandItem? EditedItem { get; private set; }

    public BrandEditDialog(BrandItem original)
    {
        _original = original;
        EditName = original.Name;

        IncludeForeground = original.Foreground.HasValue;
        ForegroundColor = original.Foreground ?? Windows.UI.Color.FromArgb(255, 0, 0, 0);

        IncludeBackground = original.Background.HasValue;
        BackgroundColor = original.Background ?? Windows.UI.Color.FromArgb(255, 255, 255, 255);

        IncludeUrl = original.UrlContent is not null;
        UrlContent = original.UrlContent ?? string.Empty;

        IncludeLogo = original.LogoImagePath is not null;
        LogoPath = original.LogoImagePath;
        LogoSize = original.LogoSizePercentage ?? 15;
        LogoPadding = original.LogoPaddingPixels ?? 4;

        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    [RelayCommand]
    private async Task SelectLogo()
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".svg");

        IntPtr windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, windowHandle);

        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
            LogoPath = file.Path;
    }

    [RelayCommand]
    private void ClearLogo()
    {
        LogoPath = null;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        EditedItem = new BrandItem
        {
            Name = EditName.Trim(),
            CreatedDateTime = _original.CreatedDateTime,
            IsDefault = _original.IsDefault,
            Foreground = IncludeForeground ? ForegroundColor : null,
            Background = IncludeBackground ? BackgroundColor : null,
            UrlContent = IncludeUrl && !string.IsNullOrWhiteSpace(UrlContent) ? UrlContent.Trim() : null,
            ErrorCorrectionLevelAsString = _original.ErrorCorrectionLevelAsString,
            LogoImagePath = IncludeLogo ? LogoPath : null,
            LogoSizePercentage = IncludeLogo ? LogoSize : null,
            LogoPaddingPixels = IncludeLogo ? LogoPadding : null,
        };
    }
}
