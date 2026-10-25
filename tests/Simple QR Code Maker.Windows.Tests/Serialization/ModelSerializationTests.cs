using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Models;
using Windows.UI;
using ZXing;

namespace Simple_QR_Code_Maker.Windows.Tests.Serialization;

[TestClass]
public sealed class ModelSerializationTests
{
    [TestMethod]
    public void HistoryUsesRealGeneratedMetadataAndPreservesCompleteDesign()
    {
        HistoryItem original = new()
        {
            CodesContent = "BEGIN:VCARD\r\nFN:Zoë 😀\r\nEND:VCARD",
            SavedDateTime = new DateTime(2024, 2, 29, 12, 34, 56, DateTimeKind.Utc),
            ContentKind = QrContentKind.VCard,
            MultiLineCodeModeOverride = MultiLineCodeMode.MultilineOneCode,
            CommonBrandCodes = ["https://example.test/α", "two"],
            Foreground = Color.FromArgb(128, 12, 34, 56),
            Background = Color.FromArgb(0, 210, 220, 230),
            ErrorCorrectionLevelAsString = "H",
            Format = BarcodeFormat.QR_CODE,
            LogoImagePath = @"C:\synthetic\logo.png",
            LogoEmoji = "😀",
            LogoEmojiStyle = EmojiLogoStyle.ThreeDimensional,
            LogoSizePercentage = 17.5,
            LogoPaddingPixels = -2,
            FramePreset = QrFramePreset.CornerCallout,
            FrameTextSource = QrFrameTextSource.Manual,
            FrameText = "Scan <me>",
        };
        string json = JsonSerializer.Serialize(original, HistoryJsonContext.Default.HistoryItem);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.AreEqual("#800C2238", document.RootElement.GetProperty("Foreground").GetString());
        Assert.AreEqual("#00D2DCE6", document.RootElement.GetProperty("Background").GetString());
        Assert.AreEqual("VCard", document.RootElement.GetProperty("ContentKind").GetString());
        Assert.AreEqual("CornerCallout", document.RootElement.GetProperty("FramePreset").GetString());
        Assert.IsFalse(document.RootElement.TryGetProperty("DisplayText", out _));
        Assert.IsFalse(document.RootElement.TryGetProperty("ErrorCorrection", out _));
        HistoryItem restored = JsonSerializer.Deserialize(json, HistoryJsonContext.Default.HistoryItem)!;
        Assert.AreEqual(json, JsonSerializer.Serialize(restored, HistoryJsonContext.Default.HistoryItem));
        Assert.AreEqual(original.SavedDateTime, restored.SavedDateTime);
        Assert.AreEqual(original.CodesContent, restored.CodesContent);
        CollectionAssert.AreEqual(original.CommonBrandCodes, restored.CommonBrandCodes);
        Assert.AreEqual(original.Foreground, restored.Foreground);
        Assert.AreEqual(original.Background, restored.Background);
        Assert.AreEqual(original.LogoEmojiStyle, restored.LogoEmojiStyle);
        Assert.AreEqual(original.FrameText, restored.FrameText);
        Assert.AreSame(HistoryJsonContext.Default, HistoryJsonSerializerOptions.Options.TypeInfoResolver);
        Assert.AreEqual(original.CodesContent, JsonSerializer.Deserialize<HistoryItem>(json, HistoryJsonSerializerOptions.Options)!.CodesContent);
    }

    [TestMethod]
    public void HistoricalMissingAndUnknownFieldsKeepBackwardCompatibleDefaults()
    {
        const string json = """{"CodesContent":"old payload","Foreground":"#FF123456","UnknownFutureField":{"Enabled":true}}""";
        HistoryItem history = JsonSerializer.Deserialize(json, HistoryJsonContext.Default.HistoryItem)!;
        Assert.AreEqual("old payload", history.CodesContent);
        Assert.AreEqual(QrContentKind.PlainText, history.ContentKind);
        Assert.IsNull(history.MultiLineCodeModeOverride);
        Assert.HasCount(0, history.CommonBrandCodes);
        Assert.AreEqual(Color.FromArgb(255, 255, 255, 255), history.Background);
        Assert.AreEqual("M", history.ErrorCorrection.ToString());
        Assert.AreEqual(15, history.LogoSizePercentage);
        Assert.AreEqual(4, history.LogoPaddingPixels);
        Assert.IsNull(history.LogoEmojiStyle);
        Assert.AreEqual(QrFramePreset.None, history.FramePreset);
        Assert.AreEqual(QrFrameTextSource.Manual, history.FrameTextSource);
        BrandItem brand = JsonSerializer.Deserialize("""{"Name":"Legacy","IsDefault":true}""", BrandJsonContext.Default.BrandItem)!;
        Assert.AreEqual("Legacy", brand.Name);
        Assert.IsTrue(brand.IsDefault);
        Assert.IsNull(brand.Foreground);
        Assert.IsNull(brand.Background);
        Assert.IsNull(brand.FramePreset);
        Assert.IsNull(brand.LogoPaddingPixels);
    }

    [TestMethod]
    public void BrandPreservesNullsAlphaEnumsAndOptionalValuesThroughGeneratedContext()
    {
        BrandItem original = new()
        {
            Name = "Synthetic",
            CreatedDateTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Foreground = Color.FromArgb(1, 2, 3, 4),
            Background = null,
            UrlContent = "https://example.test",
            ContentKind = QrContentKind.Email,
            MultiLineCodeModeOverride = MultiLineCodeMode.OneLineOneCode,
            ErrorCorrectionLevelAsString = null,
            LogoEmoji = "🌈",
            LogoEmojiStyle = EmojiLogoStyle.Flat,
            LogoSizePercentage = 12.25,
            LogoPaddingPixels = 0,
            FramePreset = QrFramePreset.None,
            FrameText = null,
            IsDefault = true,
        };
        string json = JsonSerializer.Serialize(original, BrandJsonContext.Default.BrandItem);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.AreEqual("#01020304", document.RootElement.GetProperty("Foreground").GetString());
        Assert.AreEqual(JsonValueKind.Null, document.RootElement.GetProperty("Background").ValueKind);
        Assert.AreEqual("Flat", document.RootElement.GetProperty("LogoEmojiStyle").GetString());
        Assert.AreEqual("None", document.RootElement.GetProperty("FramePreset").GetString());
        BrandItem copy = JsonSerializer.Deserialize<BrandItem>(json, BrandJsonSerializerOptions.Options)!;
        Assert.AreEqual(original.Foreground, copy.Foreground);
        Assert.IsNull(copy.Background);
        Assert.AreEqual(json, JsonSerializer.Serialize(copy, BrandJsonContext.Default.BrandItem));
        Assert.AreSame(BrandJsonContext.Default, BrandJsonSerializerOptions.Options.TypeInfoResolver);
    }

    [TestMethod]
    [DataRow("#00000000", 0, 0, 0, 0)]
    [DataRow("#FFffffff", 255, 255, 255, 255)]
    [DataRow("#80402010", 128, 64, 32, 16)]
    [DataRow("short", 255, 0, 0, 0)]
    [DataRow("", 255, 0, 0, 0)]
    public void ColorConverterHandlesAlphaCaseAndLegacyInvalidLengths(string value, int a, int r, int g, int b)
    {
        string json = $$"""{"Foreground":"{{value}}"}""";
        HistoryItem history = JsonSerializer.Deserialize(json, HistoryJsonContext.Default.HistoryItem)!;
        Assert.AreEqual(Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b), history.Foreground);
        BrandItem brand = JsonSerializer.Deserialize(json, BrandJsonContext.Default.BrandItem)!;
        Assert.AreEqual(history.Foreground, brand.Foreground);
    }

    [TestMethod]
    public void NullableColorNullRemainsNullAndCollectionsUseGeneratedMetadata()
    {
        BrandItem brand = JsonSerializer.Deserialize("""{"Foreground":null,"Background":null}""", BrandJsonContext.Default.BrandItem)!;
        Assert.IsNull(brand.Foreground);
        Assert.IsNull(brand.Background);
        ObservableCollection<BrandItem> brands = [brand, new() { Name = "second", Foreground = Color.FromArgb(255, 5, 6, 7) }];
        string json = JsonSerializer.Serialize(brands, BrandJsonContext.Default.ObservableCollectionBrandItem);
        var brandCopy = JsonSerializer.Deserialize(json, BrandJsonContext.Default.ObservableCollectionBrandItem)!;
        Assert.HasCount(2, brandCopy);
        Assert.IsNull(brandCopy[0].Foreground);
        ObservableCollection<HistoryItem> histories = [new() { CodesContent = "one" }, new() { CodesContent = "two" }];
        json = JsonSerializer.Serialize(histories, HistoryJsonContext.Default.ObservableCollectionHistoryItem);
        Assert.AreEqual("two", JsonSerializer.Deserialize(json, HistoryJsonContext.Default.ObservableCollectionHistoryItem)![1].CodesContent);
    }

    [TestMethod]
    public void AllEnumValuesAndNullableVariantsRoundTripWithoutReflection()
    {
        foreach (QrContentKind kind in Enum.GetValues<QrContentKind>())
        {
            HistoryItem value = new() { ContentKind = kind };
            string json = JsonSerializer.Serialize(value, HistoryJsonContext.Default.HistoryItem);
            Assert.AreEqual(kind, JsonSerializer.Deserialize(json, HistoryJsonContext.Default.HistoryItem)!.ContentKind);
        }
        foreach (EmojiLogoStyle style in Enum.GetValues<EmojiLogoStyle>())
        {
            BrandItem value = new() { LogoEmojiStyle = style };
            string json = JsonSerializer.Serialize(value, BrandJsonContext.Default.BrandItem);
            Assert.AreEqual(style, JsonSerializer.Deserialize(json, BrandJsonContext.Default.BrandItem)!.LogoEmojiStyle);
        }
        foreach (QrFramePreset frame in Enum.GetValues<QrFramePreset>())
        {
            foreach (QrFrameTextSource source in Enum.GetValues<QrFrameTextSource>())
            {
                HistoryItem value = new() { FramePreset = frame, FrameTextSource = source };
                string json = JsonSerializer.Serialize(value, HistoryJsonContext.Default.HistoryItem);
                HistoryItem copy = JsonSerializer.Deserialize(json, HistoryJsonContext.Default.HistoryItem)!;
                Assert.AreEqual(frame, copy.FramePreset);
                Assert.AreEqual(source, copy.FrameTextSource);
            }
        }
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize("""{"ContentKind":"NotAContentKind"}""", HistoryJsonContext.Default.HistoryItem));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize("""{"LogoEmojiStyle":"NotAStyle"}""", BrandJsonContext.Default.BrandItem));
    }

    [TestMethod]
    public void DecodingHistoryPersistsOnlyStoredDataAndSupportsOldDocuments()
    {
        foreach (DecodingSourceKind source in Enum.GetValues<DecodingSourceKind>())
        {
            DecodingHistoryItem item = new()
            {
                SourceKind = source,
                SavedImagePath = "synthetic.png",
                SourceFilePath = source == DecodingSourceKind.File ? @"C:\synthetic\source.png" : null,
                DecodedTexts = ["one", "漢字 😀"],
                SavedDateTime = new DateTime(2021, 10, 11, 12, 13, 14, DateTimeKind.Utc),
            };
            string json = JsonSerializer.Serialize(item, DecodingHistoryJsonContext.Default.DecodingHistoryItem);
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.AreEqual(source.ToString(), document.RootElement.GetProperty("SourceKind").GetString());
            Assert.IsFalse(document.RootElement.TryGetProperty("HasSourceFile", out _));
            Assert.IsFalse(document.RootElement.TryGetProperty("SaveDateAsString", out _));
            Assert.IsFalse(document.RootElement.TryGetProperty("FirstDecodedText", out _));
            DecodingHistoryItem copy = JsonSerializer.Deserialize<DecodingHistoryItem>(json, DecodingHistoryJsonSerializerOptions.Options)!;
            CollectionAssert.AreEqual(item.DecodedTexts, copy.DecodedTexts);
            Assert.AreEqual(source, copy.SourceKind);
            Assert.AreEqual(item.SavedDateTime, copy.SavedDateTime);
        }
        DecodingHistoryItem legacy = JsonSerializer.Deserialize("{}", DecodingHistoryJsonContext.Default.DecodingHistoryItem)!;
        Assert.AreEqual(DecodingSourceKind.File, legacy.SourceKind);
        Assert.HasCount(0, legacy.DecodedTexts);
        Assert.AreEqual(string.Empty, legacy.SavedImagePath);
        Assert.IsNull(legacy.SourceFilePath);
        ObservableCollection<DecodingHistoryItem> items = [legacy];
        string collectionJson = JsonSerializer.Serialize(items, DecodingHistoryJsonContext.Default.ObservableCollectionDecodingHistoryItem);
        Assert.HasCount(1, JsonSerializer.Deserialize(collectionJson, DecodingHistoryJsonContext.Default.ObservableCollectionDecodingHistoryItem)!);
    }
}
