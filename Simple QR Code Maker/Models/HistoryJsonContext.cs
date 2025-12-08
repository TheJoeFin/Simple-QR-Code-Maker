using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Models;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ObservableCollection<HistoryItem>))]
[JsonSerializable(typeof(HistoryItem))]
internal partial class HistoryJsonContext : JsonSerializerContext
{
}