using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Models;

public static class BrandJsonSerializerOptions
{
    private static JsonSerializerOptions? _options;

    public static JsonSerializerOptions Options
    {
        get
        {
            _options ??= new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    Converters =
                    {
                        new ColorJsonConverter(),
                        new NullableColorJsonConverter()
                    },
                    TypeInfoResolver = BrandJsonContext.Default
                };
            return _options;
        }
    }
}
