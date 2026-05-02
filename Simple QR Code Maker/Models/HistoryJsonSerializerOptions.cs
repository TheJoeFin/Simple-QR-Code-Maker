using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Models;

public static class HistoryJsonSerializerOptions
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
                        new ColorJsonConverter()
                    },
                TypeInfoResolver = HistoryJsonContext.Default
            };
            return _options;
        }
    }
}
