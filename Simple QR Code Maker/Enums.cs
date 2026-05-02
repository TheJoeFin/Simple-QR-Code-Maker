namespace Simple_QR_Code_Maker;

public enum FileKind
{
    None = 0,
    PNG = 1,
    SVG = 2,
}

public enum MultiLineCodeMode
{
    OneLineOneCode = 0,
    MultilineOneCode = 1,
}

public enum LaunchMode
{
    CreatingQrCodes = 0,
    ReadingQrCodes = 1,
}

public enum QrContentKind
{
    PlainText = 0,
    VCard = 1,
    WiFi = 2,
    Email = 3,
}
