namespace Simple_QR_Code_Maker.Models;

public sealed record BrandCreationOptions
{
    public bool IncludeForeground { get; init; } = true;

    public bool IncludeBackground { get; init; } = true;

    public bool IncludeUrl { get; init; }

    public bool IncludeCenterImage { get; init; } = true;

    public bool IncludeErrorCorrection { get; init; } = true;

    public bool IncludeFrame { get; init; } = true;
}
