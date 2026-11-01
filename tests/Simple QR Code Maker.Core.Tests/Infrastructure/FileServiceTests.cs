using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Core.Services;
using System.Text.Json;

namespace Simple_QR_Code_Maker.Core.Tests.Infrastructure;

[TestClass]
[TestCategory("Integration")]
public sealed class FileServiceTests
{
    [TestMethod]
    public void SaveCreatesDirectoryAndRoundTripsUnicode()
    {
        using TemporaryDirectory directory = new();
        string folder = Path.Combine(directory.Path, "nested", "settings");
        FileService service = new();
        Dictionary<string, string> expected = new() { ["text"] = "\u03bb\u6f22\ud83d\ude00", ["multiline"] = "one\r\ntwo" };

        service.Save(folder, "data.json", expected);

        Assert.IsTrue(Directory.Exists(folder));
        Dictionary<string, string>? actual = service.Read<Dictionary<string, string>>(folder, "data.json");
        Assert.IsNotNull(actual);
        CollectionAssert.AreEquivalent(expected.ToArray(), actual.ToArray());
    }

    [TestMethod]
    public void SaveReplacesPreviousLongerContent()
    {
        using TemporaryDirectory directory = new();
        FileService service = new();
        service.Save(directory.Path, "data.json", new string('x', 100));
        service.Save(directory.Path, "data.json", "short");
        Assert.AreEqual("short", service.Read<string>(directory.Path, "data.json"));
    }

    [TestMethod]
    public void MissingFileReturnsDefaultWithoutCreatingDirectory()
    {
        using TemporaryDirectory directory = new();
        string missingFolder = Path.Combine(directory.Path, "missing");
        Assert.IsNull(new FileService().Read<string>(missingFolder, "data.json"));
        Assert.AreEqual(0, new FileService().Read<int>(missingFolder, "data.json"));
        Assert.IsFalse(Directory.Exists(missingFolder));
    }

    [TestMethod]
    public void InvalidJsonIsNotSilentlyDiscarded()
    {
        using TemporaryDirectory directory = new();
        File.WriteAllText(Path.Combine(directory.Path, "data.json"), "{broken");
        Assert.ThrowsExactly<JsonException>(() => new FileService().Read<Dictionary<string, string>>(directory.Path, "data.json"));
    }

    [TestMethod]
    public void DeleteIsIdempotentAndAcceptsNullName()
    {
        using TemporaryDirectory directory = new();
        FileService service = new();
        service.Save(directory.Path, "data.json", "value");
        service.Delete(directory.Path, "data.json");
        service.Delete(directory.Path, "data.json");
        service.Delete(directory.Path, null);
        Assert.IsFalse(File.Exists(Path.Combine(directory.Path, "data.json")));
    }

    [TestMethod]
    public void WriteFailurePropagates()
    {
        using TemporaryDirectory directory = new();
        string occupiedPath = Path.Combine(directory.Path, "file");
        File.WriteAllText(occupiedPath, "keep");
        Assert.ThrowsExactly<IOException>(() => new FileService().Save(occupiedPath, "data.json", "value"));
        Assert.AreEqual("keep", File.ReadAllText(occupiedPath));
    }
}
