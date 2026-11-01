using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class CsvParserTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void EmptyInputHasNoRows(string? text)
    {
        Assert.AreEqual(0, CsvParser.Parse(text!).Count);
    }

    [TestMethod]
    [DataRow("\r\n")]
    [DataRow("\n")]
    [DataRow("\r")]
    public void ParsesRowSeparatorsWithoutPhantomTrailingRow(string newline)
    {
        List<List<string>> rows = CsvParser.Parse("a,b" + newline + "c,d" + newline);
        Assert.AreEqual(2, rows.Count);
        CollectionAssert.AreEqual(new[] { "a", "b" }, rows[0]);
        CollectionAssert.AreEqual(new[] { "c", "d" }, rows[1]);
    }

    [TestMethod]
    public void ParsesIndependentQuotedExample()
    {
        List<List<string>> rows = CsvParser.Parse("name,note,empty\r\n\"雪,😊\",\"say \"\"hi\"\"\r\nnext\",");
        Assert.AreEqual(2, rows.Count);
        CollectionAssert.AreEqual(new[] { "name", "note", "empty" }, rows[0]);
        CollectionAssert.AreEqual(new[] { "雪,😊", "say \"hi\"\r\nnext", "" }, rows[1]);
    }

    [TestMethod]
    public void SerializesIndependentEscapingExample()
    {
        IReadOnlyList<string>[] rows = [new[] { "雪", "a,b", "a\"b", "a\rb\nc" }, new[] { "" }];
        Assert.AreEqual("雪,\"a,b\",\"a\"\"b\",\"a\rb\nc\"\r\n\"\"", CsvParser.Serialize(rows));
    }

    [TestMethod]
    [DataRow(',')]
    [DataRow(';')]
    [DataRow('\t')]
    public void RoundtripPreservesEmptyFieldsUnicodeQuotesAndEmbeddedNewlines(char delimiter)
    {
        IReadOnlyList<string>[] expected =
        [
            new[] { "", "日本語😊", "comma,semi;tab\t", "\"quoted\"", "CR\rLF\nCRLF\r\n" },
            new[] { "" },
            new[] { "", "" },
        ];
        List<List<string>> actual = CsvParser.Parse(CsvParser.Serialize(expected, delimiter), delimiter);
        Assert.AreEqual(expected.Length, actual.Count);
        for (int index = 0; index < expected.Length; index++)
            CollectionAssert.AreEqual(expected[index].ToArray(), actual[index]);
    }

    [TestMethod]
    public void PreservesQuotedEmptyRowAndIntentionalBlankRows()
    {
        Assert.AreEqual(1, CsvParser.Parse("\"\"").Count);
        Assert.AreEqual(2, CsvParser.Parse("\r\n\r\n").Count);
        CollectionAssert.AreEqual(new[] { "", "", "" }, CsvParser.Parse(",,")[0]);
        Assert.AreEqual(string.Empty, CsvParser.Serialize([]));
    }

    [TestMethod]
    public void UnterminatedQuotedFieldReturnsAvailableData()
    {
        CollectionAssert.AreEqual(new[] { "a", "unfinished\ntext" }, CsvParser.Parse("a,\"unfinished\ntext")[0]);
    }

    [TestMethod]
    [DataRow('"')]
    [DataRow('\r')]
    [DataRow('\n')]
    public void RejectsAmbiguousDelimiters(char delimiter)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CsvParser.Parse("a", delimiter));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CsvParser.Serialize([], delimiter));
    }

    [TestMethod]
    public void RejectsNullRows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CsvParser.Serialize(null!));
    }
}
