using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Core.Helpers;
using System.Text.Json;

namespace Simple_QR_Code_Maker.Core.Tests.Infrastructure;

[TestClass]
public sealed class JsonTests
{
    [TestMethod]
    public async Task RoundTripPreservesValuesAndEscapedText()
    {
        Dictionary<string, string> expected = new() { ["text"] = "a\"b\\c\r\n\u6f22" };
        string json = await Json.StringifyAsync(expected);
        Dictionary<string, string>? actual = await Json.ToObjectAsync<Dictionary<string, string>>(json);
        Assert.IsNotNull(actual);
        Assert.AreEqual(expected["text"], actual["text"]);
    }

    [TestMethod]
    public async Task NullRoundTrips()
    {
        Assert.AreEqual("null", await Json.StringifyAsync(null));
        Assert.IsNull(await Json.ToObjectAsync<string>("null"));
    }

    [TestMethod]
    public async Task InvalidJsonAndMismatchedTypesPropagate()
    {
        await Assert.ThrowsExactlyAsync<JsonException>(() => Json.ToObjectAsync<string>("{"));
        await Assert.ThrowsExactlyAsync<JsonException>(() => Json.ToObjectAsync<int>("\"not an integer\""));
    }
}
