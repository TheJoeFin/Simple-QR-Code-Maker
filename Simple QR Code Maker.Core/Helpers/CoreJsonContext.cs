using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Core.Helpers;

[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class CoreJsonContext : JsonSerializerContext
{
}
