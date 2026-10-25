using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Models;

[TestClass]
public sealed class QrFramePresetCatalogTests
{
    [TestMethod]
    public void CatalogHasExactlyOneOptionPerPreset()
    {
        CollectionAssert.AreEquivalent(Enum.GetValues<QrFramePreset>(), QrFramePresetCatalog.All.Select(static option => option.Preset).ToArray());
        Assert.IsTrue(QrFramePresetCatalog.All.All(static option => !string.IsNullOrWhiteSpace(option.Description) && !string.IsNullOrWhiteSpace(option.ShortDescription)));
    }

    [TestMethod]
    [DataRow(QrFramePreset.BottomLabel, "Scan Me")]
    [DataRow(QrFramePreset.RoundedFrame, "Scan Here")]
    [DataRow(QrFramePreset.CornerCallout, "Scan QR Code")]
    public void DefaultsApplyOnlyWhenTextIsEmpty(QrFramePreset preset, string expected)
    {
        Assert.AreEqual(expected, QrFramePresetCatalog.ResolveText(preset, null));
        Assert.AreEqual(expected, QrFramePresetCatalog.ResolveText(preset, " \r\n "));
        Assert.AreEqual("雪 😊", QrFramePresetCatalog.ResolveText(preset, " 雪 😊 "));
        Assert.AreEqual(preset, QrFramePresetCatalog.GetOption(preset).Preset);
    }

    [TestMethod]
    public void DisabledAndUnknownPresetHaveSafeFallback()
    {
        Assert.IsNull(QrFramePresetCatalog.ResolveText(QrFramePreset.None, "ignored"));
        Assert.AreEqual(QrFramePreset.None, QrFramePresetCatalog.GetOption((QrFramePreset)999).Preset);
        Assert.AreEqual(string.Empty, QrFramePresetCatalog.ResolveText((QrFramePreset)999, null));
    }
}
