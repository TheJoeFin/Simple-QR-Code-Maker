using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using ZXing.QrCode.Internal;
using WinColor = Windows.UI.Color;

namespace Simple_QR_Code_Maker.Windows.Tests.Models;

[TestClass]
public sealed class DesignAndModelTests
{
    [TestMethod]
    public void HistoryEqualityAndHashingAgreeForInvariantEquivalentPayloads()
    {
        HistoryItem first = new() { CodesContent = "café", Foreground = WinColor.FromArgb(255, 0, 0, 0) };
        HistoryItem same = new() { CodesContent = "cafe\u0301", Foreground = WinColor.FromArgb(255, 255, 0, 0) };
        Assert.IsTrue(first.Equals(same));
        Assert.IsTrue(first.Equals((object)same));
        Assert.AreEqual(first.GetHashCode(), same.GetHashCode(), "Invariant-culture equality must use the same hash comparer.");
        Assert.HasCount(1, new HashSet<HistoryItem> { first, same });
        Assert.IsFalse(first.Equals(new HistoryItem { CodesContent = "CAFÉ" }));
        Assert.IsFalse(first.Equals(null));
        Assert.IsFalse(first.Equals("café"));
        HistoryItem nullPayload = new() { CodesContent = null! };
        Assert.IsTrue(nullPayload.Equals(new HistoryItem { CodesContent = null! }));
        Assert.AreEqual(0, nullPayload.GetHashCode());
    }

    [TestMethod]
    public void BrandEqualityIsOrdinalIgnoreCaseWithMatchingHash()
    {
        BrandItem first = new() { Name = "Example", IsDefault = true };
        BrandItem second = new() { Name = "EXAMPLE", IsDefault = false };
        Assert.IsTrue(first.Equals(second));
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        Assert.HasCount(1, new HashSet<BrandItem> { first, second });
        Assert.IsFalse(first.Equals(new BrandItem { Name = " Example " }));
        Assert.IsFalse(first.Equals(null));
        Assert.IsFalse(first.Equals("Example"));
        BrandItem nullName = new() { Name = null! };
        Assert.IsTrue(nullName.Equals(new BrandItem { Name = null! }));
        Assert.AreEqual(0, nullName.GetHashCode());
    }

    [TestMethod]
    [DataRow("L", "L")]
    [DataRow("M", "M")]
    [DataRow("Q", "Q")]
    [DataRow("H", "H")]
    [DataRow("old unknown", "M")]
    public void CorrectionPropertySupportsStoredStringsAndFallback(string stored, string expected)
    {
        HistoryItem history = new() { ErrorCorrectionLevelAsString = stored };
        Assert.AreEqual(expected, history.ErrorCorrection.ToString());
        history.ErrorCorrection = ErrorCorrectionLevel.Q;
        Assert.AreEqual("Q", history.ErrorCorrectionLevelAsString);
    }

    [TestMethod]
    public void DesignHistoryRoundTripCopiesEveryFieldAndDoesNotAliasArrays()
    {
        QrCodeDesignState state = CreateState();
        HistoryItem history = QrCodeDesignStateMapper.ToHistoryItem(state);
        QrCodeDesignState restored = QrCodeDesignStateMapper.FromHistoryItem(history);
        Assert.AreEqual(state with { CommonBrandCodes = restored.CommonBrandCodes }, restored);
        CollectionAssert.AreEqual(state.CommonBrandCodes, history.CommonBrandCodes);
        Assert.AreNotSame(state.CommonBrandCodes, history.CommonBrandCodes);
        Assert.AreNotSame(history.CommonBrandCodes, restored.CommonBrandCodes);
        history.CommonBrandCodes[0] = "changed";
        Assert.AreEqual("https://example.test", state.CommonBrandCodes[0]);
        Assert.AreEqual("https://example.test", restored.CommonBrandCodes[0]);
        history.CommonBrandCodes = null!;
        Assert.HasCount(0, QrCodeDesignStateMapper.FromHistoryItem(history).CommonBrandCodes);
    }

    [TestMethod]
    public void BrandMapperIncludesSelectedDesignAndTrimsNameAndCaption()
    {
        QrCodeDesignState state = CreateState();
        BrandItem brand = QrCodeDesignStateMapper.ToBrandItem(state, "  Synthetic brand  ", new() { IncludeUrl = true });
        Assert.AreEqual("Synthetic brand", brand.Name);
        Assert.AreEqual(state.Foreground, brand.Foreground);
        Assert.AreEqual(state.Background, brand.Background);
        Assert.AreEqual(state.CodesContent, brand.UrlContent);
        Assert.AreEqual(state.ContentKind, brand.ContentKind);
        Assert.AreEqual(state.MultiLineCodeModeOverride, brand.MultiLineCodeModeOverride);
        Assert.AreEqual("H", brand.ErrorCorrectionLevelAsString);
        Assert.AreEqual(state.LogoImagePath, brand.LogoImagePath);
        Assert.AreEqual(state.LogoEmoji, brand.LogoEmoji);
        Assert.AreEqual(state.LogoEmojiStyle, brand.LogoEmojiStyle);
        Assert.AreEqual(state.LogoSizePercentage, brand.LogoSizePercentage);
        Assert.AreEqual(state.LogoPaddingPixels, brand.LogoPaddingPixels);
        Assert.AreEqual(state.FramePreset, brand.FramePreset);
        Assert.AreEqual(state.FrameTextSource, brand.FrameTextSource);
        Assert.AreEqual(state.Appearance, brand.Appearance);
        Assert.AreEqual(state.QrPaddingModules, brand.QrPaddingModules);
        Assert.AreEqual("Scan me", brand.FrameText);
        BrandItem withoutFrame = QrCodeDesignStateMapper.ToBrandItem(state with { FramePreset = QrFramePreset.None }, "name", new());
        Assert.IsNull(withoutFrame.FrameText);
    }

    [TestMethod]
    [DataRow("foreground")]
    [DataRow("background")]
    [DataRow("url")]
    [DataRow("logo")]
    [DataRow("correction")]
    [DataRow("frame")]
    public void EachBrandIncludeFlagIsIndependent(string excluded)
    {
        BrandCreationOptions options = new()
        {
            IncludeForeground = excluded != "foreground",
            IncludeBackground = excluded != "background",
            IncludeUrl = excluded != "url",
            IncludeCenterImage = excluded != "logo",
            IncludeErrorCorrection = excluded != "correction",
            IncludeFrame = excluded != "frame",
        };
        BrandItem brand = QrCodeDesignStateMapper.ToBrandItem(CreateState(), "brand", options);
        Assert.AreEqual(excluded == "foreground", brand.Foreground is null);
        Assert.AreEqual(excluded == "background", brand.Background is null);
        Assert.AreEqual(excluded == "url", brand.UrlContent is null);
        Assert.AreEqual(excluded == "correction", brand.ErrorCorrectionLevelAsString is null);
        Assert.AreEqual(excluded == "logo", brand.LogoImagePath is null);
        Assert.AreEqual(excluded == "logo", brand.LogoEmoji is null);
        Assert.AreEqual(excluded == "logo", brand.LogoEmojiStyle is null);
        Assert.AreEqual(excluded == "logo", brand.LogoSizePercentage is null);
        Assert.AreEqual(excluded == "logo", brand.LogoPaddingPixels is null);
        Assert.AreEqual(excluded == "frame", brand.FramePreset is null);
        Assert.AreEqual(excluded == "frame", brand.FrameText is null);
        Assert.AreEqual(excluded == "frame", brand.Appearance is null);
        Assert.AreEqual(excluded == "frame", brand.QrPaddingModules is null);
    }

    [TestMethod]
    public void RenderSnapshotOwnsAnIndependentLogoAndPreservesSettings()
    {
        using Bitmap source = new(3, 2);
        source.SetPixel(1, 1, Color.Red);
        using QrRenderSettingsSnapshot snapshot = QrRenderSettingsSnapshot.Create(ErrorCorrectionLevel.Q, Color.Navy, Color.White, source, 12.5, -3, "<svg/>", 4, QrFramePreset.BottomLabel, QrFrameTextSource.Manual, "caption");
        source.SetPixel(1, 1, Color.Blue);
        Assert.AreNotSame(source, snapshot.LogoImage);
        Assert.AreEqual(Color.Red.ToArgb(), snapshot.LogoImage!.GetPixel(1, 1).ToArgb());
        Assert.AreEqual(ErrorCorrectionLevel.Q, snapshot.ErrorCorrectionLevel);
        Assert.AreEqual(Color.Navy, snapshot.ForegroundColor);
        Assert.AreEqual(Color.White, snapshot.BackgroundColor);
        Assert.AreEqual(12.5, snapshot.LogoSizePercentage);
        Assert.AreEqual(-3, snapshot.LogoPaddingPixels);
        Assert.AreEqual("<svg/>", snapshot.LogoSvgContent);
        Assert.AreEqual(4, snapshot.QrPaddingModules);
        Assert.AreEqual(QrFramePreset.BottomLabel, snapshot.FramePreset);
        Assert.AreEqual("caption", snapshot.FrameText);
        Assert.IsNull(snapshot.CreateCommonBrandOverride(new("https://example.test")));
        Assert.ThrowsExactly<InvalidOperationException>(() => snapshot.CreateCommonBrandOverride(new("https://example.test", UseCommonBrand: true)));
        snapshot.Dispose();
        Assert.AreEqual(Color.Blue.ToArgb(), source.GetPixel(1, 1).ToArgb(), "Snapshot disposal must not dispose the caller's bitmap.");
    }

    [TestMethod]
    [DataRow("HTTPS://WWW.YOUTUBE.COM/watch?v=123", "YouTube")]
    [DataRow("youtu.be/test", "YouTube")]
    [DataRow("https://google.com/maps/place/test", "Google Maps")]
    [DataRow("https://github.com./test", "GitHub")]
    [DataRow("https://github.com.evil.test/test", null)]
    [DataRow("https://evilgithub.com/test", null)]
    [DataRow("https://user@github.com/test", null)]
    [DataRow("ftp://github.com/test", null)]
    [DataRow("https://google.com/mapsfake", null)]
    [DataRow("text github.com test", null)]
    [DataRow("", null)]
    public void CommonBrandMatchingRequiresRealHostAndPathBoundaries(string payload, string? expected)
        => Assert.AreEqual(expected, CommonBrandCatalog.FindMatching(payload)?.Name);

    [TestMethod]
    public void CommonBrandResourcesAndOverridesRemainAvailableFromExtractedAssembly()
    {
        foreach (CommonBrandPreset brand in CommonBrandCatalog.All)
            Assert.AreEqual("svg", XElement.Parse(brand.LoadLogoSvg()).Name.LocalName);

        QrCodeAppearance appearance = new() { ModuleStyle = QrModuleStyle.Dots, EyeStyle = QrEyeStyle.Rounded };
        using QrRenderSettingsSnapshot source = QrRenderSettingsSnapshot.Create(ErrorCorrectionLevel.L, Color.Red, Color.Blue, null, 30, -2, null, 4, QrFramePreset.BottomLabel, QrFrameTextSource.ContentSummary, "existing", appearance);
        using QrRenderSettingsSnapshot common = source.CreateCommonBrandOverride(new("https://github.com/example", UseCommonBrand: true))!;
        Assert.AreEqual(ErrorCorrectionLevel.H, common.ErrorCorrectionLevel);
        Assert.AreEqual(CommonBrandCatalog.FindMatching("github.com")!.Foreground, common.ForegroundColor);
        Assert.AreEqual(Color.White, common.BackgroundColor);
        Assert.IsNotNull(common.LogoImage);
        Assert.IsFalse(string.IsNullOrEmpty(common.LogoSvgContent));
        Assert.AreEqual(15, common.LogoSizePercentage);
        Assert.AreEqual(4, common.LogoPaddingPixels);
        Assert.AreEqual(source.QrPaddingModules, common.QrPaddingModules);
        Assert.AreEqual(source.FramePreset, common.FramePreset);
        Assert.AreEqual(source.FrameTextSource, common.FrameTextSource);
        Assert.AreEqual(source.FrameText, common.FrameText);
        Assert.AreEqual(appearance, source.Appearance);
        Assert.AreEqual(source.Appearance, common.Appearance);
    }

    [TestMethod]
    public void DecodingDisplayPropertiesHandleEmptySingleMultipleAndNonFileSources()
    {
        DecodingHistoryItem item = new();
        Assert.AreEqual("(no codes found)", item.FirstDecodedText);
        Assert.AreEqual(string.Empty, item.DecodedCountSuffix);
        Assert.AreEqual("Unknown source", item.SourceDisplayName);
        Assert.IsFalse(item.HasSourceFile);
        Assert.IsFalse(item.HasSavedImage);
        item.DecodedTexts = ["first", "second", "third"];
        Assert.AreEqual("first", item.FirstDecodedText);
        Assert.AreEqual(" (+2 more)", item.DecodedCountSuffix);
        item.SourceKind = DecodingSourceKind.Camera;
        Assert.AreEqual("Camera capture", item.SourceDisplayName);
        item.SourceKind = DecodingSourceKind.SnippingTool;
        Assert.AreEqual("Snipping Tool capture", item.SourceDisplayName);
        item.SourceKind = DecodingSourceKind.Clipboard;
        Assert.AreEqual("Clipboard image", item.SourceDisplayName);
    }

    private static QrCodeDesignState CreateState() => new()
    {
        CodesContent = "https://example.test/content",
        ContentKind = QrContentKind.PlainText,
        MultiLineCodeModeOverride = MultiLineCodeMode.MultilineOneCode,
        CommonBrandCodes = ["https://example.test"],
        Foreground = WinColor.FromArgb(128, 1, 2, 3),
        Background = WinColor.FromArgb(0, 4, 5, 6),
        ErrorCorrection = ErrorCorrectionLevel.H,
        LogoImagePath = "synthetic.png",
        LogoEmoji = "😀",
        LogoEmojiStyle = EmojiLogoStyle.Flat,
        LogoSizePercentage = 15.5,
        LogoPaddingPixels = -2.5,
        FramePreset = QrFramePreset.BottomLabel,
        FrameTextSource = QrFrameTextSource.Manual,
        FrameText = "  Scan me  ",
        Appearance = new()
        {
            ModuleStyle = QrModuleStyle.Dots,
            EyeStyle = QrEyeStyle.Rounded,
            FrameAccent = WinColor.FromArgb(255, 230, 100, 170),
            FrameGradientEnd = WinColor.FromArgb(255, 250, 190, 70),
            CaptionIcon = QrCaptionIcon.WiFi,
        },
        QrPaddingModules = 4,
    };
}
