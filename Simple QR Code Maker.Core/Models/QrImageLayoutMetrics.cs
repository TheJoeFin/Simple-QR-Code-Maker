namespace Simple_QR_Code_Maker.Models;

internal readonly record struct QrImageLayoutMetrics(
    double CanvasWidth,
    double CanvasHeight,
    double QrWidth,
    double QrHeight)
{
    public static QrImageLayoutMetrics Square { get; } = new(1, 1, 1, 1);

    public double WidthPerQrSize => NormalizeScale(CanvasWidth / Math.Max(QrWidth, 1));

    public double HeightPerQrSize => NormalizeScale(CanvasHeight / Math.Max(QrHeight, 1));

    public static QrImageLayoutMetrics FromScaleProfile(double widthPerQrSize, double heightPerQrSize)
    {
        return new QrImageLayoutMetrics(
            NormalizeScale(widthPerQrSize),
            NormalizeScale(heightPerQrSize),
            1,
            1);
    }

    private static double NormalizeScale(double value) => double.IsFinite(value) ? Math.Max(value, 1) : 1;
}
