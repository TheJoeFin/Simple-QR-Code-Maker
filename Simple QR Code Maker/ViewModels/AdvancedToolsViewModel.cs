using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using System.Drawing;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class AdvancedToolsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial bool IsGrayscaleEnabled { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial bool IsInvertEnabled { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial double ContrastValue { get; set; } = 0.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial double BlackPointLevel { get; set; } = 0.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial double WhitePointLevel { get; set; } = 100.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    [NotifyPropertyChangedFor(nameof(IsAnyToolActive))]
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
    [NotifyPropertyChangedFor(nameof(CurrentCornerInstruction))]
    [NotifyPropertyChangedFor(nameof(CurrentCornerNumber))]
    [NotifyPropertyChangedFor(nameof(IsCornerSelectionComplete))]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial Point? TopLeftCorner { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCornerInstruction))]
    [NotifyPropertyChangedFor(nameof(CurrentCornerNumber))]
    [NotifyPropertyChangedFor(nameof(IsCornerSelectionComplete))]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial Point? TopRightCorner { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCornerInstruction))]
    [NotifyPropertyChangedFor(nameof(CurrentCornerNumber))]
    [NotifyPropertyChangedFor(nameof(IsCornerSelectionComplete))]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial Point? BottomRightCorner { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCornerInstruction))]
    [NotifyPropertyChangedFor(nameof(CurrentCornerNumber))]
    [NotifyPropertyChangedFor(nameof(IsCornerSelectionComplete))]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
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
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial MagickColor? SelectedBlackPointColor { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial MagickColor? SelectedWhitePointColor { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyToolActive))]
    public partial bool IsEyedropperBlackMode { get; set; } = false;

    partial void OnIsEyedropperBlackModeChanged(bool value)
    {
        if (value)
        {
            // Deactivate other tools
            IsEyedropperWhiteMode = false;
            if (IsPerspectiveCorrectionMode)
            {
                IsPerspectiveCorrectionMode = false;
                System.Diagnostics.Debug.WriteLine("Black Point Eyedropper activated - other tools deactivated");
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyToolActive))]
    public partial bool IsEyedropperWhiteMode { get; set; } = false;

    partial void OnIsEyedropperWhiteModeChanged(bool value)
    {
        if (value)
        {
            // Deactivate other tools
            IsEyedropperBlackMode = false;
            if (IsPerspectiveCorrectionMode)
            {
                IsPerspectiveCorrectionMode = false;
                System.Diagnostics.Debug.WriteLine("White Point Eyedropper activated - other tools deactivated");
            }
        }
    }

    [ObservableProperty]
    public partial bool IsProcessing { get; set; } = false;

    // Track if there are pending changes that need to be applied
    public bool HasPendingChanges
    {
        get
        {
            // Check if any setting has been changed from default
            bool hasBasicChanges = IsGrayscaleEnabled || IsInvertEnabled || Math.Abs(ContrastValue) > 0.01;
            bool hasLevelChanges = Math.Abs(BlackPointLevel) > 0.01 || Math.Abs(WhitePointLevel - 100.0) > 0.01;
            bool hasEyedropperChanges = SelectedBlackPointColor != null || SelectedWhitePointColor != null;
            bool hasPerspectiveChanges = IsPerspectiveCorrectionMode && AllCornersSet();

            return hasBasicChanges || hasLevelChanges || hasEyedropperChanges || hasPerspectiveChanges;
        }
    }

    // Track if any interactive tool is currently active
    public bool IsAnyToolActive => IsEyedropperBlackMode || IsEyedropperWhiteMode || IsPerspectiveCorrectionMode;

    /// <summary>
    /// The unmodified original image. Only used by "Reset All" to restore the baseline.
    /// </summary>
    private MagickImage? trueOriginalImage = null;

    /// <summary>
    /// The cumulative baseline. Each successful Apply advances this to include all
    /// previously-applied changes. New pending changes are applied on top of this.
    /// </summary>
    private MagickImage? baselineImage = null;

    private MagickImage? processedImage = null;

    public event EventHandler<MagickImage>? ImageProcessed;
    public event EventHandler? PerspectiveCornersClearedRequested;

    public void SetOriginalImage(MagickImage image)
    {
        // Ensure EXIF orientation is applied to avoid coordinate misalignment
        var orientedImage = (MagickImage)image.Clone();
        orientedImage.AutoOrient();

        trueOriginalImage = orientedImage;
        baselineImage = (MagickImage)orientedImage.Clone();
        processedImage = (MagickImage)orientedImage.Clone();

        System.Diagnostics.Debug.WriteLine($"AdvancedToolsViewModel: Set original image");
        System.Diagnostics.Debug.WriteLine($"  Dimensions: {orientedImage.Width}x{orientedImage.Height}");
        System.Diagnostics.Debug.WriteLine($"  AutoOrient applied to ensure correct rotation");
    }

    [RelayCommand]
    private void ResetAllSettings()
    {
        IsGrayscaleEnabled = false;
        IsInvertEnabled = false;
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
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(IsAnyToolActive));

        // Safely invoke events
        try
        {
            PerspectiveCornersClearedRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing perspective corners: {ex.Message}");
        }

        if (trueOriginalImage != null)
        {
            try
            {
                // Reset All reverts the baseline back to the true original
                baselineImage = (MagickImage)trueOriginalImage.Clone();
                processedImage = (MagickImage)trueOriginalImage.Clone();
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
        if (baselineImage == null)
            return;

        IsProcessing = true;

        try
        {
            // Capture current settings before they are reset
            bool grayscale = IsGrayscaleEnabled;
            bool invert = IsInvertEnabled;
            double contrast = ContrastValue;
            MagickColor? blackColor = SelectedBlackPointColor;
            MagickColor? whiteColor = SelectedWhitePointColor;
            double blackLevel = BlackPointLevel;
            double whiteLevel = WhitePointLevel;
            bool doPerspective = IsPerspectiveCorrectionMode && AllCornersSet();
            Point? tl = TopLeftCorner;
            Point? tr = TopRightCorner;
            Point? br = BottomRightCorner;
            Point? bl = BottomLeftCorner;
            int border = BorderPixels;

            // Perform image processing on a background thread.
            // Apply only the current pending changes on top of the baseline.
            await Task.Run(() =>
            {
                MagickImage tempProcessedImage = (MagickImage)baselineImage.Clone();

                if (grayscale)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.ApplyGrayscale(tempProcessedImage);
                }

                if (invert)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.InvertColors(tempProcessedImage);
                }

                if (Math.Abs(contrast) > 0.01)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.AdjustContrast(tempProcessedImage, contrast);
                }

                if (blackColor != null)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.SetBlackPoint(tempProcessedImage, blackColor);
                }

                if (whiteColor != null)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.SetWhitePoint(tempProcessedImage, whiteColor);
                }

                if (Math.Abs(blackLevel) > 0.01 || Math.Abs(whiteLevel - 100.0) > 0.01)
                {
                    tempProcessedImage = Helpers.ImageProcessingHelper.AdjustLevels(tempProcessedImage, blackLevel, whiteLevel);
                }

                if (doPerspective)
                {
                    try
                    {
                        tempProcessedImage = Helpers.ImageProcessingHelper.CorrectPerspectiveDistortion(
                            tempProcessedImage,
                            tl!.Value,
                            tr!.Value,
                            br!.Value,
                            bl!.Value,
                            border);
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

            // Advance the baseline so the next Apply builds on this result
            baselineImage = (MagickImage)processedImage!.Clone();

            // Reset the settings that were just applied so they don't stack
            ResetPendingSettings();

            // Raise event on UI thread
            ImageProcessed?.Invoke(this, processedImage);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Resets all tool settings back to defaults after a successful apply,
    /// so they are not re-applied on the next Apply click.
    /// </summary>
    private void ResetPendingSettings()
    {
        IsGrayscaleEnabled = false;
        IsInvertEnabled = false;
        ContrastValue = 0.0;
        BlackPointLevel = 0.0;
        WhitePointLevel = 100.0;
        SelectedBlackPointColor = null;
        SelectedWhitePointColor = null;

        // Perspective corners and mode are cleared via the ImageProcessed
        // handler in DecodingPage.xaml.cs, so we don't reset them here
        // to avoid double-clearing.
    }

    [RelayCommand]
    private void ClearPerspectiveCorners()
    {
        TopLeftCorner = null;
        TopRightCorner = null;
        BottomRightCorner = null;
        BottomLeftCorner = null;

        // Property changes are now handled by NotifyPropertyChangedFor attributes
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

        // Property changes are now handled by NotifyPropertyChangedFor attributes
        // Don't auto-apply processing - let user click "Apply Changes" button
        // This prevents UI blocking when selecting the 4th corner
    }

    public async void SetColorFromPoint(Point point)
    {
        // Use baseline image for color sampling (reflects cumulative changes)
        MagickImage? imageToSample = baselineImage ?? processedImage;

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
        }
        else if (IsEyedropperWhiteMode)
        {
            SelectedWhitePointColor = new MagickColor(color.R, color.G, color.B, color.A);
            IsEyedropperWhiteMode = false;
        }
    }

    private bool AllCornersSet()
    {
        return TopLeftCorner.HasValue && TopRightCorner.HasValue &&
               BottomRightCorner.HasValue && BottomLeftCorner.HasValue;
    }

    public MagickImage? GetProcessedImage() => processedImage;

    /// <summary>
    /// Clears all state including internal images. Used when the current image is
    /// removed (e.g. the user clicks Clear) so that stale settings and images do
    /// not carry over to the next image.
    /// </summary>
    public void ClearAll()
    {
        IsGrayscaleEnabled = false;
        IsInvertEnabled = false;
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
        IsProcessing = false;

        trueOriginalImage = null;
        baselineImage = null;
        processedImage = null;

        OnPropertyChanged(nameof(CurrentCornerInstruction));
        OnPropertyChanged(nameof(CurrentCornerNumber));
        OnPropertyChanged(nameof(IsCornerSelectionComplete));
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(IsAnyToolActive));
    }
}
