using System.Globalization;

namespace Simple_QR_Code_Maker.Models;

public static class PrintPageTypeHelper
{
    public static readonly PrintPageType[] SelectablePageTypes =
    [
        PrintPageType.Letter,
        PrintPageType.Legal,
        PrintPageType.Statement,
        PrintPageType.Executive,
        PrintPageType.Tabloid,
        PrintPageType.Ledger,
        PrintPageType.A3,
        PrintPageType.A4,
        PrintPageType.A5,
        PrintPageType.B4,
        PrintPageType.B5,
    ];

    public static PrintPageType Resolve(PrintPageType pageType)
    {
        return pageType == PrintPageType.Auto
            ? GetRegionalDefault()
            : pageType;
    }

    public static PrintPageType GetRegionalDefault()
    {
        try
        {
            RegionInfo region = new(CultureInfo.CurrentCulture.Name);
            return region.IsMetric ? PrintPageType.A4 : PrintPageType.Letter;
        }
        catch (ArgumentException)
        {
            return PrintPageType.Letter;
        }
    }
}
