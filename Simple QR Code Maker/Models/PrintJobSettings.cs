namespace Simple_QR_Code_Maker.Models;

public record PrintJobSettings
{
    public static readonly int[] SupportedCodesPerPage = [1, 2, 4, 6, 9, 12, 16];
    public const double MinimumMarginMm = 0;
    public const double MaximumMarginMm = 50;
    public const double MinimumCodeSizeMm = 5;
    public const double MaximumCodeSizeMm = 250;
    public const double MinimumSpacingMm = 0;
    public const double MaximumSpacingMm = 25;
    public const int DefaultCodesPerPage = 12;
    public const double DefaultMarginMm = 10;
    public const double DefaultCodeSizeMm = 35;
    public const double DefaultSpacingMm = 4;
    public const bool DefaultShowLabels = true;
    public const bool DefaultFitAsManyAsPossible = true;
    public const PrintPageType DefaultPageType = PrintPageType.Auto;
    public const PrintPageLayout DefaultPageLayout = PrintPageLayout.Portrait;

    public int CodesPerPage { get; init; } = DefaultCodesPerPage;
    public PrintPageType PageType { get; init; } = DefaultPageType;
    public PrintPageLayout PageLayout { get; init; } = DefaultPageLayout;
    public double MarginMm { get; init; } = DefaultMarginMm;
    public double CodeSizeMm { get; init; } = DefaultCodeSizeMm;
    public double SpacingMm { get; init; } = DefaultSpacingMm;
    public bool ShowLabels { get; init; } = DefaultShowLabels;
    public bool FitAsManyAsPossible { get; init; } = DefaultFitAsManyAsPossible;

    public PrintJobSettings Normalize()
    {
        return this with
        {
            CodesPerPage = NormalizeCodesPerPage(CodesPerPage),
            PageType = PrintPageTypeHelper.Resolve(PageType),
            MarginMm = NormalizeMillimeters(MarginMm, DefaultMarginMm, MinimumMarginMm, MaximumMarginMm),
            CodeSizeMm = NormalizeMillimeters(CodeSizeMm, DefaultCodeSizeMm, MinimumCodeSizeMm, MaximumCodeSizeMm),
            SpacingMm = NormalizeMillimeters(SpacingMm, DefaultSpacingMm, MinimumSpacingMm, MaximumSpacingMm),
        };
    }

    private static int NormalizeCodesPerPage(int value)
    {
        return Array.IndexOf(SupportedCodesPerPage, value) >= 0
            ? value
            : DefaultCodesPerPage;
    }

    private static double NormalizeMillimeters(double value, double fallback, double minimum, double maximum)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return fallback;
        }

        return Math.Clamp(value, minimum, maximum);
    }
}
