using System.Globalization;

using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Helpers;

public static class LocalizedDisplay
{
    public static string ContentKindName(QrContentKind kind) => kind switch
    {
        QrContentKind.VCard => "History_KindVCard".GetLocalized(),
        QrContentKind.WiFi => "History_KindWifi".GetLocalized(),
        QrContentKind.Email => "History_KindEmail".GetLocalized(),
        _ => string.Empty,
    };

    public static string HistoryText(HistoryItem item) => ContentText(item.CodesContent, item.ContentKind);

    public static string ContentText(string text, QrContentKind kind) => kind switch
    {
        QrContentKind.VCard => VCardBuilderHelper.GetDisplayName(text, "History_FallbackContact".GetLocalized()),
        QrContentKind.WiFi => WifiBuilderHelper.GetDisplayName(text, "History_FallbackNetwork".GetLocalized()),
        QrContentKind.Email => EmailBuilderHelper.GetDisplayName(text, ContentKindName(kind)),
        _ => text,
    };

    public static string DecodingSourceName(DecodingSourceKind kind, string? path) => kind switch
    {
        DecodingSourceKind.Camera => "Read_CameraCapture".GetLocalized(),
        DecodingSourceKind.SnippingTool => "Read_SnippingToolCapture".GetLocalized(),
        DecodingSourceKind.Clipboard => "Read_ClipboardImage".GetLocalized(),
        _ => string.IsNullOrEmpty(path) ? "Read_UnknownSource".GetLocalized() : Path.GetFileName(path),
    };

    public static string FirstDecodedText(IReadOnlyList<string> texts)
        => texts.Count == 0 ? "Read_HistoryNoCodes".GetLocalized() : texts[0];

    public static string DecodedCountSuffix(IReadOnlyList<string> texts)
        => texts.Count > 1 ? "Read_HistoryAdditionalCodes".FormatLocalized(texts.Count - 1) : string.Empty;

    public static string FrameDescription(QrFramePreset? preset)
        => preset.HasValue ? FrameOption(preset.Value).Description : string.Empty;

    public static string? FrameText(QrFramePreset preset, string? text)
        => preset == QrFramePreset.None
            ? null
            : string.IsNullOrWhiteSpace(text) ? FrameOption(preset).DefaultText : text;

    public static QrFramePresetOption FrameOption(QrFramePreset preset)
    {
        QrFramePreset id = QrFramePresetCatalog.GetOption(preset).Preset;
        return new QrFramePresetOption(id,
            $"FramePreset_{id}_Short".GetLocalized(),
            $"FramePreset_{id}_Description".GetLocalized(),
            id == QrFramePreset.None ? string.Empty : $"FramePreset_{id}_Caption".GetLocalized());
    }

    public static QrDesignPreset DesignPreset(QrDesignPreset preset)
        => preset with
        {
            Name = $"DesignPreset_{preset.Id}_Name".GetLocalized(),
            Description = $"DesignPreset_{preset.Id}_Description".GetLocalized(),
            Caption = preset.Frame == QrFramePreset.None
                ? string.Empty
                : $"DesignPreset_{preset.Id}_Caption".GetLocalized(),
        };

    public static string IdFormatName(SpreadsheetGeneratedIdFormatOption? option) => option?.Format switch
    {
        SpreadsheetGeneratedIdFormat.GuidWithoutDashes => "GenerateIds_FormatGuidCompact".GetLocalized(),
        SpreadsheetGeneratedIdFormat.NanoId => "GenerateIds_FormatNanoId".GetLocalized(),
        _ => "GenerateIds_FormatGuid".GetLocalized(),
    };

    public static IEnumerable<SpreadsheetGeneratedIdFormatOption> GetIdFormatOptions()
        => SpreadsheetGeneratedIdFormatOption.All.Select(option => new SpreadsheetGeneratedIdFormatOption
        {
            DisplayName = IdFormatName(option),
            Format = option.Format,
        });

    public static string SizeRecommendation(QrCodeSizeRecommendation recommendation) => recommendation.Kind switch
    {
        QrCodeSizeRecommendationKind.Exact => RegionInfo.CurrentRegion.IsMetric
            ? "Preview_SizeCentimeters".FormatLocalized(recommendation.SideMillimeters / 10)
            : "Preview_SizeInches".FormatLocalized(recommendation.SideMillimeters / 25.4),
        QrCodeSizeRecommendationKind.PaddingDependent => "Preview_SizePadding".FormatLocalized(recommendation.PaddingModules),
        QrCodeSizeRecommendationKind.TransparencyDependent => "Preview_SizeTransparency".GetLocalized(),
        QrCodeSizeRecommendationKind.LowContrast => "Preview_SizeContrast".GetLocalized(),
        _ => "Preview_SizeDistance".GetLocalized(),
    };
}
