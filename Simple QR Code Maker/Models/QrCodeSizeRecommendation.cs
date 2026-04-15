namespace Simple_QR_Code_Maker.Models;

public enum QrCodeSizeRecommendationKind
{
    Exact,
    PaddingDependent,
    TransparencyDependent,
    LowContrast,
    Error,
}

public readonly record struct QrCodeSizeRecommendation(QrCodeSizeRecommendationKind Kind, string Text)
{
    public bool IsExact => Kind == QrCodeSizeRecommendationKind.Exact;
}
