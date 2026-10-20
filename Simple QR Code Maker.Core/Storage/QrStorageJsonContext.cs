#nullable enable

using Simple_QR_Code_Maker.Core.Storage.Models;
using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Core.Storage;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(QrDocument))]
[JsonSerializable(typeof(QrDesign))]
[JsonSerializable(typeof(QrDesignDocument))]
[JsonSerializable(typeof(QrSavedDesign))]
[JsonSerializable(typeof(List<QrSavedDesign>))]
[JsonSerializable(typeof(QrHistoryDocument))]
[JsonSerializable(typeof(QrHistoryEntry))]
[JsonSerializable(typeof(List<QrHistoryEntry>))]
[JsonSerializable(typeof(QrPrivacyDocument))]
[JsonSerializable(typeof(LogoAssetId))]
public sealed partial class QrStorageJsonContext : JsonSerializerContext
{
}
