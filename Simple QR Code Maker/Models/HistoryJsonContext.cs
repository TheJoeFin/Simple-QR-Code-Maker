using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using ZXing;

namespace Simple_QR_Code_Maker.Models;

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never, Converters = [typeof(ColorJsonConverter)])]
[JsonSerializable(typeof(ObservableCollection<HistoryItem>))]
[JsonSerializable(typeof(HistoryItem))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(QrContentKind))]
[JsonSerializable(typeof(MultiLineCodeMode))]
[JsonSerializable(typeof(MultiLineCodeMode?))]
[JsonSerializable(typeof(BarcodeFormat))]
[JsonSerializable(typeof(EmojiLogoStyle))]
[JsonSerializable(typeof(EmojiLogoStyle?))]
[JsonSerializable(typeof(QrFramePreset))]
[JsonSerializable(typeof(QrFrameTextSource))]
internal partial class HistoryJsonContext : JsonSerializerContext
{
}