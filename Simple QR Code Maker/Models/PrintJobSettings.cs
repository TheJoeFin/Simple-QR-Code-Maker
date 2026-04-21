namespace Simple_QR_Code_Maker.Models;

public record PrintJobSettings
{
    public int CodesPerPage { get; init; } = 4;
    public double MarginMm { get; init; } = 10;
    public bool ShowLabels { get; init; } = true;
}
