using Humanizer;
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
    public Color Foreground { get; set; } = Color.FromArgb(255, 255, 255, 255);
    public Color Background { get; set; } = Color.FromArgb(255, 0,0,0);

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
