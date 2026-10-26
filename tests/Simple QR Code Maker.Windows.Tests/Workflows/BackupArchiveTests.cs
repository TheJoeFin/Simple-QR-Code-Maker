using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;

namespace Simple_QR_Code_Maker.Windows.Tests.Workflows;

[TestClass]
public sealed class BackupArchiveTests
{
    [TestMethod]
    public void HistoryMergePrefersImportedValuesAndPreservesOrder()
    {
        HistoryItem old = new() { CodesContent = "same", FrameText = "old" };
        HistoryItem replacement = new() { CodesContent = "same", FrameText = "new" };
        ObservableCollection<HistoryItem> merged = BackupArchiveHelper.MergeHistory(
            [old, new() { CodesContent = "existing" }], [new() { CodesContent = "first" }, replacement]);
        CollectionAssert.AreEqual(new[] { "first", "same", "existing" }, merged.Select(item => item.CodesContent).ToArray());
        Assert.AreSame(replacement, merged[1]);
    }

    [TestMethod]
    public void BrandMergeIsCaseInsensitiveAndChoosesFirstImportedDefault()
    {
        ObservableCollection<BrandItem> merged = BackupArchiveHelper.MergeBrands(
            [new() { Name = "same", IsDefault = true }, new() { Name = "keep" }],
            [new() { Name = "SAME", IsDefault = true }, new() { Name = "other", IsDefault = true }]);
        CollectionAssert.AreEqual(new[] { "SAME", "other", "keep" }, merged.Select(brand => brand.Name).ToArray());
        Assert.HasCount(1, merged.Where(brand => brand.IsDefault).ToArray());
        Assert.IsTrue(merged[0].IsDefault);
    }

    [TestMethod]
    public void ExistingDefaultSurvivesWhenImportHasNoDefault()
    {
        ObservableCollection<BrandItem> merged = BackupArchiveHelper.MergeBrands(
            [new() { Name = "keep", IsDefault = true }], [new() { Name = "new" }]);
        Assert.IsTrue(merged.Single(brand => brand.Name == "keep").IsDefault);
    }

    [TestMethod]
    public void RelativeImageReferencesResolveOnlyWhenPresent()
    {
        Dictionary<string, string> paths = new() { ["images/logo.png"] = @"C:\temporary\logo.png" };
        Assert.AreEqual(@"C:\temporary\logo.png", BackupArchiveHelper.ResolveImportedImagePath("images/logo.png", paths));
        Assert.IsNull(BackupArchiveHelper.ResolveImportedImagePath("images/missing.png", paths));
        Assert.IsNull(BackupArchiveHelper.ResolveImportedImagePath(null, paths));
        Assert.AreEqual(@"C:\old\logo.png", BackupArchiveHelper.ResolveImportedImagePath(@"C:\old\logo.png", paths));
    }

    [TestMethod]
    public void ArchiveNamesArePredictableAndSegmentsAreBounded()
    {
        Assert.AreEqual("images/history_20261025_101112_example_logo_logo_003.png",
            BackupArchiveHelper.CreateArchiveImagePath("History", "Example", "Logo.png", new DateTime(2026, 10, 25, 10, 11, 12), 3));
        Assert.AreEqual("one_two", BackupArchiveHelper.SanitizeFileNameSegment(" /ONE:* TWO/ ", 10));
        Assert.AreEqual("abcd", BackupArchiveHelper.SanitizeFileNameSegment("abcdefgh", 4));
        Assert.AreEqual("", BackupArchiveHelper.SanitizeFileNameSegment("text", 0));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ImageExportDeduplicatesAndImportUsesUniqueSafeLocalNames()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"simple-qr-backup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "logo.png");
            await File.WriteAllTextAsync(source, "synthetic image bytes");
            using MemoryStream stream = new();
            string? archivePath;
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                Dictionary<string, string> map = [];
                archivePath = await BackupArchiveHelper.AddImageToArchiveAsync(archive, source, map, "history", "example", new DateTime(2026, 1, 1));
                Assert.AreEqual(archivePath, await BackupArchiveHelper.AddImageToArchiveAsync(archive, source, map, "brand", "duplicate", default));
                Assert.IsNull(await BackupArchiveHelper.AddImageToArchiveAsync(archive, source + ".missing", map, "history", "", default));
                WriteEntry(archive, "images/sub/duplicate.png", "first");
                WriteEntry(archive, "images/other/duplicate.png", "second");
                WriteEntry(archive, "images/../../outside.png", "contained");
                WriteEntry(archive, "ignored.txt", "ignored");
            }

            stream.Position = 0;
            using ZipArchive input = new(stream, ZipArchiveMode.Read);
            Dictionary<string, string> extracted = await BackupArchiveHelper.ExtractImagesAsync(input, directory);
            Assert.HasCount(4, extracted);
            Assert.IsNotNull(archivePath);
            Assert.AreEqual("synthetic image bytes", await File.ReadAllTextAsync(extracted[archivePath]));
            Assert.AreNotEqual(extracted["images/sub/duplicate.png"], extracted["images/other/duplicate.png"]);
            foreach (string path in extracted.Values)
                Assert.StartsWith(Path.Combine(directory, "ImportedImages") + Path.DirectorySeparatorChar, Path.GetFullPath(path));
            Assert.IsFalse(File.Exists(Path.Combine(directory, "outside.png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        using StreamWriter writer = new(archive.CreateEntry(name).Open(), Encoding.UTF8);
        writer.Write(text);
    }
}
