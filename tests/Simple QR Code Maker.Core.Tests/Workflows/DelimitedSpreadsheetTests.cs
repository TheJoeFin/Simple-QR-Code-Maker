using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Core.Tests.Infrastructure;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Workflows;

[TestClass]
[TestCategory("Integration")]
public sealed class DelimitedSpreadsheetTests
{
    [TestMethod]
    [DataRow(',')]
    [DataRow('\t')]
    public async Task WritebackPreservesQuotedAndUnicodeValues(char delimiter)
    {
        using TemporaryDirectory directory = new();
        string path = Path.Combine(directory.Path, "data.txt");
        await File.WriteAllTextAsync(path, CsvParser.Serialize([new[] { "name", "value" }, new[] { "a\"b\r\nc", "\u6f22" }], delimiter));
        await DelimitedSpreadsheetHelper.WriteGeneratedIdsAsync(path, delimiter, new SpreadsheetIdWriteRequest
        {
            FirstRowIsHeader = true,
            CreateIdColumn = true,
            IdColumnIndex = 2,
            Rows = [new() { SourceRowIndex = 1, Value = "fixed-id" }],
        });
        SpreadsheetReadResult actual = await DelimitedSpreadsheetHelper.ReadAsync(path, delimiter);
        Assert.HasCount(2, actual.Rows);
        Assert.AreEqual(1, actual.Rows[1].SourceRowIndex);
        CollectionAssert.AreEqual(new[] { "a\"b\r\nc", "\u6f22", "fixed-id" }, actual.Rows[1].Cells.ToArray());
        Assert.IsEmpty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [TestMethod]
    public async Task InvalidWritebackPreservesOriginalAndLeavesNoTempFiles()
    {
        using TemporaryDirectory directory = new();
        string path = Path.Combine(directory.Path, "data.csv");
        const string original = "name\r\nkeep";
        await File.WriteAllTextAsync(path, original);
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => DelimitedSpreadsheetHelper.WriteGeneratedIdsAsync(path, ',', new SpreadsheetIdWriteRequest
        {
            FirstRowIsHeader = true,
            CreateIdColumn = true,
            IdColumnIndex = -1,
            Rows = [],
        }));
        Assert.AreEqual(original, await File.ReadAllTextAsync(path));
        Assert.IsEmpty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [TestMethod]
    public async Task MissingSourceDoesNotCreateOutput()
    {
        using TemporaryDirectory directory = new();
        string path = Path.Combine(directory.Path, "missing.csv");
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => DelimitedSpreadsheetHelper.ReadAsync(path, ','));
        Assert.IsEmpty(Directory.GetFiles(directory.Path));
    }
}
