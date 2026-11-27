using Humanizer;
using System.Text.Json.Serialization;

namespace Simple_QR_Code_Maker.Models;

public class DecodingHistoryItem
{
    public DateTime SavedDateTime { get; set; } = DateTime.Now;

    [JsonConverter(typeof(JsonStringEnumConverter<DecodingSourceKind>))]
    public DecodingSourceKind SourceKind { get; set; } = DecodingSourceKind.File;

    // Path to image copy stored in app local data
    public string SavedImagePath { get; set; } = string.Empty;

    // Original source file path (only set when SourceKind == File)
    public string? SourceFilePath { get; set; } = null;

    public List<string> DecodedTexts { get; set; } = [];

    [JsonIgnore]
    public string SaveDateAsString => SavedDateTime.Humanize();

    [JsonIgnore]
    public string SourceDisplayName => SourceKind switch
    {
        DecodingSourceKind.Camera => "Camera capture",
        DecodingSourceKind.SnippingTool => "Snipping Tool capture",
        DecodingSourceKind.Clipboard => "Clipboard image",
        _ => string.IsNullOrEmpty(SourceFilePath)
            ? "Unknown source"
            : Path.GetFileName(SourceFilePath),
    };

    [JsonIgnore]
    public string FirstDecodedText => DecodedTexts.Count > 0
        ? DecodedTexts[0]
        : "(no codes found)";

    [JsonIgnore]
    public string DecodedCountSuffix => DecodedTexts.Count > 1
        ? $" (+{DecodedTexts.Count - 1} more)"
        : string.Empty;

    [JsonIgnore]
    public bool HasSourceFile => SourceKind == DecodingSourceKind.File
        && !string.IsNullOrEmpty(SourceFilePath)
        && File.Exists(SourceFilePath);

    [JsonIgnore]
    public bool HasSavedImage => !string.IsNullOrEmpty(SavedImagePath)
        && File.Exists(SavedImagePath);
}
