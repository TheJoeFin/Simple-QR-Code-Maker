using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Models;
public struct ErrorCorrectionOptions
{
    public string Description { get; set; }
    public string ShortDescription { get; set; }
    public ErrorCorrectionLevel ErrorCorrectionLevel { get; set; }

    public ErrorCorrectionOptions(string shortDescription, string description, ErrorCorrectionLevel errorCorrectionLevel)
    {
        ShortDescription = shortDescription;
        Description = description;
        ErrorCorrectionLevel = errorCorrectionLevel;
    }
}
