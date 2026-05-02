using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Simple_QR_Code_Maker.Helpers;

public class PerspectiveCorrectionHelper
{
    private readonly List<System.Drawing.Point> corners = [];
    private readonly List<Ellipse> cornerMarkers = [];
    private readonly Action<System.Drawing.Point, int> onCornerSelected;

    public PerspectiveCorrectionHelper(Action<System.Drawing.Point, int> cornerSelectedCallback)
    {
        onCornerSelected = cornerSelectedCallback;
    }

    public int CornerCount => corners.Count;

    public void AddCorner(System.Drawing.Point point)
    {
        if (corners.Count >= 4)
            return;

        corners.Add(point);
        onCornerSelected?.Invoke(point, corners.Count - 1);
    }

    public void ClearCorners()
    {
        corners.Clear();
        cornerMarkers.Clear();
    }

    public static Ellipse CreateCornerMarker(System.Drawing.Point point, int index)
    {
        var marker = new Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(Microsoft.UI.Colors.Red),
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.White),
            StrokeThickness = 2
        };

        Canvas.SetLeft(marker, point.X - 10);
        Canvas.SetTop(marker, point.Y - 10);

        return marker;
    }

    public System.Drawing.Point? GetCorner(int index)
    {
        if (index < 0 || index >= corners.Count)
            return null;

        return corners[index];
    }

    public bool AllCornersSet => corners.Count == 4;

    public static System.Drawing.Point ConvertToImageCoordinates(
        Windows.Foundation.Point clickPoint,
        FrameworkElement imageControl,
        double imageActualWidth,
        double imageActualHeight)
    {
        double scaleX = imageActualWidth / imageControl.ActualWidth;
        double scaleY = imageActualHeight / imageControl.ActualHeight;

        int x = (int)(clickPoint.X * scaleX);
        int y = (int)(clickPoint.Y * scaleY);

        return new System.Drawing.Point(x, y);
    }

    public static Windows.Foundation.Point ConvertToDisplayCoordinates(
        System.Drawing.Point imagePoint,
        FrameworkElement imageControl,
        double imageActualWidth,
        double imageActualHeight)
    {
        double scaleX = imageControl.ActualWidth / imageActualWidth;
        double scaleY = imageControl.ActualHeight / imageActualHeight;

        double x = imagePoint.X * scaleX;
        double y = imagePoint.Y * scaleY;

        return new Windows.Foundation.Point(x, y);
    }

    public string GetCornerName(int index)
    {
        return index switch
        {
            0 => "Top-Left",
            1 => "Top-Right",
            2 => "Bottom-Right",
            3 => "Bottom-Left",
            _ => "Unknown"
        };
    }

    public List<System.Drawing.Point> GetAllCorners()
    {
        return [.. corners];
    }
}
