using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using Simple_QR_Code_Maker.Models;
using System.Drawing;

namespace Simple_QR_Code_Maker.ViewModels;

public partial class AdvancedToolsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkflowPickerVisible))]
    [NotifyPropertyChangedFor(nameof(IsSampleBlackPointWorkflowVisible))]
    [NotifyPropertyChangedFor(nameof(IsSampleWhitePointWorkflowVisible))]
    [NotifyPropertyChangedFor(nameof(IsPerspectiveWorkflowVisible))]
    [NotifyPropertyChangedFor(nameof(IsCutOutWorkflowVisible))]
    public partial AdvancedToolWorkflow ActiveWorkflow { get; set; } = AdvancedToolWorkflow.None;

    public bool IsWorkflowPickerVisible => ActiveWorkflow == AdvancedToolWorkflow.None;
    public bool IsSampleBlackPointWorkflowVisible => ActiveWorkflow == AdvancedToolWorkflow.SampleBlackPoint;
    public bool IsSampleWhitePointWorkflowVisible => ActiveWorkflow == AdvancedToolWorkflow.SampleWhitePoint;
    public bool IsPerspectiveWorkflowVisible => ActiveWorkflow == AdvancedToolWorkflow.PerspectiveCorrection;
    public bool IsCutOutWorkflowVisible => ActiveWorkflow == AdvancedToolWorkflow.CutOutRegion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial bool IsGrayscaleEnabled { get; set; } = false;

    partial void OnIsGrayscaleEnabledChanged(bool value)
    {
        if (value && !IsProcessing)
            _ = ApplyProcessingAsync();
    }

    [RelayCommand]
    private void ApplyGrayscale() => IsGrayscaleEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial bool IsInvertEnabled { get; set; } = false;

    partial void OnIsInvertEnabledChanged(bool value)
    {
        if (value && !IsProcessing)
            _ = ApplyProcessingAsync();
    }

    [RelayCommand]
    private void ApplyInvert() => IsInvertEnabled = true;

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
    [NotifyPropertyChangedFor(nameof(CanApplyPerspectiveWorkflow))]
    public partial bool IsPerspectiveCorrectionMode { get; set; } = false;

    partial void OnIsPerspectiveCorrectionModeChanged(bool value)
    {
        if (value)
        {
            IsEyedropperBlackMode = false;
            IsEyedropperWhiteMode = false;
            IsCutOutRegionMode = false;
            System.Diagnostics.Debug.WriteLine("Perspective Correction Mode activated - other tools deactivated");
        }
        else
        {
            ClearPerspectiveSelection();
            System.Diagnostics.Debug.WriteLine("Perspective Correction Mode deactivated");
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyToolActive))]
    public partial bool IsCutOutRegionMode { get; set; } = false;

    partial void OnIsCutOutRegionModeChanged(bool value)
    {
        if (value)
        {
            IsEyedropperBlackMode = false;
            IsEyedropperWhiteMode = false;
            IsPerspectiveCorrectionMode = false;
        }
        else
        {
            ClearCutOutSelectionInternal();
        }
    }

    public System.Drawing.Point? CutOutStartPoint { get; private set; }
    public System.Drawing.Point? CutOutEndPoint { get; private set; }
    public bool HasCutOutSelection => CutOutStartPoint.HasValue && CutOutEndPoint.HasValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCornerInstruction))]
    [NotifyPropertyChangedFor(nameof(CurrentCornerNumber))]
    [NotifyPropertyChangedFor(nameof(IsCornerSelectionComplete))]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    [NotifyPropertyChangedFor(nameof(CanApplyPerspectiveWorkflow))]
    public partial Point? TopLeftCorner { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCornerInstruction))]
    [NotifyPropertyChangedFor(nameof(CurrentCornerNumber))]
    [NotifyPropertyChangedFor(nameof(IsCornerSelectionComplete))]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    [NotifyPropertyChangedFor(nameof(CanApplyPerspectiveWorkflow))]
    public partial Point? TopRightCorner { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCornerInstruction))]
    [NotifyPropertyChangedFor(nameof(CurrentCornerNumber))]
    [NotifyPropertyChangedFor(nameof(IsCornerSelectionComplete))]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    [NotifyPropertyChangedFor(nameof(CanApplyPerspectiveWorkflow))]
    public partial Point? BottomRightCorner { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCornerInstruction))]
    [NotifyPropertyChangedFor(nameof(CurrentCornerNumber))]
    [NotifyPropertyChangedFor(nameof(IsCornerSelectionComplete))]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    [NotifyPropertyChangedFor(nameof(CanApplyPerspectiveWorkflow))]
    public partial Point? BottomLeftCorner { get; set; } = null;

    public string CurrentCornerInstruction
    {
        get
        {
            if (!IsPerspectiveCorrectionMode) return "1. Select Top-Left corner";
            if (TopLeftCorner == null) return "1. Select Top-Left corner";
            if (TopRightCorner == null) return "2. Select Top-Right corner";
            if (BottomRightCorner == null) return "3. Select Bottom-Right corner";
            if (BottomLeftCorner == null) return "4. Select Bottom-Left corner";
            return "All corners selected. Apply correction to continue.";
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
    public bool CanApplyPerspectiveWorkflow => IsPerspectiveCorrectionMode && IsCornerSelectionComplete && !IsProcessing;

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
            IsEyedropperWhiteMode = false;
            IsPerspectiveCorrectionMode = false;
            IsCutOutRegionMode = false;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnyToolActive))]
    public partial bool IsEyedropperWhiteMode { get; set; } = false;

    partial void OnIsEyedropperWhiteModeChanged(bool value)
    {
        if (value)
        {
            IsEyedropperBlackMode = false;
            IsPerspectiveCorrectionMode = false;
            IsCutOutRegionMode = false;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyPerspectiveWorkflow))]
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
    public bool IsAnyToolActive => IsEyedropperBlackMode || IsEyedropperWhiteMode || IsPerspectiveCorrectionMode || IsCutOutRegionMode;

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

    private readonly Stack<MagickImage> _undoStack = new();
    private readonly Stack<MagickImage> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0 || HasPendingChanges;
    public bool CanRedo => _redoStack.Count > 0;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(HasPendingChanges))
            UndoCommand.NotifyCanExecuteChanged();
    }

    public event EventHandler<MagickImage>? ImageProcessed;
    public event EventHandler? PerspectiveCornersClearedRequested;
    public event EventHandler<MagickImage>? RegionCutOut;
    public event EventHandler? CutOutSelectionClearedRequested;

    [RelayCommand]
    private void OpenBlackPointWorkflow() => ActivateWorkflow(AdvancedToolWorkflow.SampleBlackPoint);

    [RelayCommand]
    private void OpenWhitePointWorkflow() => ActivateWorkflow(AdvancedToolWorkflow.SampleWhitePoint);

    [RelayCommand]
    private void OpenPerspectiveWorkflow() => ActivateWorkflow(AdvancedToolWorkflow.PerspectiveCorrection);

    [RelayCommand]
    private void OpenCutOutWorkflow() => ActivateWorkflow(AdvancedToolWorkflow.CutOutRegion);

    [RelayCommand]
    private void CancelWorkflow() => CloseActiveWorkflow();

    public void CloseActiveWorkflow()
    {
        switch (ActiveWorkflow)
        {
            case AdvancedToolWorkflow.SampleBlackPoint:
                IsEyedropperBlackMode = false;
                break;
            case AdvancedToolWorkflow.SampleWhitePoint:
                IsEyedropperWhiteMode = false;
                break;
            case AdvancedToolWorkflow.PerspectiveCorrection:
                IsPerspectiveCorrectionMode = false;
                break;
            case AdvancedToolWorkflow.CutOutRegion:
                IsCutOutRegionMode = false;
                break;
        }

        ActiveWorkflow = AdvancedToolWorkflow.None;
    }

    private void ActivateWorkflow(AdvancedToolWorkflow workflow)
    {
        if (ActiveWorkflow == workflow)
            return;

        CloseActiveWorkflow();
        ActiveWorkflow = workflow;

        switch (workflow)
        {
            case AdvancedToolWorkflow.SampleBlackPoint:
                IsEyedropperBlackMode = true;
                break;
            case AdvancedToolWorkflow.SampleWhitePoint:
                IsEyedropperWhiteMode = true;
                break;
            case AdvancedToolWorkflow.PerspectiveCorrection:
                IsPerspectiveCorrectionMode = true;
                break;
            case AdvancedToolWorkflow.CutOutRegion:
                IsCutOutRegionMode = true;
                break;
        }
    }

    private void ClearPerspectiveSelection()
    {
        TopLeftCorner = null;
        TopRightCorner = null;
        BottomRightCorner = null;
        BottomLeftCorner = null;

        PerspectiveCornersClearedRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetCutOutRegion(Point start, Point end)
    {
        CutOutStartPoint = start;
        CutOutEndPoint = end;
        OnPropertyChanged(nameof(HasCutOutSelection));
        PerformCutOutCommand.NotifyCanExecuteChanged();
    }

    private void ClearCutOutSelectionInternal()
    {
        CutOutStartPoint = null;
        CutOutEndPoint = null;
        OnPropertyChanged(nameof(HasCutOutSelection));
        PerformCutOutCommand.NotifyCanExecuteChanged();
        CutOutSelectionClearedRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearCutOutSelection() => ClearCutOutSelectionInternal();

    [RelayCommand(CanExecute = nameof(HasCutOutSelection))]
    private void PerformCutOut()
    {
        if (baselineImage == null || CutOutStartPoint == null || CutOutEndPoint == null)
            return;

        int x = Math.Min(CutOutStartPoint.Value.X, CutOutEndPoint.Value.X);
        int y = Math.Min(CutOutStartPoint.Value.Y, CutOutEndPoint.Value.Y);
        int width = Math.Abs(CutOutEndPoint.Value.X - CutOutStartPoint.Value.X);
        int height = Math.Abs(CutOutEndPoint.Value.Y - CutOutStartPoint.Value.Y);

        if (width < 5 || height < 5)
            return;

        x = Math.Clamp(x, 0, (int)baselineImage.Width - 1);
        y = Math.Clamp(y, 0, (int)baselineImage.Height - 1);
        width = Math.Clamp(width, 1, (int)baselineImage.Width - x);
        height = Math.Clamp(height, 1, (int)baselineImage.Height - y);

        MagickImage cropped = Helpers.ImageProcessingHelper.CropRegion(baselineImage, x, y, width, height);
        RegionCutOut?.Invoke(this, cropped);
        CloseActiveWorkflow();
    }

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
        ActiveWorkflow = AdvancedToolWorkflow.None;
        IsGrayscaleEnabled = false;
        IsInvertEnabled = false;
        ContrastValue = 0.0;
        BlackPointLevel = 0.0;
        WhitePointLevel = 100.0;
        IsPerspectiveCorrectionMode = false;
        IsCutOutRegionMode = false;
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

        if (trueOriginalImage != null)
        {
            try
            {
                // Reset All reverts the baseline back to the true original
                baselineImage = (MagickImage)trueOriginalImage.Clone();
                processedImage = (MagickImage)trueOriginalImage.Clone();

                _undoStack.Clear();
                _redoStack.Clear();
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();

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

            // Snapshot the pre-apply baseline so the user can undo this step
            _undoStack.Push((MagickImage)baselineImage.Clone());
            _redoStack.Clear();

            // Advance the baseline so the next Apply builds on this result
            baselineImage = (MagickImage)processedImage!.Clone();

            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();

            // Reset the settings that were just applied so they don't stack
            ResetPendingSettings();

            if (doPerspective)
                CloseActiveWorkflow();

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
    private void ClearPerspectiveCorners() => ClearPerspectiveSelection();

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (HasPendingChanges)
        {
            ResetPendingSettings();
            ActiveWorkflow = AdvancedToolWorkflow.None;
            if (IsPerspectiveCorrectionMode)
                IsPerspectiveCorrectionMode = false;
            IsEyedropperBlackMode = false;
            IsEyedropperWhiteMode = false;
            IsCutOutRegionMode = false;
            return;
        }

        if (!_undoStack.TryPop(out MagickImage? previous)) return;
        IsProcessing = true;
        _redoStack.Push((MagickImage)baselineImage!.Clone());
        baselineImage = previous;
        processedImage = (MagickImage)previous.Clone();
        ImageProcessed?.Invoke(this, processedImage);
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        IsProcessing = false;
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (!_redoStack.TryPop(out MagickImage? next)) return;

        IsProcessing = true;
        _undoStack.Push((MagickImage)baselineImage!.Clone());
        baselineImage = next;
        processedImage = (MagickImage)next.Clone();
        ImageProcessed?.Invoke(this, processedImage);
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        IsProcessing = false;
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
            ActiveWorkflow = AdvancedToolWorkflow.None;
            await ApplyProcessingAsync();
        }
        else if (IsEyedropperWhiteMode)
        {
            SelectedWhitePointColor = new MagickColor(color.R, color.G, color.B, color.A);
            IsEyedropperWhiteMode = false;
            ActiveWorkflow = AdvancedToolWorkflow.None;
            await ApplyProcessingAsync();
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
        ActiveWorkflow = AdvancedToolWorkflow.None;
        IsGrayscaleEnabled = false;
        IsInvertEnabled = false;
        ContrastValue = 0.0;
        BlackPointLevel = 0.0;
        WhitePointLevel = 100.0;
        IsPerspectiveCorrectionMode = false;
        IsCutOutRegionMode = false;
        BorderPixels = 20;
        SelectedBlackPointColor = null;
        SelectedWhitePointColor = null;
        IsEyedropperBlackMode = false;
        IsEyedropperWhiteMode = false;
        IsProcessing = false;

        trueOriginalImage = null;
        baselineImage = null;
        processedImage = null;

        _undoStack.Clear();
        _redoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();

        OnPropertyChanged(nameof(CurrentCornerInstruction));
        OnPropertyChanged(nameof(CurrentCornerNumber));
        OnPropertyChanged(nameof(IsCornerSelectionComplete));
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(IsAnyToolActive));
    }
}
