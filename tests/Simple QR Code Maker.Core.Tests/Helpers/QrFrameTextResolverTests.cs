using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class QrFrameTextResolverTests
{
    [TestMethod]
    [DataRow(QrFramePreset.None, QrFrameTextSource.ContentSummary)]
    [DataRow(QrFramePreset.BottomLabel, QrFrameTextSource.Manual)]
    public void ManualOrDisabledFrameReturnsManualTextUnmodified(QrFramePreset preset, QrFrameTextSource source)
    {
        Assert.AreEqual(" manual \n text ", QrFrameTextResolver.Resolve(preset, source, " manual \n text ", "summary", QrContentKind.PlainText));
    }

    [TestMethod]
    [DataRow("https://www.example.com/a%20b?q=private#anchor", "example.com/a b")]
    [DataRow("https://example.com:8443/a", "example.com:8443/a")]
    [DataRow("https://example.com:443/", "example.com")]
    [DataRow("https://example.com/this/path/is/too/long/to/fit/into/the/summary", "example.com")]
    [DataRow(" first \t line \r\nsecond line ", "first line")]
    [DataRow("\n\r\n雪 😊\rnext", "雪 😊")]
    public void ResolvesIndependentUriAndPlainTextSummaries(string input, string expected)
    {
        Assert.AreEqual(expected, Resolve(input));
    }

    [TestMethod]
    [DataRow(QrContentKind.VCard, "BEGIN:VCARD\nVERSION:3.0\nFN:Ada Lovelace\nEND:VCARD", "Ada Lovelace")]
    [DataRow(QrContentKind.WiFi, "WIFI:S:Cafe;T:nopass;;", "Cafe")]
    [DataRow(QrContentKind.Email, "mailto:a@test.com?subject=Hello%20World", "Hello World")]
    [DataRow(QrContentKind.Email, "mailto:a@test.com", "a@test.com")]
    public void UsesStructuredContentDisplayName(QrContentKind kind, string text, string expected)
    {
        Assert.AreEqual(expected, Resolve(text, kind));
    }

    [TestMethod]
    [DataRow(QrContentKind.PlainText, null)]
    [DataRow(QrContentKind.PlainText, " \r\n\t")]
    [DataRow(QrContentKind.VCard, "broken")]
    [DataRow(QrContentKind.WiFi, "broken")]
    [DataRow(QrContentKind.Email, "broken")]
    public void EmptyOrInvalidSummaryFallsBackToManual(QrContentKind kind, string? input)
    {
        Assert.AreEqual("fallback", Resolve(input, kind));
    }

    [TestMethod]
    public void TruncatesAtFortyFourCharactersWithoutSplittingUnicode()
    {
        Assert.AreEqual(new string('a', 44), Resolve(new string('a', 44)));
        Assert.AreEqual(new string('a', 41) + "...", Resolve(new string('a', 45)));
        Assert.AreEqual(new string('a', 40) + "...", Resolve(new string('a', 40) + "😊" + new string('b', 10)));
    }

    [TestMethod]
    public void MultilineModeStillUsesFirstNonemptyLine()
    {
        Assert.AreEqual("first line", QrFrameTextResolver.Resolve(QrFramePreset.RoundedFrame, QrFrameTextSource.ContentSummary,
            null, "\nfirst \t line\nsecond", QrContentKind.PlainText, MultiLineCodeMode.MultilineOneCode));
    }

    private static string? Resolve(string? text, QrContentKind kind = QrContentKind.PlainText)
    {
        return QrFrameTextResolver.Resolve(QrFramePreset.BottomLabel, QrFrameTextSource.ContentSummary, "fallback", text, kind);
    }
}
