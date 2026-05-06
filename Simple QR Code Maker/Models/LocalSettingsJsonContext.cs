using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Models;

[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(double?))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(MultiLineCodeMode))]
[JsonSerializable(typeof(MultiLineCodeMode?))]
[JsonSerializable(typeof(LaunchMode))]
[JsonSerializable(typeof(LaunchMode?))]
[JsonSerializable(typeof(QrFramePreset))]
[JsonSerializable(typeof(QrFramePreset?))]
[JsonSerializable(typeof(QrFrameTextSource))]
[JsonSerializable(typeof(QrFrameTextSource?))]
[JsonSerializable(typeof(PrintPageType))]
[JsonSerializable(typeof(PrintPageType?))]
[JsonSerializable(typeof(PrintPageLayout))]
[JsonSerializable(typeof(PrintPageLayout?))]
internal partial class LocalSettingsJsonContext : JsonSerializerContext { }
