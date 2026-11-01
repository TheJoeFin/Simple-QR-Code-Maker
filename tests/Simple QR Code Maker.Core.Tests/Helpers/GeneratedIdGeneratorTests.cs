using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class GeneratedIdGeneratorTests
{
    [TestMethod]
    [DataRow(SpreadsheetGeneratedIdFormat.Guid, "D", 36)]
    [DataRow(SpreadsheetGeneratedIdFormat.GuidWithoutDashes, "N", 32)]
    public void CreatesGuidWithRequestedRepresentationAndAffixes(SpreadsheetGeneratedIdFormat format, string specifier, int length)
    {
        string value = GeneratedIdGenerator.Create(new GeneratedIdOptions { Format = format, Prefix = "雪[", Suffix = "]😊" });
        Assert.StartsWith("雪[", value);
        Assert.EndsWith("]😊", value);
        string core = value[2..^3];
        Assert.AreEqual(length, core.Length);
        Assert.IsTrue(Guid.TryParseExact(core, specifier, out Guid parsed));
        Assert.AreNotEqual(Guid.Empty, parsed);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(12)]
    [DataRow(128)]
    public void NanoIdUsesRequestedLengthAndDocumentedAlphabet(int length)
    {
        string value = GeneratedIdGenerator.Create(new GeneratedIdOptions { Format = SpreadsheetGeneratedIdFormat.NanoId, NanoIdLength = length });
        Assert.AreEqual(length, value.Length);
        Assert.IsTrue(value.All(static character => character is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z'));
    }

    [TestMethod]
    public void CreatesRequestedCountWithoutDependingOnRandomValues()
    {
        IReadOnlyList<string> values = GeneratedIdGenerator.CreateMany(3, new GeneratedIdOptions { Prefix = "id-" });
        Assert.AreEqual(3, values.Count);
        Assert.IsTrue(values.All(static value => value.Length == 39 && Guid.TryParseExact(value[3..], "D", out _)));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void RejectsNonpositiveCountAndNanoIdLength(int value)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GeneratedIdGenerator.CreateMany(value, new GeneratedIdOptions()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GeneratedIdGenerator.Create(new GeneratedIdOptions
        {
            Format = SpreadsheetGeneratedIdFormat.NanoId,
            NanoIdLength = value,
        }));
    }

    [TestMethod]
    public void RejectsMissingOptionsAndUnknownFormat()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GeneratedIdGenerator.Create(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => GeneratedIdGenerator.CreateMany(1, null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GeneratedIdGenerator.Create(new GeneratedIdOptions
        {
            Format = (SpreadsheetGeneratedIdFormat)999,
        }));
    }

    [TestMethod]
    [DataRow(1.0, 1)]
    [DataRow(123.0, 123)]
    [DataRow(2147483647.0, int.MaxValue)]
    public void AcceptsRepresentablePositiveWholeNumbers(double value, int expected)
    {
        Assert.IsTrue(GeneratedIdGenerator.TryGetWholeNumber(value, out int actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(1.5)]
    [DataRow(2147483648.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void RejectsUnrepresentableWholeNumbersAndClearsResult(double value)
    {
        Assert.IsFalse(GeneratedIdGenerator.TryGetWholeNumber(value, out int actual));
        Assert.AreEqual(0, actual);
    }
}
