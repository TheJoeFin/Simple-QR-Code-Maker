using Simple_QR_Code_Maker.Models;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(HistoryItem))]
[JsonSerializable(typeof(DecodingImageItem))]
[JsonSerializable(typeof(TextBorderInfo))]
[JsonSerializable(typeof(LocalSettingsOptions))]
[JsonSerializable(typeof(BarcodeImageItem))]
[JsonSerializable(typeof(ErrorCorrectionOptions))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}