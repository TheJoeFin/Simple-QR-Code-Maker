namespace Simple_QR_Code_Maker.Models;

internal readonly record struct QrImageLayoutMetrics(
    double CanvasWidth,
    double CanvasHeight,
    double QrWidth,
    double QrHeight)
{
    public static QrImageLayoutMetrics Square { get; } = new(1, 1, 1, 1);

    public double WidthPerQrSize => CanvasWidth / Math.Max(QrWidth, 1);

    public double HeightPerQrSize => CanvasHeight / Math.Max(QrHeight, 1);

    public static QrImageLayoutMetrics FromScaleProfile(double widthPerQrSize, double heightPerQrSize)
    {
        return new QrImageLayoutMetrics(
            Math.Max(widthPerQrSize, 1),
            Math.Max(heightPerQrSize, 1),
            1,
            1);
    }
}
