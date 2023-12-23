using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Models;
public struct ErrorCorrectionOptions
{
    public string Description { get; set; }
    public ErrorCorrectionLevel ErrorCorrectionLevel { get; set; }

    public ErrorCorrectionOptions(string description, ErrorCorrectionLevel errorCorrectionLevel)
    {
        Description = description;
        ErrorCorrectionLevel = errorCorrectionLevel;
    }
}
