using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Windows.UI;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Windows.Tests.Models;

[TestClass]
public sealed class QrDesignPresetTests
{
    [TestMethod]
    public void PresetsChangeOnlyVisualDesignAndKeepContentAndLogo()
    {
        QrCodeDesignState original = new()
        {
            CodesContent = "WIFI:T:WPA;S:Example;P:password;;",
            ContentKind = QrContentKind.WiFi,
            MultiLineCodeModeOverride = MultiLineCodeMode.MultilineOneCode,
            CommonBrandCodes = ["https://example.com"],
            Foreground = Color.FromArgb(255, 0, 0, 0),
            Background = Color.FromArgb(255, 255, 255, 255),
            ErrorCorrection = ErrorCorrectionLevel.H,
            LogoImagePath = "synthetic.png",
            LogoEmoji = "logo",
            LogoEmojiStyle = EmojiLogoStyle.Flat,
            LogoSizePercentage = 15,
            LogoPaddingPixels = 4,
        };
        foreach (QrDesignPreset preset in QrDesignPresetCatalog.All)
        {
            QrCodeDesignState styled = preset.ApplyTo(original);
            Assert.AreEqual(original.CodesContent, styled.CodesContent);
            Assert.AreEqual(original.ContentKind, styled.ContentKind);
            Assert.AreEqual(original.MultiLineCodeModeOverride, styled.MultiLineCodeModeOverride);
            CollectionAssert.AreEqual(original.CommonBrandCodes, styled.CommonBrandCodes);
            Assert.AreEqual(original.LogoImagePath, styled.LogoImagePath);
            Assert.AreEqual(original.LogoEmoji, styled.LogoEmoji);
            Assert.AreEqual(original.LogoEmojiStyle, styled.LogoEmojiStyle);
            Assert.AreEqual(original.LogoSizePercentage, styled.LogoSizePercentage);
            Assert.AreEqual(original.LogoPaddingPixels, styled.LogoPaddingPixels);
            Assert.AreEqual(original.ErrorCorrection, styled.ErrorCorrection);
            Assert.AreEqual(preset.Appearance, styled.Appearance);
            Assert.AreEqual(preset.Frame, styled.FramePreset);
            Assert.AreEqual(preset.Caption, styled.FrameText);
            Assert.AreEqual(QrFrameTextSource.Manual, styled.FrameTextSource);
            Assert.AreEqual(4, styled.QrPaddingModules);
            Assert.AreEqual(255, (int)styled.Background.A);
            Assert.IsTrue(ColorHelpers.GetContrastRatio(styled.Foreground, styled.Background) >= 7, preset.Name);
        }
        Assert.AreEqual(QrDesignPresetCatalog.All.Count, QrDesignPresetCatalog.All.Select(preset => preset.Name).Distinct().Count());
        Assert.AreEqual(new QrCodeAppearance(), original.Appearance);
        Assert.IsNull(original.QrPaddingModules);
    }

    [TestMethod]
    public void EveryPresetSurvivesHistoryAndOptionalBrandSerialization()
    {
        foreach (QrDesignPreset preset in QrDesignPresetCatalog.All)
        {
            HistoryItem history = new()
            {
                CodesContent = "example",
                Foreground = preset.Foreground,
                Background = preset.Background,
                FramePreset = preset.Frame,
                FrameText = preset.Caption,
                Appearance = preset.Appearance,
                QrPaddingModules = 4,
            };
            string json = JsonSerializer.Serialize(history, HistoryJsonContext.Default.HistoryItem);
            HistoryItem restored = JsonSerializer.Deserialize(json, HistoryJsonContext.Default.HistoryItem)!;
            Assert.AreEqual(history.Appearance, restored.Appearance);
            Assert.AreEqual(history.QrPaddingModules, restored.QrPaddingModules);
            Assert.AreEqual(history.FramePreset, restored.FramePreset);

            BrandItem brand = QrCodeDesignStateMapper.ToBrandItem(QrCodeDesignStateMapper.FromHistoryItem(restored), preset.Name, new());
            json = JsonSerializer.Serialize(brand, BrandJsonContext.Default.BrandItem);
            BrandItem restoredBrand = JsonSerializer.Deserialize(json, BrandJsonContext.Default.BrandItem)!;
            Assert.AreEqual(preset.Appearance, restoredBrand.Appearance);
            Assert.AreEqual(4, restoredBrand.QrPaddingModules);
        }
    }

    [TestMethod]
    public void EveryPatternEyeAndIconRoundTripsWithOptionalColors()
    {
        foreach (QrModuleStyle module in Enum.GetValues<QrModuleStyle>())
        foreach (QrEyeStyle eye in Enum.GetValues<QrEyeStyle>())
        foreach (QrCaptionIcon icon in Enum.GetValues<QrCaptionIcon>())
        {
            QrCodeAppearance appearance = new() { ModuleStyle = module, EyeStyle = eye, CaptionIcon = icon };
            string json = JsonSerializer.Serialize(appearance, HistoryJsonContext.Default.QrCodeAppearance);
            Assert.AreEqual(appearance, JsonSerializer.Deserialize(json, HistoryJsonContext.Default.QrCodeAppearance));
        }
    }

    [TestMethod]
    public void LegacyDocumentsKeepClassicStylesAndDoNotOverrideGlobalPadding()
    {
        HistoryItem history = JsonSerializer.Deserialize("""{"CodesContent":"legacy"}""", HistoryJsonContext.Default.HistoryItem)!;
        Assert.AreEqual(new QrCodeAppearance(), history.Appearance);
        Assert.IsNull(history.QrPaddingModules);
        BrandItem brand = JsonSerializer.Deserialize("""{"Name":"legacy","FramePreset":"BottomLabel"}""", BrandJsonContext.Default.BrandItem)!;
        Assert.IsNull(brand.Appearance);
        Assert.IsNull(brand.QrPaddingModules);
        history.Appearance = null!;
        Assert.AreEqual(new QrCodeAppearance(), QrCodeDesignStateMapper.FromHistoryItem(history).Appearance);
    }
}
