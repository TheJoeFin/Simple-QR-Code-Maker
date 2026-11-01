using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Drawing;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Windows.Tests.Workflows;

[TestClass]
public sealed class QrExportWriterTests
{
    [TestMethod]
    public async Task ZipContainsBothFormatsAndIsCompleteBeforeReturning()
    {
        using QrRenderSettingsSnapshot settings = CreateSettings();
        RequestedQrCodeItem[] codes = [new("one"), new("two"), new("")];
        List<int> progress = [];
        using MemoryStream output = new();
        await QrExportWriter.WriteZipAsync(output, codes, settings, count => progress.Add(count), FileKind.PNG, FileKind.SVG);
        Assert.IsTrue(output.CanWrite);
        output.Position = 0;
        using ZipArchive archive = new(output, ZipArchiveMode.Read, leaveOpen: true);
        CollectionAssert.AreEquivalent(new[] { "one.png", "one.svg", "two.png", "two.svg" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, progress);
        using Stream png = archive.GetEntry("one.png")!.Open();
        byte[] signature = new byte[8];
        await png.ReadExactlyAsync(signature);
        CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, signature);
        using StreamReader svg = new(archive.GetEntry("two.svg")!.Open());
        Assert.AreEqual("svg", XDocument.Parse(await svg.ReadToEndAsync()).Root!.Name.LocalName);
    }

    [TestMethod]
    public async Task SvgOutputHasNoBomAndLeavesCallerStreamOpen()
    {
        using QrRenderSettingsSnapshot settings = CreateSettings();
        using MemoryStream output = new();
        await QrExportWriter.WriteAsync(new("unicode-\u6f22"), output, FileKind.SVG, settings);
        Assert.IsTrue(output.CanWrite);
        byte[] bytes = output.ToArray();
        Assert.IsFalse(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.AreEqual("svg", XDocument.Parse(Encoding.UTF8.GetString(bytes)).Root!.Name.LocalName);
    }

    [TestMethod]
    public async Task InvalidFormatsAndStreamFailuresPropagate()
    {
        using QrRenderSettingsSnapshot settings = CreateSettings();
        using MemoryStream output = new();
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            QrExportWriter.WriteAsync(new("one"), output, (FileKind)999, settings));
        using MemoryStream readOnly = new([1, 2, 3], writable: false);
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            QrExportWriter.WriteAsync(new("one"), readOnly, FileKind.PNG, settings));
    }

    [TestMethod]
    public async Task EmptyZipIsValidAndMakesNoProgress()
    {
        using QrRenderSettingsSnapshot settings = CreateSettings();
        using MemoryStream output = new();
        await QrExportWriter.WriteZipAsync(output, [], settings, _ => Assert.Fail("No items completed."), FileKind.PNG);
        output.Position = 0;
        using ZipArchive archive = new(output, ZipArchiveMode.Read);
        Assert.IsEmpty(archive.Entries);
    }

    [TestMethod]
    public async Task PngAndSvgExportsCarryPatternsEyesFrameAccentsAndIcons()
    {
        QrDesignPreset preset = QrDesignPresetCatalog.All[1];
        using QrRenderSettingsSnapshot settings = QrRenderSettingsSnapshot.Create(
            ErrorCorrectionLevel.M, Color.FromArgb(preset.Foreground.A, preset.Foreground.R, preset.Foreground.G, preset.Foreground.B),
            Color.White, null, 15, 4, null, 4, preset.Frame, QrFrameTextSource.Manual, preset.Caption, preset.Appearance);
        RequestedQrCodeItem code = new("https://example.com/export");
        byte[] actualPng = await QrExportWriter.RenderPngAsync(code, settings);
        using MemoryStream expectedPng = new();
        QrCodeRenderer.SaveQrCodePngToStream(expectedPng, code.CodeAsText, settings.ErrorCorrectionLevel,
            settings.ForegroundColor, settings.BackgroundColor, qrPaddingModules: 4, framePreset: preset.Frame,
            frameText: preset.Caption, appearance: preset.Appearance);
        CollectionAssert.AreEqual(expectedPng.ToArray(), actualPng);
        string expectedSvg = QrCodeRenderer.GetSvgQrCodeForText(code.CodeAsText, settings.ErrorCorrectionLevel,
            settings.ForegroundColor, settings.BackgroundColor, qrPaddingModules: 4, framePreset: preset.Frame,
            frameText: preset.Caption, appearance: preset.Appearance).Content;
        Assert.AreEqual(expectedSvg, await QrExportWriter.RenderSvgAsync(code, settings));
        using MemoryStream stream = new(actualPng);
        using Bitmap bitmap = new(stream);
        Assert.AreEqual(code.CodeAsText, QrCodeRenderer.GetStringsFromBitmap(bitmap).Single().Item1);
    }

    private static QrRenderSettingsSnapshot CreateSettings()
        => QrRenderSettingsSnapshot.Create(
            ErrorCorrectionLevel.M, Color.Black, Color.White,
            null, 15, 4, null, 2, QrFramePreset.None, QrFrameTextSource.Manual, null);
}
