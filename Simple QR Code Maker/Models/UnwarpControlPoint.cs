namespace Simple_QR_Code_Maker.Models;

public enum UnwarpPointKind { OuterCorner, AlignmentPattern }

public class UnwarpControlPoint
{
    public int ModuleRow { get; init; }
    public int ModuleCol { get; init; }
    public double IdealX { get; set; }
    public double IdealY { get; set; }

    // Set by the user clicking the image; null until placed.
    public System.Drawing.Point? PlacedImagePoint { get; set; }

    public UnwarpPointKind Kind { get; init; }
    public string Label { get; init; } = "";
    public int OrderIndex { get; init; }

    // True when the user accepted the bilinear-estimated position via "Use Estimated".
    // The actual image-space coordinate is then computed on demand from the corner positions.
    public bool IsEstimated { get; set; }

    public bool IsPlacedOrEstimated => PlacedImagePoint.HasValue || IsEstimated;
}
