using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Models;

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never, Converters = [typeof(ColorJsonConverter), typeof(NullableColorJsonConverter)])]
[JsonSerializable(typeof(ObservableCollection<BrandItem>))]
[JsonSerializable(typeof(BrandItem))]
internal partial class BrandJsonContext : JsonSerializerContext
{
}
