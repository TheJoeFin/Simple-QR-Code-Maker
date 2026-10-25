using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class VCardBuilderHelperTests
{
    [TestMethod]
    public void SerializesIndependentContactExample()
    {
        VCardBuilderState state = new()
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Organization = "Analytical, Engines",
            MobilePhone = "+44 123",
            Email = "ada@example.com",
            StreetAddress = "1; Main Street",
            Locality = "London",
            Note = "first\rsecond\r\nthird\n雪",
        };
        Assert.AreEqual(
            "BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Ada Lovelace\r\nN:Lovelace;Ada;;;\r\nORG:Analytical\\, Engines\r\nTEL;TYPE=CELL:+44 123\r\nEMAIL;TYPE=INTERNET:ada@example.com\r\nADR;TYPE=HOME:;;1\\; Main Street;London;;;\r\nNOTE:first\\nsecond\\nthird\\n雪\r\nEND:VCARD",
            VCardBuilderHelper.Serialize(state));
    }

    [TestMethod]
    public void ParsesIndependentFoldedCaseInsensitiveContact()
    {
        const string text = "begin:vcard\nVERSION:3.0\nFN:Renée 雪\nN:Last;First;Middle;Dr.;Jr.\nTEL;TYPE=WORK:111\nTEL;TYPE=HOME:222\nTEL;TYPE=CELL:333\nNOTE:Long\n continuation\n\tmore\\Nline\nend:vcard";
        Assert.IsTrue(VCardBuilderHelper.TryParse(text, out VCardBuilderState state));
        Assert.AreEqual("Renée 雪", state.FormattedName);
        Assert.AreEqual("Last", state.LastName);
        Assert.AreEqual("First", state.FirstName);
        Assert.AreEqual("Middle", state.MiddleName);
        Assert.AreEqual("Dr.", state.Prefix);
        Assert.AreEqual("Jr.", state.Suffix);
        Assert.AreEqual("111", state.WorkPhone);
        Assert.AreEqual("222", state.HomePhone);
        Assert.AreEqual("333", state.MobilePhone);
        Assert.AreEqual("Longcontinuationmore\nline", state.Note);
    }

    [TestMethod]
    public void RoundtripPreservesEscapedStructuredSeparatorsAndLiteralBackslashes()
    {
        VCardBuilderState expected = new()
        {
            FirstName = @"First\name",
            LastName = "Last;尾",
            MiddleName = "Mid,😊",
            Prefix = "Dr.",
            Suffix = "Jr.",
            StreetAddress = @"12; Path\name",
            Locality = "Town;East",
            Region = "Region,West",
            PostalCode = "123",
            Country = "日本",
            Title = "Engineer",
            Nickname = "A,B",
            Birthday = "1815-12-10",
            Url = "https://example.com",
            Note = @"literal \n; comma, and slash\",
        };
        string canonical = VCardBuilderHelper.Serialize(expected);
        Assert.IsTrue(VCardBuilderHelper.TryParse(canonical, out VCardBuilderState actual));
        Assert.AreEqual(expected.FirstName, actual.FirstName);
        Assert.AreEqual(expected.LastName, actual.LastName);
        Assert.AreEqual(expected.MiddleName, actual.MiddleName);
        Assert.AreEqual(expected.StreetAddress, actual.StreetAddress);
        Assert.AreEqual(expected.Locality, actual.Locality);
        Assert.AreEqual(expected.Region, actual.Region);
        Assert.AreEqual(expected.Country, actual.Country);
        Assert.AreEqual(expected.Note, actual.Note);
        Assert.AreEqual(canonical, VCardBuilderHelper.Serialize(actual));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("not a card")]
    [DataRow("BEGIN:VCARD\nFN:Name")]
    [DataRow("END:VCARD\nFN:Name\nBEGIN:VCARD")]
    [DataRow("BEGIN:VCARD\nVERSION:3.0\nEND:VCARD")]
    public void RejectsMissingBoundaryOrDisplayName(string? text)
    {
        Assert.IsFalse(VCardBuilderHelper.TryParse(text, out _));
        Assert.IsFalse(VCardBuilderHelper.IsVCard(text));
        Assert.AreEqual("vCard contact", VCardBuilderHelper.GetDisplayName(text));
    }

    [TestMethod]
    public void DisplayNameUsesOrderedFallbacksAndEmptyContactDoesNotSerialize()
    {
        VCardBuilderState state = new();
        Assert.AreEqual(string.Empty, VCardBuilderHelper.Serialize(state));
        state.MobilePhone = " 123 ";
        Assert.AreEqual("123", VCardBuilderHelper.GetDisplayName(state));
        state.Email = " a@test.com ";
        Assert.AreEqual("a@test.com", VCardBuilderHelper.GetDisplayName(state));
        state.Organization = " Company ";
        Assert.AreEqual("Company", VCardBuilderHelper.GetDisplayName(state));
        state.FirstName = " Ada ";
        state.LastName = " Lovelace ";
        Assert.AreEqual("Ada Lovelace", VCardBuilderHelper.GetDisplayName(state));
        state.FormattedName = " Preferred ";
        Assert.AreEqual("Preferred", VCardBuilderHelper.GetDisplayName(state));
    }

    [TestMethod]
    public void UntypedPhonesFillAvailableSlotsAndMalformedPropertiesAreIgnored()
    {
        const string text = "BEGIN:VCARD\rVERSION:3.0\rFN:Name\rbroken\rTEL:1\rTEL:2\rTEL:3\rTEL:4\rUNKNOWN:value\rEND:VCARD";
        Assert.IsTrue(VCardBuilderHelper.TryParse(text, out VCardBuilderState state));
        Assert.AreEqual("1", state.MobilePhone);
        Assert.AreEqual("2", state.WorkPhone);
        Assert.AreEqual("3", state.HomePhone);
    }
}
