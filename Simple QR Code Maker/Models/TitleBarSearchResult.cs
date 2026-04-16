namespace Simple_QR_Code_Maker.Models;

public enum TitleBarSearchResultKind
{
    Faq,
    History,
    Brand,
}

public sealed class TitleBarSearchResult
{
    public TitleBarSearchResultKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string SearchText { get; init; } = string.Empty;

    public BrandItem? BrandItem { get; init; }

    public HistoryItem? HistoryItem { get; init; }

    public string KindLabel => Kind switch
    {
        TitleBarSearchResultKind.Brand => "Brand",
        TitleBarSearchResultKind.History => "History",
        _ => "FAQ",
    };
}
