using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Controls;
using System.Collections.ObjectModel;
using ZXing;

namespace Simple_QR_Code_Maker.Models;

public partial class DecodingImageItem : ObservableObject
{
    public string ImagePath { get; set; } = string.Empty;

    // Always the oriented PNG in the temp folder, used for history thumbnail saving
    public string CachedBitmapPath { get; set; } = string.Empty;

    public string FileName => System.IO.Path.GetFileName(ImagePath);

    [ObservableProperty]
    public partial BitmapImage? BitmapImage { get; set; }

    [ObservableProperty]
    public partial int ImagePixelWidth { get; set; }

    [ObservableProperty]
    public partial int ImagePixelHeight { get; set; }

    public BitmapImage? ProcessedBitmapImage { get; set; }

    public List<(string, Result)> Strings { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<TextBorder> CodeBorders { get; set; } = new();

    [ObservableProperty]
    public partial bool IsNoCodesWarningDismissed { get; set; } = false;

    public MagickImage? OriginalMagickImage { get; set; }

    public MagickImage? ProcessedMagickImage { get; set; }

    public bool HasProcessedImage => ProcessedMagickImage != null;

    [ObservableProperty]
    public partial ObservableCollection<UIElement> PerspectiveCornerMarkers { get; set; } = new();

    [ObservableProperty]
    public partial int CurrentCornerIndex { get; set; } = 0;
    }
