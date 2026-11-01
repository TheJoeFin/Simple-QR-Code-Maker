namespace Simple_QR_Code_Maker.Models;

public sealed class UrlBuilderLine
{
    public string Scheme { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Fragment { get; set; } = string.Empty;

    public bool UsesAuthorityDelimiter { get; set; }

    public List<UrlBuilderQueryParameter> QueryParameters { get; set; } = [];

    public UrlBuilderLine Clone()
    {
        return new UrlBuilderLine
        {
            Scheme = Scheme,
            Authority = Authority,
            Path = Path,
            Fragment = Fragment,
            UsesAuthorityDelimiter = UsesAuthorityDelimiter,
            QueryParameters = [.. QueryParameters.Select(parameter => parameter.Clone())]
        };
    }
}
