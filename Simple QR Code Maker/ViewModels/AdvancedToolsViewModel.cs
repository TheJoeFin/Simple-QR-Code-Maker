using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using System.Drawing;
using Windows.Storage;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class AdvancedToolsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isGrayscaleEnabled = false;

    [ObservableProperty]
    private double contrastValue = 0.0;

    [ObservableProperty]
    private double blackPointLevel = 0.0;

    [ObservableProperty]
    private double whitePointLevel = 100.0;

    [ObservableProperty]
    private bool isPerspectiveCorrectionMode = false;

    [ObservableProperty]
    private Point? topLeftCorner = null;

    [ObservableProperty]
    private Point? topRightCorner = null;

    [ObservableProperty]
    private Point? bottomRightCorner = null;

    [ObservableProperty]
    private Point? bottomLeftCorner = null;

    [ObservableProperty]
    private int borderPixels = 20;

    [ObservableProperty]
    private MagickColor? selectedBlackPointColor = null;

    [ObservableProperty]
    private MagickColor? selectedWhitePointColor = null;

    [ObservableProperty]
    private bool isEyedropperBlackMode = false;

    [ObservableProperty]
    private bool isEyedropperWhiteMode = false;

    private MagickImage? originalImage = null;
    private MagickImage? processedImage = null;

    public event EventHandler<MagickImage>? ImageProcessed;

    public void SetOriginalImage(MagickImage image)
    {
        originalImage = (MagickImage)image.Clone();
        processedImage = (MagickImage)image.Clone();
    }

    [RelayCommand]
    private void ResetAllSettings()
    {
        IsGrayscaleEnabled = false;
        ContrastValue = 0.0;
        BlackPointLevel = 0.0;
        WhitePointLevel = 100.0;
        IsPerspectiveCorrectionMode = false;
        TopLeftCorner = null;
        TopRightCorner = null;
        BottomRightCorner = null;
        BottomLeftCorner = null;
        BorderPixels = 20;
        SelectedBlackPointColor = null;
        SelectedWhitePointColor = null;
        IsEyedropperBlackMode = false;
        IsEyedropperWhiteMode = false;

        if (originalImage != null)
        {
            processedImage = (MagickImage)originalImage.Clone();
            ImageProcessed?.Invoke(this, processedImage);
        }
    }

    [RelayCommand]
    private void ApplyProcessing()
    {
        if (originalImage == null)
            return;

        processedImage = (MagickImage)originalImage.Clone();

        if (IsGrayscaleEnabled)
        {
            processedImage = Helpers.ImageProcessingHelper.ApplyGrayscale(processedImage);
        }

        if (Math.Abs(ContrastValue) > 0.01)
        {
            processedImage = Helpers.ImageProcessingHelper.AdjustContrast(processedImage, ContrastValue);
        }

        if (SelectedBlackPointColor != null)
        {
            processedImage = Helpers.ImageProcessingHelper.SetBlackPoint(processedImage, SelectedBlackPointColor);
        }

        if (SelectedWhitePointColor != null)
        {
            processedImage = Helpers.ImageProcessingHelper.SetWhitePoint(processedImage, SelectedWhitePointColor);
        }

        if (Math.Abs(BlackPointLevel) > 0.01 || Math.Abs(WhitePointLevel - 100.0) > 0.01)
        {
            processedImage = Helpers.ImageProcessingHelper.AdjustLevels(processedImage, BlackPointLevel, WhitePointLevel);
        }

        if (IsPerspectiveCorrectionMode && AllCornersSet())
        {
            processedImage = Helpers.ImageProcessingHelper.CorrectPerspectiveDistortion(
                processedImage,
                TopLeftCorner!.Value,
                TopRightCorner!.Value,
                BottomRightCorner!.Value,
                BottomLeftCorner!.Value,
                BorderPixels);
        }

        ImageProcessed?.Invoke(this, processedImage);
    }

    [RelayCommand]
    private void ToggleEyedropperBlackMode()
    {
        IsEyedropperBlackMode = !IsEyedropperBlackMode;
        if (IsEyedropperBlackMode)
            IsEyedropperWhiteMode = false;
    }

    [RelayCommand]
    private void ToggleEyedropperWhiteMode()
    {
        IsEyedropperWhiteMode = !IsEyedropperWhiteMode;
        if (IsEyedropperWhiteMode)
            IsEyedropperBlackMode = false;
    }

    [RelayCommand]
    private void ClearPerspectiveCorners()
    {
        TopLeftCorner = null;
        TopRightCorner = null;
        BottomRightCorner = null;
        BottomLeftCorner = null;
    }

    public void SetCornerPoint(Point point, int cornerIndex)
    {
        switch (cornerIndex)
        {
            case 0:
                TopLeftCorner = point;
                break;
            case 1:
                TopRightCorner = point;
                break;
            case 2:
                BottomRightCorner = point;
                break;
            case 3:
                BottomLeftCorner = point;
                break;
        }

        if (AllCornersSet())
        {
            ApplyProcessing();
        }
    }

    public void SetColorFromPoint(Point point)
    {
        if (processedImage == null)
            return;

        if (point.X < 0 || point.X >= processedImage.Width || point.Y < 0 || point.Y >= processedImage.Height)
            return;

        var pixels = processedImage.GetPixels();
        var pixel = pixels.GetPixel(point.X, point.Y);
        var color = pixel.ToColor();

        if (IsEyedropperBlackMode)
        {
            SelectedBlackPointColor = new MagickColor(color.R, color.G, color.B, color.A);
            IsEyedropperBlackMode = false;
        }
        else if (IsEyedropperWhiteMode)
        {
            SelectedWhitePointColor = new MagickColor(color.R, color.G, color.B, color.A);
            IsEyedropperWhiteMode = false;
        }

        ApplyProcessing();
    }

    private bool AllCornersSet()
    {
        return TopLeftCorner.HasValue && TopRightCorner.HasValue &&
               BottomRightCorner.HasValue && BottomLeftCorner.HasValue;
    }

    public MagickImage? GetProcessedImage()
    {
        return processedImage;
    }
}
