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

    public string FileName => System.IO.Path.GetFileName(ImagePath);

    [ObservableProperty]
    private BitmapImage? bitmapImage;

    public BitmapImage? ProcessedBitmapImage { get; set; }

    public List<(string, Result)> Strings { get; set; } = new();

        [ObservableProperty]
        private ObservableCollection<TextBorder> codeBorders = new();

        [ObservableProperty]
        private bool isNoCodesWarningDismissed = false;

        public MagickImage? OriginalMagickImage { get; set; }

        public MagickImage? ProcessedMagickImage { get; set; }

        public bool HasProcessedImage => ProcessedMagickImage != null;

        [ObservableProperty]
        private ObservableCollection<UIElement> perspectiveCornerMarkers = new();

        [ObservableProperty]
        private int currentCornerIndex = 0;
    }
