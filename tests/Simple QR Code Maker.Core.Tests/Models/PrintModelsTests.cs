using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Models;

[TestClass]
public sealed class PrintModelsTests
{
    [TestMethod]
    public void NormalizationClampsBoundariesWithoutMutatingOriginal()
    {
        PrintJobSettings original = new()
        {
            CodesPerPage = 3,
            PageType = PrintPageType.A5,
            PageLayout = (PrintPageLayout)99,
            MarginMm = -1,
            CodeSizeMm = 300,
            SpacingMm = 30,
            FitAsManyAsPossible = false,
            ShowLabels = false,
        };
        PrintJobSettings normalized = original.Normalize();
        Assert.AreEqual(PrintJobSettings.DefaultCodesPerPage, normalized.CodesPerPage);
        Assert.AreEqual(PrintPageType.A5, normalized.PageType);
        Assert.AreEqual(PrintPageLayout.Portrait, normalized.PageLayout);
        Assert.AreEqual(0, normalized.MarginMm, 1e-10);
        Assert.AreEqual(250, normalized.CodeSizeMm, 1e-10);
        Assert.AreEqual(25, normalized.SpacingMm, 1e-10);
        Assert.IsFalse(normalized.FitAsManyAsPossible);
        Assert.IsFalse(normalized.ShowLabels);
        Assert.AreEqual(-1, original.MarginMm, 1e-10);
        Assert.AreEqual(normalized, normalized.Normalize());
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void NonfiniteMeasurementsUseDefaults(double value)
    {
        PrintJobSettings settings = new PrintJobSettings { MarginMm = value, CodeSizeMm = value, SpacingMm = value }.Normalize();
        Assert.AreEqual(10, settings.MarginMm, 1e-10);
        Assert.AreEqual(35, settings.CodeSizeMm, 1e-10);
        Assert.AreEqual(4, settings.SpacingMm, 1e-10);
    }

    [TestMethod]
    public void PreservesValidBoundsAndClampsOtherEnds()
    {
        PrintJobSettings settings = new PrintJobSettings { MarginMm = 100, CodeSizeMm = -1, SpacingMm = -1 }.Normalize();
        Assert.AreEqual(50, settings.MarginMm, 1e-10);
        Assert.AreEqual(5, settings.CodeSizeMm, 1e-10);
        Assert.AreEqual(0, settings.SpacingMm, 1e-10);
        foreach (int count in PrintJobSettings.SupportedCodesPerPage)
            Assert.AreEqual(count, new PrintJobSettings { CodesPerPage = count }.Normalize().CodesPerPage);
    }

    [TestMethod]
    [DataRow("en-US", PrintPageType.Letter)]
    [DataRow("en-GB", PrintPageType.A4)]
    [DataRow("de-DE", PrintPageType.A4)]
    [DataRow("", PrintPageType.Letter)]
    public void AutoAndUnknownPageTypesUseRegionalDefault(string cultureName, PrintPageType expected)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.AreEqual(expected, PrintPageTypeHelper.GetRegionalDefault());
            Assert.AreEqual(expected, PrintPageTypeHelper.Resolve(PrintPageType.Auto));
            Assert.AreEqual(expected, PrintPageTypeHelper.Resolve((PrintPageType)999));
            Assert.AreEqual(PrintPageType.A3, PrintPageTypeHelper.Resolve(PrintPageType.A3));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void SelectablePagesAreDefinedDistinctAndExcludeAuto()
    {
        Assert.AreEqual(11, PrintPageTypeHelper.SelectablePageTypes.Length);
        Assert.AreEqual(11, PrintPageTypeHelper.SelectablePageTypes.Distinct().Count());
        Assert.IsTrue(PrintPageTypeHelper.SelectablePageTypes.All(static type => Enum.IsDefined(type) && type != PrintPageType.Auto));
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void InvalidScaleProfilesFallBackToSquare(double value)
    {
        QrImageLayoutMetrics metrics = QrImageLayoutMetrics.FromScaleProfile(value, value);
        Assert.AreEqual(1, metrics.WidthPerQrSize, 1e-10);
        Assert.AreEqual(1, metrics.HeightPerQrSize, 1e-10);
    }

    [TestMethod]
    public void ImageMetricsCalculateRatiosAndClampToQrSize()
    {
        QrImageLayoutMetrics metrics = new(400, 300, 200, 100);
        Assert.AreEqual(2, metrics.WidthPerQrSize, 1e-10);
        Assert.AreEqual(3, metrics.HeightPerQrSize, 1e-10);
        Assert.AreEqual(1, new QrImageLayoutMetrics(0, 0, 100, 100).WidthPerQrSize, 1e-10);
        Assert.AreEqual(1, QrImageLayoutMetrics.Square.HeightPerQrSize, 1e-10);
    }
}
