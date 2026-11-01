using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Core.Tests.Workflows;

[TestClass]
public sealed class QrDocumentHelperTests
{
    [TestMethod]
    public void PerLineModeRecognizesAllNewlinesAndRetainsDuplicates()
    {
        CollectionAssert.AreEqual(new[] { "one", "two", "three", "one" },
            QrDocumentHelper.EnumerateTexts(" one\r\ntwo\r \nthree\none ", MultiLineCodeMode.OneLineOneCode).ToArray());
    }

    [TestMethod]
    public void SingleCodeModePreservesInteriorNewlines()
    {
        CollectionAssert.AreEqual(new[] { "one\r\ntwo\rthree" },
            QrDocumentHelper.EnumerateTexts(" one\r\ntwo\rthree ", MultiLineCodeMode.MultilineOneCode).ToArray());
        Assert.IsEmpty(QrDocumentHelper.EnumerateTexts(" \r\n ", MultiLineCodeMode.MultilineOneCode).ToArray());
    }

    [TestMethod]
    [DataRow("WIFI:S:Network;T:nopass;;", QrContentKind.WiFi)]
    [DataRow("mailto:user@example.com", QrContentKind.Email)]
    [DataRow("BEGIN:VCARD\rVERSION:3.0\rFN:Contact\rEND:VCARD", QrContentKind.VCard)]
    [DataRow("https://example.com", QrContentKind.PlainText)]
    [DataRow("WIFI:malformed", QrContentKind.PlainText)]
    [DataRow(null, QrContentKind.PlainText)]
    public void DecodedContentClassificationUsesActualBuilderParsers(string? text, QrContentKind expected)
        => Assert.AreEqual(expected, QrDocumentHelper.ClassifyContent(text));

    [TestMethod]
    [DataRow("https://www.Example.com/", "example.com", true)]
    [DataRow("http://example.com/path", "https://example.com/", true)]
    [DataRow("https://example.com:8080/path", "example.com", true)]
    [DataRow("https://example.com?q=x", "example.com", true)]
    [DataRow("https://example.com#fragment", "example.com", true)]
    [DataRow("https://example.com.evil.test", "example.com", false)]
    [DataRow("https://example.com@evil.test", "example.com", false)]
    [DataRow("https://example.com:443@evil.test", "example.com", false)]
    [DataRow("https://example.com/path@contact", "example.com", true)]
    [DataRow("https://notexample.com", "example.com", false)]
    [DataRow("https://example.com/pathology", "example.com/path", false)]
    [DataRow("https://example.com", "", false)]
    public void BrandMatchingRequiresRealPatternBoundary(string content, string pattern, bool expected)
        => Assert.AreEqual(expected, QrDocumentHelper.MatchesBrandUrl(content, pattern));
}
