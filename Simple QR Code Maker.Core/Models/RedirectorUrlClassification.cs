namespace Simple_QR_Code_Maker.Models;

public readonly record struct RedirectorUrlClassification(
    bool IsAbsoluteUri,
    bool IsKnownRedirector,
    bool IsSafeDomain,
    string Host)
{
    public bool ShouldWarn => IsKnownRedirector && !IsSafeDomain;

    public static RedirectorUrlClassification None => new(false, false, false, string.Empty);
}
