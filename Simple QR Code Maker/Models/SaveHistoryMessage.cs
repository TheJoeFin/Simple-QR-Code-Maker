namespace Simple_QR_Code_Maker.Models;

internal record SaveHistoryMessage
{
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
}
