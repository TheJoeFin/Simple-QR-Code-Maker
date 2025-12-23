using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using System.Drawing;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class AdvancedToolsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsGrayscaleEnabled { get; set; } = false;

    [ObservableProperty]
    public partial double ContrastValue { get; set; } = 0.0;

    [ObservableProperty]
    public partial double BlackPointLevel { get; set; } = 0.0;

    [ObservableProperty]
    public partial double WhitePointLevel { get; set; } = 100.0;

    [ObservableProperty]
    public partial bool IsPerspectiveCorrectionMode { get; set; } = false;

    partial void OnIsPerspectiveCorrectionModeChanged(bool value)
    {
        if (value)
        {
            // Deactivate other tools when perspective correction is enabled
            IsEyedropperBlackMode = false;
            IsEyedropperWhiteMode = false;
            System.Diagnostics.Debug.WriteLine("Perspective Correction Mode activated - other tools deactivated");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Perspective Correction Mode deactivated");
        }
    }

    [ObservableProperty]
    public partial Point? TopLeftCorner { get; set; } = null;

    [ObservableProperty]
    public partial Point? TopRightCorner { get; set; } = null;

    [ObservableProperty]
    public partial Point? BottomRightCorner { get; set; } = null;

    [ObservableProperty]
    public partial Point? BottomLeftCorner { get; set; } = null;

    public string CurrentCornerInstruction
    {
        get
        {
            if (!IsPerspectiveCorrectionMode) return string.Empty;
            if (TopLeftCorner == null) return "1. Select Top-Left corner";
            if (TopRightCorner == null) return "2. Select Top-Right corner";
            if (BottomRightCorner == null) return "3. Select Bottom-Right corner";
            if (BottomLeftCorner == null) return "4. Select Bottom-Left corner";
            return "? All corners selected! Click 'Apply Changes' to process.";
        }
    }

    public int CurrentCornerNumber
    {
        get
        {
            if (TopLeftCorner == null) return 1;
            if (TopRightCorner == null) return 2;
            if (BottomRightCorner == null) return 3;
            if (BottomLeftCorner == null) return 4;
            return 0; // All complete
        }
    }

    public bool IsCornerSelectionComplete => CurrentCornerNumber == 0;

    [ObservableProperty]
    public partial int BorderPixels { get; set; } = 20;

    [ObservableProperty]
    public partial MagickColor? SelectedBlackPointColor { get; set; } = null;

    [ObservableProperty]
    public partial MagickColor? SelectedWhitePointColor { get; set; } = null;

    [ObservableProperty]
    public partial bool IsEyedropperBlackMode { get; set; } = false;

    [ObservableProperty]
    public partial bool IsEyedropperWhiteMode { get; set; } = false;

    [ObservableProperty]
    public partial bool IsProcessing { get; set; } = false;

    private MagickImage? originalImage = null;
    private MagickImage? processedImage = null;

    public event EventHandler<MagickImage>? ImageProcessed;
    public event EventHandler? PerspectiveCornersClearedRequested;

    public void SetOriginalImage(MagickImage image)
    {
        // Ensure EXIF orientation is applied to avoid coordinate misalignment
        var orientedImage = (MagickImage)image.Clone();
        orientedImage.AutoOrient();

        originalImage = orientedImage;
        processedImage = (MagickImage)orientedImage.Clone();

        System.Diagnostics.Debug.WriteLine($"AdvancedToolsViewModel: Set original image");
        System.Diagnostics.Debug.WriteLine($"  Dimensions: {orientedImage.Width}x{orientedImage.Height}");
        System.Diagnostics.Debug.WriteLine($"  AutoOrient applied to ensure correct rotation");
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

        // Notify property changes for computed properties
        OnPropertyChanged(nameof(CurrentCornerInstruction));
        OnPropertyChanged(nameof(CurrentCornerNumber));
        OnPropertyChanged(nameof(IsCornerSelectionComplete));

        // Safely invoke events
        try
        {
            PerspectiveCornersClearedRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing perspective corners: {ex.Message}");
        }

        if (originalImage != null)
        {
            try
            {
                processedImage = (MagickImage)originalImage.Clone();
                ImageProcessed?.Invoke(this, processedImage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing image on reset: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ApplyProcessingAsync()
    {
        if (originalImage == null)
            return;

        IsProcessing = true;

        try
        {
            // Perform image processing on a background thread
            await Task.Run(() =>
            {
                MagickImage tempProcessedImage = (MagickImage)originalImage.Clone();

                if (IsGrayscaleEnabled)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.ApplyGrayscale(tempProcessedImage);
                }

                if (Math.Abs(ContrastValue) > 0.01)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.AdjustContrast(tempProcessedImage, ContrastValue);
                }

                if (SelectedBlackPointColor != null)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.SetBlackPoint(tempProcessedImage, SelectedBlackPointColor);
                }

                if (SelectedWhitePointColor != null)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.SetWhitePoint(tempProcessedImage, SelectedWhitePointColor);
                }

                if (Math.Abs(BlackPointLevel) > 0.01 || Math.Abs(WhitePointLevel - 100.0) > 0.01)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.AdjustLevels(tempProcessedImage, BlackPointLevel, WhitePointLevel);
                }

                if (IsPerspectiveCorrectionMode && AllCornersSet())
                {
                    try
                    {
                        tempProcessedImage = Helpers.ImageProcessingHelper.CorrectPerspectiveDistortion(
                            tempProcessedImage,
                            TopLeftCorner!.Value,
                            TopRightCorner!.Value,
                            BottomRightCorner!.Value,
                            BottomLeftCorner!.Value,
                            BorderPixels);
                    }
                    catch (ArgumentException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Perspective correction validation failed: {ex.Message}");
                        throw new InvalidOperationException($"Perspective correction failed: {ex.Message}", ex);
                    }
                    catch (InvalidOperationException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Perspective correction failed: {ex.Message}");
                        throw;
                    }
                }

                processedImage = tempProcessedImage;
            });

            // Raise event on UI thread
            ImageProcessed?.Invoke(this, processedImage);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void ToggleEyedropperBlackMode()
    {
        IsEyedropperBlackMode = !IsEyedropperBlackMode;

        if (IsEyedropperBlackMode)
        {
            // Deactivate other tools
            IsEyedropperWhiteMode = false;
            if (IsPerspectiveCorrectionMode)
            {
                IsPerspectiveCorrectionMode = false;
                System.Diagnostics.Debug.WriteLine("Black Point Eyedropper activated - Perspective Correction deactivated");
            }
        }
    }

    [RelayCommand]
    private void ToggleEyedropperWhiteMode()
    {
        IsEyedropperWhiteMode = !IsEyedropperWhiteMode;

        if (IsEyedropperWhiteMode)
        {
            // Deactivate other tools
            IsEyedropperBlackMode = false;
            if (IsPerspectiveCorrectionMode)
            {
                IsPerspectiveCorrectionMode = false;
                System.Diagnostics.Debug.WriteLine("White Point Eyedropper activated - Perspective Correction deactivated");
            }
        }
    }

    [RelayCommand]
    private void ClearPerspectiveCorners()
    {
        TopLeftCorner = null;
        TopRightCorner = null;
        BottomRightCorner = null;
        BottomLeftCorner = null;

        OnPropertyChanged(nameof(CurrentCornerInstruction));
        OnPropertyChanged(nameof(CurrentCornerNumber));
        OnPropertyChanged(nameof(IsCornerSelectionComplete));

        PerspectiveCornersClearedRequested?.Invoke(this, EventArgs.Empty);
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

        // Notify UI about instruction changes
        OnPropertyChanged(nameof(CurrentCornerInstruction));
        OnPropertyChanged(nameof(CurrentCornerNumber));
        OnPropertyChanged(nameof(IsCornerSelectionComplete));

        // Don't auto-apply processing - let user click "Apply Changes" button
        // This prevents UI blocking when selecting the 4th corner
    }

    public async void SetColorFromPoint(Point point)
    {
        // Use original image for color sampling
        MagickImage? imageToSample = originalImage ?? processedImage;

        if (imageToSample == null)
            return;

        if (point.X < 0 || point.X >= imageToSample.Width || point.Y < 0 || point.Y >= imageToSample.Height)
            return;

        IPixelCollection<ushort> pixels = imageToSample.GetPixels();
        IPixel<ushort> pixel = pixels.GetPixel(point.X, point.Y);
        IMagickColor<ushort>? color = pixel.ToColor();

        if (color is null)
            return;

        if (IsEyedropperBlackMode)
        {
            SelectedBlackPointColor = new MagickColor(color.R, color.G, color.B, color.A);
            IsEyedropperBlackMode = false;
            await ApplyProcessingAsync();
        }
        else if (IsEyedropperWhiteMode)
        {
            SelectedWhitePointColor = new MagickColor(color.R, color.G, color.B, color.A);
            IsEyedropperWhiteMode = false;
            await ApplyProcessingAsync();
        }
    }

    private bool AllCornersSet()
    {
        return TopLeftCorner.HasValue && TopRightCorner.HasValue &&
               BottomRightCorner.HasValue && BottomLeftCorner.HasValue;
    }

    public MagickImage? GetProcessedImage() => processedImage;
}
