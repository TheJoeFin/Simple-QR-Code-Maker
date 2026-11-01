using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class WifiBuilderHelperTests
{
    [TestMethod]
    public void SerializesIndependentEscapingExample()
    {
        WifiBuilderState state = new() { Ssid = "Cafe;雪", Password = "p:a,s\"s\\", IsHiddenNetwork = true };
        Assert.AreEqual("WIFI:S:Cafe\\;雪;T:WPA;P:p\\:a\\,s\\\"s\\\\;H:true;;", WifiBuilderHelper.Serialize(state));
    }

    [TestMethod]
    [DataRow("WIFI:S:Guest;T:nopass;;", WifiAuthenticationType.None)]
    [DataRow("wifi:s:Guest;t:wep;p:key;h:TRUE;;", WifiAuthenticationType.WEP)]
    [DataRow("WIFI:S:Guest;T:WPA2;P:key;;", WifiAuthenticationType.WPA)]
    [DataRow("WIFI:S:Guest;T:WPA3;P:key;;", WifiAuthenticationType.WPA)]
    [DataRow("WIFI:S:Guest;;", WifiAuthenticationType.None)]
    public void ParsesKnownAuthenticationRepresentations(string text, WifiAuthenticationType expected)
    {
        Assert.IsTrue(WifiBuilderHelper.TryParse(text, out WifiBuilderState state));
        Assert.AreEqual("Guest", state.Ssid);
        Assert.AreEqual(expected, state.AuthenticationType);
    }

    [TestMethod]
    public void RoundtripPreservesUnicodeEscapedSeparatorsAndBackslashes()
    {
        WifiBuilderState expected = new() { Ssid = "\\;:,\"雪😊\n", Password = "a\\;b:c,d\"e", IsHiddenNetwork = true };
        string canonical = WifiBuilderHelper.Serialize(expected);
        Assert.IsTrue(WifiBuilderHelper.TryParse(canonical, out WifiBuilderState actual));
        Assert.AreEqual(expected.Ssid, actual.Ssid);
        Assert.AreEqual(expected.Password, actual.Password);
        Assert.IsTrue(actual.IsHiddenNetwork);
        Assert.AreEqual(canonical, WifiBuilderHelper.Serialize(actual));
    }

    [TestMethod]
    public void OpenNetworkOmitsPasswordAndEmptyNetworkDoesNotSerialize()
    {
        Assert.AreEqual(string.Empty, WifiBuilderHelper.Serialize(new WifiBuilderState()));
        Assert.AreEqual("WIFI:S:Guest;T:nopass;;", WifiBuilderHelper.Serialize(new WifiBuilderState
        {
            Ssid = "Guest",
            Password = "ignored",
            AuthenticationType = WifiAuthenticationType.None,
        }));
        Assert.IsTrue(WifiBuilderHelper.TryParse("WIFI:S:Guest;H:not-bool;UNKNOWN:ignored;;", out WifiBuilderState state));
        Assert.IsFalse(state.IsHiddenNetwork);
        Assert.AreEqual("Guest", WifiBuilderHelper.GetDisplayName(state));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("SSID:Guest")]
    [DataRow("WIFI:T:WPA;P:key;;")]
    [DataRow("WIFI:S: ;T:WPA;;")]
    [DataRow("WIFI:S:Guest;T:ROT13;;")]
    public void RejectsInvalidPayloads(string? text)
    {
        Assert.IsFalse(WifiBuilderHelper.IsWifi(text));
        Assert.AreEqual("WiFi network", WifiBuilderHelper.GetDisplayName(text));
    }
}
