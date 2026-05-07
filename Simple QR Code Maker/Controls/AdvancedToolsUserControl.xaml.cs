using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.ViewModels;
using Windows.UI;

namespace Simple_QR_Code_Maker.Controls;

public sealed partial class AdvancedToolsUserControl : UserControl
{
    private AdvancedToolsViewModel viewModel = new();

    public AdvancedToolsViewModel ViewModel
    {
        get => viewModel;
        set
        {
            if (viewModel != null)
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            viewModel = value;
            if (viewModel != null)
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            if (Content is not null)
                Bindings.Update();
            DrawUnwarpPreview();
        }
    }

    public AdvancedToolsUserControl()
    {
        InitializeComponent();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AdvancedToolsViewModel.UnwarpControlPoints)
            or nameof(AdvancedToolsViewModel.IsUnwarpWorkflowVisible))
        {
            DrawUnwarpPreview();
        }
    }

    private void DrawUnwarpPreview()
    {
        if (UnwarpPreviewCanvas == null) return;
        UnwarpPreviewCanvas.Children.Clear();

        if (!ViewModel.IsUnwarpWorkflowVisible) return;

        int version = ViewModel.QrVersion;
        int dim = QrAlignmentPatternHelper.GetDimension(version);
        var points = ViewModel.UnwarpControlPoints;

        const double canvasSize = 140.0;
        const double padding = 6.0;
        double drawingSize = canvasSize - 2 * padding;

        double ToCanvasX(double moduleCol) => padding + (moduleCol + 0.5) / dim * drawingSize;
        double ToCanvasY(double moduleRow) => padding + (moduleRow + 0.5) / dim * drawingSize;
        double ToCanvasLen(double modules) => modules / dim * drawingSize;

        // Finder pattern zones (7x7 modules at TL, TR, BL)
        (double Row, double Col)[] finderTopLefts = [(0, 0), (0, dim - 7), (dim - 7, 0)];
        foreach (var (fRow, fCol) in finderTopLefts)
        {
            double left = padding + fCol / dim * drawingSize;
            double top = padding + fRow / dim * drawingSize;
            double size = ToCanvasLen(7);

            var rect = new Rectangle
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(50, 120, 120, 120)),
                Stroke = new SolidColorBrush(Color.FromArgb(100, 120, 120, 120)),
                StrokeThickness = 1,
            };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            UnwarpPreviewCanvas.Children.Add(rect);
        }

        // Control point dots
        var nextPoint = ViewModel.NextUnwarpPoint;
        foreach (var pt in points)
        {
            bool isCorner = pt.Kind == UnwarpPointKind.OuterCorner;
            bool isPlaced = pt.IsPlacedOrEstimated;
            bool isNext = pt == nextPoint;
            double dotSize = isCorner ? 10.0 : 7.0;

            byte alpha = (isPlaced || isNext) ? (byte)230 : (byte)110;
            Color color = isCorner
                ? Color.FromArgb(alpha, 0, 120, 212)   // blue for corners
                : Color.FromArgb(alpha, 202, 80, 16);  // orange for alignment patterns

            double cx = ToCanvasX(pt.ModuleCol);
            double cy = ToCanvasY(pt.ModuleRow);

            if (isNext)
            {
                double haloSize = dotSize * 2.4;
                var transform = new ScaleTransform { ScaleX = 0.6, ScaleY = 0.6 };
                var halo = new Ellipse
                {
                    Width = haloSize,
                    Height = haloSize,
                    Fill = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
                    RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                    RenderTransform = transform,
                    Opacity = 0.75,
                };
                Canvas.SetLeft(halo, cx - haloSize / 2);
                Canvas.SetTop(halo, cy - haloSize / 2);
                UnwarpPreviewCanvas.Children.Add(halo);

                var sb = new Storyboard();

                var scaleXAnim = new DoubleAnimation
                {
                    From = 0.6,
                    To = 2.0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.9)),
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(scaleXAnim, transform);
                Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");

                var scaleYAnim = new DoubleAnimation
                {
                    From = 0.6,
                    To = 2.0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.9)),
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(scaleYAnim, transform);
                Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

                var opacityAnim = new DoubleAnimation
                {
                    From = 0.75,
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.9)),
                    RepeatBehavior = RepeatBehavior.Forever,
                };
                Storyboard.SetTarget(opacityAnim, halo);
                Storyboard.SetTargetProperty(opacityAnim, "Opacity");

                sb.Children.Add(scaleXAnim);
                sb.Children.Add(scaleYAnim);
                sb.Children.Add(opacityAnim);
                sb.Begin();
            }

            var ellipse = new Ellipse
            {
                Width = dotSize,
                Height = dotSize,
                Fill = new SolidColorBrush(color),
                Stroke = isPlaced && !isNext
                    ? new SolidColorBrush(Color.FromArgb(140, 255, 255, 255))
                    : null,
                StrokeThickness = isPlaced && !isNext ? 1.5 : 0,
            };
            Canvas.SetLeft(ellipse, cx - dotSize / 2);
            Canvas.SetTop(ellipse, cy - dotSize / 2);
            UnwarpPreviewCanvas.Children.Add(ellipse);
        }
    }

    private void UnwarpPreviewCanvas_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!ViewModel.IsUnwarpWorkflowVisible) return;

        var pos = e.GetCurrentPoint(UnwarpPreviewCanvas).Position;
        int version = ViewModel.QrVersion;
        int dim = QrAlignmentPatternHelper.GetDimension(version);
        const double canvasSize = 140.0;
        const double padding = 6.0;
        double drawingSize = canvasSize - 2 * padding;

        double ToCanvasX(double moduleCol) => padding + (moduleCol + 0.5) / dim * drawingSize;
        double ToCanvasY(double moduleRow) => padding + (moduleRow + 0.5) / dim * drawingSize;

        UnwarpControlPoint? closest = null;
        double minDist = double.MaxValue;

        foreach (var pt in ViewModel.UnwarpControlPoints)
        {
            double cx = ToCanvasX(pt.ModuleCol);
            double cy = ToCanvasY(pt.ModuleRow);
            double dist = Math.Sqrt(Math.Pow(pos.X - cx, 2) + Math.Pow(pos.Y - cy, 2));
            if (dist < minDist)
            {
                minDist = dist;
                closest = pt;
            }
        }

        const double hitRadius = 12.0;
        if (closest != null && minDist <= hitRadius)
            ViewModel.SetActiveUnwarpPoint(closest);
    }

    private void UndoAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.UndoCommand.CanExecute(null))
            ViewModel.UndoCommand.Execute(null);
    }

    private void RedoAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.RedoCommand.CanExecute(null))
            ViewModel.RedoCommand.Execute(null);
    }
}
