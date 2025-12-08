using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.UI;

namespace Simple_QR_Code_Maker.Models;

public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? hex = reader.GetString();
        if (string.IsNullOrEmpty(hex) || hex.Length != 9)
            return Color.FromArgb(255, 0, 0, 0);

        byte a = Convert.ToByte(hex[1..3], 16);
        byte r = Convert.ToByte(hex[3..5], 16);
        byte g = Convert.ToByte(hex[5..7], 16);
        byte b = Convert.ToByte(hex[7..9], 16);
        return Color.FromArgb(a, r, g, b);
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
    }
}
