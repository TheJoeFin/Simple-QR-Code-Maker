using System.Text.Json.Serialization;
using Windows.UI;

namespace Simple_QR_Code_Maker.Models;

public sealed record QrCodeAppearance
{
    [JsonConverter(typeof(JsonStringEnumConverter<QrModuleStyle>))]
    public QrModuleStyle ModuleStyle { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<QrEyeStyle>))]
    public QrEyeStyle EyeStyle { get; init; }

    [JsonConverter(typeof(NullableColorJsonConverter))]
    public Color? FrameAccent { get; init; }

    [JsonConverter(typeof(NullableColorJsonConverter))]
    public Color? FrameGradientEnd { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<QrCaptionIcon>))]
    public QrCaptionIcon CaptionIcon { get; init; }
}
