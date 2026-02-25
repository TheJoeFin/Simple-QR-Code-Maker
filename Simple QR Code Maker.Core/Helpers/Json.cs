using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Simple_QR_Code_Maker.Core.Helpers;

public static class Json
{
    private static readonly JsonSerializerOptions _options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static async Task<T> ToObjectAsync<T>(string value)
    {
        return await Task.Run<T>(() =>
        {
            return JsonSerializer.Deserialize<T>(value, _options);
        });
    }

    public static async Task<string> StringifyAsync(object value)
    {
        return await Task.Run<string>(() =>
        {
            return JsonSerializer.Serialize(value, _options);
        });
    }
}
