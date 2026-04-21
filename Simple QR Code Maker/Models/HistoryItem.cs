using Humanizer;
using Simple_QR_Code_Maker.Helpers;
using System.Text.Json.Serialization;
using Windows.UI;
using ZXing;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Models;

public class HistoryItem : IEquatable<HistoryItem>
{
    public DateTime SavedDateTime { get; set; } = DateTime.Now;

    public string SaveDateAsString => SavedDateTime.Humanize();

    public string CodesContent { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter<QrContentKind>))]
    public QrContentKind ContentKind { get; set; } = QrContentKind.PlainText;

    public MultiLineCodeMode? MultiLineCodeModeOverride { get; set; }

    [JsonConverter(typeof(ColorJsonConverter))]
    public Color Foreground { get; set; } = Color.FromArgb(255, 0, 0, 0);

    [JsonConverter(typeof(ColorJsonConverter))]
    public Color Background { get; set; } = Color.FromArgb(255, 255, 255, 255);

    public string ErrorCorrectionLevelAsString { get; set; } = "M";

    [JsonIgnore]
    public ErrorCorrectionLevel ErrorCorrection
    {
        get
        {
            return ErrorCorrectionLevelAsString switch
            {
                "H" => ErrorCorrectionLevel.H,
                "L" => ErrorCorrectionLevel.L,
                "Q" => ErrorCorrectionLevel.Q,
                _ => ErrorCorrectionLevel.M,
            };
        }
        set
        {
            ErrorCorrectionLevelAsString = value.ToString();
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<BarcodeFormat>))]
    public BarcodeFormat Format { get; set; } = BarcodeFormat.QR_CODE;

    public string? LogoImagePath { get; set; }

    public string? LogoEmoji { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<EmojiLogoStyle>))]
    public EmojiLogoStyle? LogoEmojiStyle { get; set; }

    public double LogoSizePercentage { get; set; } = 15;

    public double LogoPaddingPixels { get; set; } = 4.0;

    [JsonIgnore]
    public string DisplayText => ContentKind switch
    {
        QrContentKind.VCard => VCardBuilderHelper.GetDisplayName(CodesContent),
        QrContentKind.WiFi => WifiBuilderHelper.GetDisplayName(CodesContent),
        _ => CodesContent,
    };

    [JsonIgnore]
    public string ContentKindLabel => ContentKind switch
    {
        QrContentKind.VCard => "vCard",
        QrContentKind.WiFi => "WiFi",
        _ => string.Empty,
    };

    public HistoryItem()
    {

    }

    public bool Equals(HistoryItem? other)
    {
        if (other is null)
            return false;

        return CodesContent.Equals(other.CodesContent, StringComparison.InvariantCulture);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as HistoryItem);
    }

    public override int GetHashCode() => HashCode.Combine(CodesContent);
}
