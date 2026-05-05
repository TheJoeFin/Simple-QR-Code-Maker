using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Models;

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never, Converters = [typeof(ColorJsonConverter)])]
[JsonSerializable(typeof(ObservableCollection<HistoryItem>))]
[JsonSerializable(typeof(HistoryItem))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class HistoryJsonContext : JsonSerializerContext
{
}