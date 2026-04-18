using Humanizer;
using System.Text.Json.Serialization;
using Windows.UI;

namespace Simple_QR_Code_Maker.Models;

public class BrandItem : IEquatable<BrandItem>
{
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedDateTime { get; set; } = DateTime.Now;

    public string CreatedDateAsString => CreatedDateTime.Humanize();

    [JsonConverter(typeof(NullableColorJsonConverter))]
    public Color? Foreground { get; set; }

    [JsonConverter(typeof(NullableColorJsonConverter))]
    public Color? Background { get; set; }

    public string? ErrorCorrectionLevelAsString { get; set; }

    public string? UrlContent { get; set; }

    public string? LogoImagePath { get; set; }

    public string? LogoEmoji { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<EmojiLogoStyle>))]
    public EmojiLogoStyle? LogoEmojiStyle { get; set; }

    public double? LogoSizePercentage { get; set; }

    public double? LogoPaddingPixels { get; set; }

    public bool IsDefault { get; set; } = false;

    public BrandItem()
    {
    }

    public bool Equals(BrandItem? other)
    {
        if (other is null)
            return false;

        return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as BrandItem);
    }

    public override int GetHashCode() => HashCode.Combine(Name.ToUpperInvariant());
}
