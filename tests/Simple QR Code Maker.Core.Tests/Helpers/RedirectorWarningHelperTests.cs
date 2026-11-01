using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class RedirectorWarningHelperTests
{
    [TestMethod]
    [DataRow("https://bit.ly/code", "bit.ly")]
    [DataRow("https://WWW.BIT.LY./code", "bit.ly")]
    [DataRow("https://sub.qrco.de/code", "sub.qrco.de")]
    [DataRow("https://tinyurl.com/a", "tinyurl.com")]
    public void WarnsForKnownHostsAndTheirSubdomains(string text, string expectedHost)
    {
        RedirectorUrlClassification result = RedirectorWarningHelper.Classify(text);
        Assert.IsTrue(result.IsAbsoluteUri);
        Assert.IsTrue(result.IsKnownRedirector);
        Assert.IsFalse(result.IsSafeDomain);
        Assert.IsTrue(result.ShouldWarn);
        Assert.AreEqual(expectedHost, result.Host);
    }

    [TestMethod]
    [DataRow("https://notbit.ly/code")]
    [DataRow("https://bit.ly.evil.example/code")]
    [DataRow("https://bit.ly@evil.example/code")]
    [DataRow("https://example.com/?next=https://bit.ly/code")]
    [DataRow("https://example.com/#bit.ly")]
    [DataRow("https://tinyurl.com-evil.example/code")]
    public void DeceptiveNamesAndEmbeddedUrlsAreNotKnownRedirectorHosts(string text)
    {
        RedirectorUrlClassification result = RedirectorWarningHelper.Classify(text, ["example.com"]);
        Assert.IsTrue(result.IsAbsoluteUri);
        Assert.IsFalse(result.IsKnownRedirector);
        Assert.IsFalse(result.IsSafeDomain);
        Assert.IsFalse(result.ShouldWarn);
    }

    [TestMethod]
    public void AllowlistMatchesOnlyWholeHostOrSubdomain()
    {
        Assert.IsFalse(RedirectorWarningHelper.Classify("https://sub.bit.ly/code", [" WWW.BIT.LY. "]).ShouldWarn);
        Assert.IsTrue(RedirectorWarningHelper.Classify("https://bit.ly/code", ["it.ly"]).ShouldWarn);
        Assert.IsTrue(RedirectorWarningHelper.Classify("https://bit.ly/code", ["bit.ly.evil.example"]).ShouldWarn);
        Assert.IsTrue(RedirectorWarningHelper.MatchesHostOrSubdomain("child.example.com", ["example.com"]));
        Assert.IsFalse(RedirectorWarningHelper.MatchesHostOrSubdomain("badexample.com", ["example.com"]));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t ")]
    [DataRow("example.com/path")]
    [DataRow("https://")]
    public void InvalidOrRelativeInputHasNoClassification(string? text)
    {
        Assert.AreEqual(RedirectorUrlClassification.None, RedirectorWarningHelper.Classify(text));
    }

    [TestMethod]
    public void HostlessAbsoluteUrisDoNotWarn()
    {
        RedirectorUrlClassification result = RedirectorWarningHelper.Classify("urn:example:resource");
        Assert.IsTrue(result.IsAbsoluteUri);
        Assert.AreEqual(string.Empty, result.Host);
        Assert.IsFalse(result.ShouldWarn);
    }

    [TestMethod]
    public void NormalizesDeduplicatesAndSortsHostNames()
    {
        CollectionAssert.AreEqual(new[] { "a.test", "b.test" }, RedirectorWarningHelper.NormalizeHosts(
            [" WWW.B.Test. ", "b.test", "", "A.TEST", "  "]).ToArray());
        Assert.AreEqual(0, RedirectorWarningHelper.NormalizeHosts(null).Count);
        Assert.AreEqual(string.Empty, RedirectorWarningHelper.NormalizeHost(null));
        Assert.AreEqual("sub.example.com", RedirectorWarningHelper.NormalizeHost(" Sub.Example.Com... "));
    }
}
