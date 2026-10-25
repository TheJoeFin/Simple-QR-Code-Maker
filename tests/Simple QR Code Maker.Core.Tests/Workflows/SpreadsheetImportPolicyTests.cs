using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Workflows;

[TestClass]
public sealed class SpreadsheetImportPolicyTests
{
    [TestMethod]
    public void HeadersUseWidestRowAndFillMissingNames()
    {
        SpreadsheetSourceRow[] rows =
        [
            new() { SourceRowIndex = 0, Cells = ["Name", " "] },
            new() { SourceRowIndex = 1, Cells = ["value", "other", "extra"] },
        ];
        CollectionAssert.AreEqual(new[] { "Name", "Column 2", "Column 3" }, SpreadsheetImportPolicy.CreateHeaders(rows, true));
        CollectionAssert.AreEqual(new[] { "Column 1", "Column 2", "Column 3" }, SpreadsheetImportPolicy.CreateHeaders(rows, false));
        Assert.IsEmpty(SpreadsheetImportPolicy.CreateHeaders([], true));
    }

    [TestMethod]
    public void SelectionFiltersBlanksAndPreservesOrdinalOrderAndContent()
    {
        IReadOnlyList<string>[] rows = [["one"], [], [" "], ["one"], ["ONE"], [" two "]];
        CollectionAssert.AreEqual(new[] { "<one>", "<ONE>", "< two >" },
            SpreadsheetImportPolicy.SelectValues(rows, 0, "<", ">", true));
        Assert.HasCount(4, SpreadsheetImportPolicy.SelectValues(rows, 0, "", "", false));
        Assert.IsEmpty(SpreadsheetImportPolicy.SelectValues(rows, -1, "", "", true));
        Assert.IsEmpty(SpreadsheetImportPolicy.SelectValues(rows, 4, "", "", true));
    }

    [TestMethod]
    public void NormalizationDoesNotMutateSource()
    {
        List<string> source = ["one"];
        CollectionAssert.AreEqual(new[] { "one", "", "" }, SpreadsheetImportPolicy.NormalizeRow(source, 3));
        Assert.HasCount(1, source);
        Assert.IsEmpty(SpreadsheetImportPolicy.NormalizeRow(source, 0));
        Assert.AreEqual("", SpreadsheetImportPolicy.GetCellValue(source, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SpreadsheetImportPolicy.NormalizeRow(source, -1));
    }

    [TestMethod]
    public void IdColumnRecognitionIgnoresCaseAndSurroundingWhitespace()
    {
        Assert.AreEqual(1, SpreadsheetImportPolicy.FindIdColumnIndex(["name", " ID ", "id"]));
        Assert.AreEqual(-1, SpreadsheetImportPolicy.FindIdColumnIndex(["identifier"]));
        Assert.IsFalse(SpreadsheetImportPolicy.ShouldGenerateId(["existing-id", " "], 0));
        Assert.IsTrue(SpreadsheetImportPolicy.ShouldGenerateId(["", "value"], 0));
        Assert.IsFalse(SpreadsheetImportPolicy.ShouldGenerateId([], 0));
    }

    [TestMethod]
    public void WritebackOnlyChangesRequestedCellsAndAddsHeader()
    {
        List<List<string>> rows = [["name"], ["first", "existing"], ["second"]];
        SpreadsheetImportPolicy.ApplyGeneratedIds(rows, new SpreadsheetIdWriteRequest
        {
            FirstRowIsHeader = true,
            CreateIdColumn = true,
            IdColumnIndex = 1,
            Rows = [new() { SourceRowIndex = 2, Value = "new-id" }],
        });
        CollectionAssert.AreEqual(new[] { "name", "id" }, rows[0]);
        CollectionAssert.AreEqual(new[] { "first", "existing" }, rows[1]);
        CollectionAssert.AreEqual(new[] { "second", "new-id" }, rows[2]);
    }

    [TestMethod]
    public void HeaderlessWritebackPadsRaggedAndMissingRows()
    {
        List<List<string>> rows = [["first"]];
        SpreadsheetImportPolicy.ApplyGeneratedIds(rows, new SpreadsheetIdWriteRequest
        {
            FirstRowIsHeader = false,
            CreateIdColumn = true,
            IdColumnIndex = 2,
            Rows = [new() { SourceRowIndex = 2, Value = "id" }],
        });
        CollectionAssert.AreEqual(new[] { "first" }, rows[0]);
        CollectionAssert.AreEqual(new[] { "", "", "id" }, rows[2]);
    }

    [TestMethod]
    public void InvalidWritebackDoesNotPartiallyMutateRows()
    {
        List<List<string>> rows = [["name"]];
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SpreadsheetImportPolicy.ApplyGeneratedIds(rows, new SpreadsheetIdWriteRequest
        {
            FirstRowIsHeader = true,
            CreateIdColumn = true,
            IdColumnIndex = 1,
            Rows = [new() { SourceRowIndex = -1, Value = "invalid" }],
        }));
        CollectionAssert.AreEqual(new[] { "name" }, rows[0]);
    }
}
