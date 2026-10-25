using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Models;

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never, Converters = [typeof(ColorJsonConverter), typeof(NullableColorJsonConverter)])]
[JsonSerializable(typeof(ObservableCollection<BrandItem>))]
[JsonSerializable(typeof(BrandItem))]
[JsonSerializable(typeof(QrContentKind))]
[JsonSerializable(typeof(MultiLineCodeMode))]
[JsonSerializable(typeof(MultiLineCodeMode?))]
[JsonSerializable(typeof(EmojiLogoStyle))]
[JsonSerializable(typeof(EmojiLogoStyle?))]
[JsonSerializable(typeof(QrFramePreset))]
[JsonSerializable(typeof(QrFramePreset?))]
[JsonSerializable(typeof(QrFrameTextSource))]
internal partial class BrandJsonContext : JsonSerializerContext
{
}
