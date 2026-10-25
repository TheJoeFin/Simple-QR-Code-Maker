using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class EmailBuilderHelperTests
{
    [TestMethod]
    public void SerializesIndependentMailtoExample()
    {
        EmailBuilderState state = new()
        {
            To = " ada@example.com ",
            Cc = "cc@example.com",
            Bcc = "hidden@example.com",
            Subject = "Hello & 雪",
            Body = "line 1\r\nline 2?=😊 ",
        };
        Assert.AreEqual("mailto:ada@example.com?cc=cc%40example.com&bcc=hidden%40example.com&subject=Hello%20%26%20%E9%9B%AA&body=line%201%0D%0Aline%202%3F%3D%F0%9F%98%8A%20", EmailBuilderHelper.Serialize(state));
    }

    [TestMethod]
    public void ParsesIndependentCaseInsensitiveEncodedHeaders()
    {
        Assert.IsTrue(EmailBuilderHelper.TryParse(" MAILTO:ada%40example.com?SUBJECT=A%2BB&body=first%0Asecond&CC=c%40test.com&ignored=x ", out EmailBuilderState state));
        Assert.AreEqual("ada@example.com", state.To);
        Assert.AreEqual("A+B", state.Subject);
        Assert.AreEqual("first\nsecond", state.Body);
        Assert.AreEqual("c@test.com", state.Cc);
    }

    [TestMethod]
    public void RoundtripEscapesRecipientQueryDelimitersAndPreservesBodyWhitespace()
    {
        EmailBuilderState expected = new()
        {
            To = "tag?x#雪@example.com,other@example.com",
            Subject = "雪?&=+",
            Body = " \r\nline\n ",
        };
        string canonical = EmailBuilderHelper.Serialize(expected);
        Assert.StartsWith("mailto:tag%3Fx%23%E9%9B%AA@example.com,other@example.com?", canonical);
        Assert.IsTrue(EmailBuilderHelper.TryParse(canonical, out EmailBuilderState actual));
        Assert.AreEqual(expected.To, actual.To);
        Assert.AreEqual(expected.Subject, actual.Subject);
        Assert.AreEqual(expected.Body, actual.Body);
        Assert.AreEqual(canonical, EmailBuilderHelper.Serialize(actual));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("ada@example.com")]
    [DataRow("mailto:")]
    [DataRow("mailto:?subject=hello")]
    public void RejectsMissingMailtoRecipient(string? text)
    {
        Assert.IsFalse(EmailBuilderHelper.IsEmail(text));
        Assert.AreEqual("Email", EmailBuilderHelper.GetDisplayName(text));
    }

    [TestMethod]
    public void HandlesMalformedQueryAndDisplayNameFallbacks()
    {
        Assert.IsTrue(EmailBuilderHelper.TryParse("mailto:a@test.com?bad&=ignored&subject=%zz&body=a=b", out EmailBuilderState state));
        Assert.AreEqual("%zz", state.Subject);
        Assert.AreEqual("a=b", state.Body);
        Assert.AreEqual("%zz", EmailBuilderHelper.GetDisplayName(state));
        Assert.AreEqual("a@test.com", EmailBuilderHelper.GetDisplayName(new EmailBuilderState { To = " a@test.com " }));
        Assert.AreEqual(string.Empty, EmailBuilderHelper.Serialize(new EmailBuilderState()));
    }
}
