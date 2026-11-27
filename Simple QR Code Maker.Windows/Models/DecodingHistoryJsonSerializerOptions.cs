using System.Text.Json;

namespace Simple_QR_Code_Maker.Models;

public static class DecodingHistoryJsonSerializerOptions
{
    public static JsonSerializerOptions Options { get; } = new(DecodingHistoryJsonContext.Default.Options);
}
