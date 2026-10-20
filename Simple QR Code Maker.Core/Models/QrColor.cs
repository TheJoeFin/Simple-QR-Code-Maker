namespace Simple_QR_Code_Maker.Core.Models;

public readonly record struct QrColor(byte Red, byte Green, byte Blue, byte Alpha = byte.MaxValue)
{
    public static QrColor Black { get; } = new(0, 0, 0);

    public static QrColor White { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);
}
