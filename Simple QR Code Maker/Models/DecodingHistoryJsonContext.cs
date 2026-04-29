using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Models;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ObservableCollection<DecodingHistoryItem>))]
[JsonSerializable(typeof(DecodingHistoryItem))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(DecodingSourceKind))]
internal partial class DecodingHistoryJsonContext : JsonSerializerContext
{
}
