using System.Drawing;

namespace Simple_QR_Code_Maker.Helpers;

public static class QrAlignmentPatternHelper
{
    // ISO 18004:2015 Annex E – alignment pattern center row/col coordinates per version
    private static readonly int[][] AlignmentPositions =
    [
        [],                                     // v1
        [6, 18],                                // v2
        [6, 22],                                // v3
        [6, 26],                                // v4
        [6, 30],                                // v5
        [6, 34],                                // v6
        [6, 22, 38],                            // v7
        [6, 24, 42],                            // v8
        [6, 26, 46],                            // v9
        [6, 28, 50],                            // v10
        [6, 30, 54],                            // v11
        [6, 32, 58],                            // v12
        [6, 34, 62],                            // v13
        [6, 26, 46, 66],                        // v14
        [6, 26, 48, 70],                        // v15
        [6, 26, 50, 74],                        // v16
        [6, 30, 54, 78],                        // v17
        [6, 30, 56, 82],                        // v18
        [6, 30, 58, 86],                        // v19
        [6, 34, 62, 90],                        // v20
        [6, 28, 50, 72, 94],                    // v21
        [6, 26, 50, 74, 98],                    // v22
        [6, 30, 54, 78, 102],                   // v23
        [6, 28, 54, 80, 106],                   // v24
        [6, 32, 58, 84, 110],                   // v25
        [6, 30, 58, 86, 114],                   // v26
        [6, 34, 62, 90, 118],                   // v27
        [6, 26, 50, 74, 98, 122],               // v28
        [6, 30, 54, 78, 102, 126],              // v29
        [6, 26, 52, 78, 104, 130],              // v30
        [6, 30, 56, 82, 108, 134],              // v31
        [6, 34, 60, 86, 112, 138],              // v32
        [6, 30, 58, 86, 114, 142],              // v33
        [6, 34, 62, 90, 118, 146],              // v34
        [6, 30, 54, 78, 102, 126, 150],         // v35
        [6, 26, 52, 78, 104, 130, 156],         // v36
        [6, 30, 56, 82, 108, 134, 160],         // v37
        [6, 34, 60, 86, 112, 138, 164],         // v38
        [6, 30, 58, 86, 114, 142, 168],         // v39
        [6, 32, 62, 92, 122, 152, 182],         // v40
    ];

    public static int GetDimension(int version) => version * 4 + 17;

    // Returns all alignment pattern centers (moduleRow, moduleCol) for the given version,
    // excluding positions that overlap the three finder pattern + separator zones.
    public static List<(int Row, int Col)> GetAlignmentPatternCenters(int version)
    {
        if (version < 1 || version > 40)
            return [];

        int[] positions = AlignmentPositions[version - 1];
        if (positions.Length == 0)
            return [];

        int dim = GetDimension(version);
        var centers = new List<(int Row, int Col)>();

        foreach (int r in positions)
        {
            foreach (int c in positions)
            {
                bool overlapsTopLeft = r <= 8 && c <= 8;
                bool overlapsTopRight = r <= 8 && c >= dim - 8;
                bool overlapsBottomLeft = r >= dim - 8 && c <= 8;
                if (!overlapsTopLeft && !overlapsTopRight && !overlapsBottomLeft)
                    centers.Add((r, c));
            }
        }

        return centers;
    }

    // Maps a module-center position to ideal pixel coordinates in the unwarped output.
    // Module (row, col) center occupies the pixel at (col+0.5)*outputSize/dim, (row+0.5)*outputSize/dim.
    public static (double X, double Y) GetIdealPixelCoord(int moduleRow, int moduleCol, int version, int outputSize)
    {
        int dim = GetDimension(version);
        double x = (moduleCol + 0.5) * outputSize / dim;
        double y = (moduleRow + 0.5) * outputSize / dim;
        return (x, y);
    }

    // Bilinear interpolation: given the four outer-corner image-space coordinates of the QR code,
    // estimate where the module at (moduleRow, moduleCol) lands in the actual distorted image.
    // Parameters: topLeft, topRight, bottomLeft, bottomRight are the four corner image pixels.
    public static PointF EstimateImagePosition(
        Point topLeft, Point topRight, Point bottomLeft, Point bottomRight,
        int moduleRow, int moduleCol, int version)
    {
        int dim = GetDimension(version);

        // Normalized (u, v): u=0 is the left edge, u=1 is the right edge; v=0 top, v=1 bottom.
        // A module CENTER at (row, col) sits at (col+0.5)/dim of the total width/height.
        double u = (moduleCol + 0.5) / dim;
        double v = (moduleRow + 0.5) / dim;

        float x = (float)(
            (1 - u) * (1 - v) * topLeft.X +
            u * (1 - v) * topRight.X +
            (1 - u) * v * bottomLeft.X +
            u * v * bottomRight.X);

        float y = (float)(
            (1 - u) * (1 - v) * topLeft.Y +
            u * (1 - v) * topRight.Y +
            (1 - u) * v * bottomLeft.Y +
            u * v * bottomRight.Y);

        return new PointF(x, y);
    }
}
