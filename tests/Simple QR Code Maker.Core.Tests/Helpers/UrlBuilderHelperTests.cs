using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class UrlBuilderHelperTests
{
    [TestMethod]
    public void ParsesIndependentUriExample()
    {
        UrlBuilderLine line = UrlBuilderHelper.ParseLine(" https://example.com/a%20b/%E9%9B%AA?x=one%26two&flag#hello%20world ");
        Assert.AreEqual("https", line.Scheme);
        Assert.AreEqual("example.com", line.Authority);
        Assert.AreEqual("/a b/雪", line.Path);
        Assert.AreEqual("hello world", line.Fragment);
        Assert.IsTrue(line.UsesAuthorityDelimiter);
        Assert.AreEqual(2, line.QueryParameters.Count);
        Assert.AreEqual("x", line.QueryParameters[0].Name);
        Assert.AreEqual("one&two", line.QueryParameters[0].Value);
        Assert.AreEqual(string.Empty, line.QueryParameters[1].Value);
    }

    [TestMethod]
    public void SerializesIndependentModelWithReservedAndUnicodeCharacters()
    {
        UrlBuilderLine line = new()
        {
            Scheme = " https: ",
            Authority = "example.com",
            Path = "a b/雪",
            Fragment = "a#😊",
            QueryParameters = [new("a&b", "x=y+z"), new("empty", ""), new("", "value"), new("", "")],
        };
        Assert.AreEqual("https://example.com/a%20b/%E9%9B%AA?a%26b=x%3Dy%2Bz&empty=&=value#a%23%F0%9F%98%8A", UrlBuilderHelper.SerializeLine(line));
    }

    [TestMethod]
    [DataRow("https://example.com/path?x=1&x=2#top", "https://example.com/path?x=1&x=2#top")]
    [DataRow("//example.com/a", "//example.com/a")]
    [DataRow("file:///folder/a%20b", "file:///folder/a%20b")]
    [DataRow("///relative/path", "///relative/path")]
    [DataRow("example.com/a", "example.com/a")]
    [DataRow("/relative/a%20b", "/relative/a%20b")]
    [DataRow("urn:example:path/child", "urn:example%3Apath/child")]
    [DataRow("mailto:me@example.com", "mailto:me%40example.com")]
    [DataRow("https://example.com/?flag&x=a=b&plus=a+b", "https://example.com/?flag=&x=a%3Db&plus=a%2Bb")]
    [DataRow("https://example.com/%zz?x=%zz", "https://example.com/%25zz?x=%25zz")]
    public void ProducesStableCanonicalRoundtrip(string input, string expected)
    {
        string actual = UrlBuilderHelper.SerializeLine(UrlBuilderHelper.ParseLine(input));
        Assert.AreEqual(expected, actual);
        Assert.AreEqual(expected, UrlBuilderHelper.SerializeLine(UrlBuilderHelper.ParseLine(actual)));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \r\n\t")]
    [DataRow(null)]
    public void EmptyTextProvidesOneEditableBlankLine(string? text)
    {
        IReadOnlyList<UrlBuilderLine> lines = UrlBuilderHelper.ParseText(text);
        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(string.Empty, UrlBuilderHelper.SerializeText(lines));
    }

    [TestMethod]
    public void SplitsAllNewlineFormsAndOmitsBlankLines()
    {
        IReadOnlyList<UrlBuilderLine> lines = UrlBuilderHelper.ParseText("https://a.test\r\n \rhttps://b.test\nhttps://c.test\n");
        Assert.AreEqual(3, lines.Count);
        Assert.AreEqual("https://a.test\r\nhttps://b.test\r\nhttps://c.test", UrlBuilderHelper.SerializeText(lines));
    }
}
