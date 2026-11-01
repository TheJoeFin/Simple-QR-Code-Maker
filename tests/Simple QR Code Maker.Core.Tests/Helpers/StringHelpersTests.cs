using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class StringHelpersTests
{
    [TestMethod]
    public void ReplacesReservedAndControlCharactersAndCollapsesDashes()
    {
        Assert.AreEqual("a-b-c-雪😊", "a /:*?\"<>|\\+,.=[]!@;\tb\r\nc-雪😊".ReplaceReservedCharacters());
        Assert.AreEqual("-A-B-C", "\0A\tB\r\nC".ReplaceReservedCharacters());
    }

    [TestMethod]
    [DataRow("", "QR-Code")]
    [DataRow("   ", "QR-Code")]
    [DataRow("\0\r\n", "QR-Code")]
    [DataRow(":", "QR-Code")]
    [DataRow("hello?", "hello")]
    [DataRow("hello-", "hello-")]
    [DataRow("-", "-")]
    [DataRow("雪😊", "雪😊")]
    [DataRow("CON", "CON-QR")]
    [DataRow("nul", "nul-QR")]
    [DataRow("COM1", "COM1-QR")]
    [DataRow("LPT9", "LPT9-QR")]
    [DataRow("COM10", "COM10")]
    public void ProducesUsableWindowsFilename(string input, string expected)
    {
        Assert.AreEqual(expected, input.ToSafeFileName());
    }

    [TestMethod]
    public void TruncatesOnlyAboveSixtyCharactersKeepingBothEnds()
    {
        string sixty = new('a', 60);
        Assert.AreEqual(sixty, sixty.ToSafeFileName());
        string longName = new string('a', 30) + "middle" + new string('z', 30);
        Assert.AreEqual(new string('a', 28) + "..." + new string('z', 28), longName.ToSafeFileName());
        Assert.AreEqual(59, longName.ToSafeFileName().Length);
    }

    [TestMethod]
    public void TruncationDoesNotSplitUnicodeSurrogatePairs()
    {
        string input = new string('a', 27) + "😊" + new string('b', 10) + "😊" + new string('z', 27);
        Assert.AreEqual(new string('a', 27) + "..." + new string('z', 27), input.ToSafeFileName());
    }

    [TestMethod]
    public void UsesContentSpecificDisplayNames()
    {
        Assert.AreEqual("Ada-vCard", VCardBuilderHelper.Serialize(new VCardBuilderState { FormattedName = "Ada" }).ToSafeFileName());
        Assert.AreEqual("Cafe-WiFi", "WIFI:S:Cafe;T:nopass;;".ToSafeFileName());
        Assert.AreEqual("Hello-Email", "mailto:a@test.com?subject=Hello".ToSafeFileName());
    }

    [TestMethod]
    public void NullFilenameInputIsRejectedExplicitly()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => StringHelpers.ToSafeFileName(null!));
    }
}
