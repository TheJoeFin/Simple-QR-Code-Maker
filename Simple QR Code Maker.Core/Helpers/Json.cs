using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Simple_QR_Code_Maker.Core.Helpers;

public static class Json
{
    public static T? ToObject<T>(string value)
        => ToObject<T>(value, CoreJsonContext.Default);

    public static T? ToObject<T>(string value, JsonSerializerContext context)
    {
        JsonTypeInfo typeInfo = context.GetTypeInfo(typeof(T))
            ?? throw new NotSupportedException($"JSON metadata is not registered for {typeof(T)}.");
        object? result = JsonSerializer.Deserialize(value, typeInfo);
        return result is T typedResult ? typedResult : default;
    }

    public static Task<T?> ToObjectAsync<T>(string value)
        => Task.Run(() => ToObject<T>(value));

    public static Task<T?> ToObjectAsync<T>(string value, JsonSerializerContext context)
        => Task.Run(() => ToObject<T>(value, context));

    public static string Stringify(object? value)
        => Stringify(value, CoreJsonContext.Default);

    public static string Stringify(object? value, JsonSerializerContext context)
    {
        if (value is null)
            return "null";

        JsonTypeInfo typeInfo = context.GetTypeInfo(value.GetType())
            ?? throw new NotSupportedException($"JSON metadata is not registered for {value.GetType()}.");
        return JsonSerializer.Serialize(value, typeInfo);
    }

    public static Task<string> StringifyAsync(object? value)
        => Task.Run(() => Stringify(value));

    public static Task<string> StringifyAsync(object? value, JsonSerializerContext context)
        => Task.Run(() => Stringify(value, context));
}
