using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class PrintLayoutHelperTests
{
    [TestMethod]
    [DataRow(0.0)]
    [DataRow(1.0)]
    [DataRow(25.4)]
    [DataRow(250.0)]
    public void UnitConversionPreservesPrecision(double millimeters)
    {
        Assert.AreEqual(millimeters * 72 / 25.4, PrintLayoutHelper.MillimetersToPoints(millimeters), 1e-10);
        Assert.AreEqual(millimeters, PrintLayoutHelper.PointsToMillimeters(PrintLayoutHelper.MillimetersToPoints(millimeters)), 1e-10);
    }

    [TestMethod]
    [DataRow(PrintPageType.Letter, 612.0, 792.0)]
    [DataRow(PrintPageType.Legal, 612.0, 1008.0)]
    [DataRow(PrintPageType.Statement, 396.0, 612.0)]
    [DataRow(PrintPageType.Executive, 540.0, 720.0)]
    [DataRow(PrintPageType.Tabloid, 792.0, 1224.0)]
    [DataRow(PrintPageType.Ledger, 1224.0, 792.0)]
    [DataRow(PrintPageType.A3, 842.0, 1191.0)]
    [DataRow(PrintPageType.A4, 595.0, 842.0)]
    [DataRow(PrintPageType.A5, 420.0, 595.0)]
    [DataRow(PrintPageType.B4, 709.0, 1001.0)]
    [DataRow(PrintPageType.B5, 499.0, 709.0)]
    public void UsesKnownPageDimensionsAndSwapsForLandscape(PrintPageType type, double width, double height)
    {
        Assert.AreEqual((width, height), PrintLayoutHelper.GetPageDimensions(type, PrintPageLayout.Portrait));
        Assert.AreEqual((height, width), PrintLayoutHelper.GetPageDimensions(type, PrintPageLayout.Landscape));
    }

    [TestMethod]
    [DataRow(1, 1, 1)]
    [DataRow(2, 2, 1)]
    [DataRow(4, 2, 2)]
    [DataRow(6, 3, 2)]
    [DataRow(9, 3, 3)]
    [DataRow(12, 4, 3)]
    [DataRow(16, 4, 4)]
    public void FixedGridUsesRequestedCountAndOrientation(int count, int rows, int columns)
    {
        PrintJobSettings settings = new() { CodesPerPage = count, FitAsManyAsPossible = false, PageType = PrintPageType.Letter };
        PrintLayoutMetrics portrait = PrintLayoutHelper.CreateMetrics(settings);
        PrintLayoutMetrics landscape = PrintLayoutHelper.CreateMetrics(settings with { PageLayout = PrintPageLayout.Landscape });
        Assert.AreEqual(rows, portrait.Rows);
        Assert.AreEqual(columns, portrait.Columns);
        Assert.AreEqual(count, portrait.CodesPerPage);
        Assert.AreEqual(columns, landscape.Rows);
        Assert.AreEqual(rows, landscape.Columns);
        Assert.AreEqual(count, landscape.CodesPerPage);
    }

    [TestMethod]
    public void FixedGridSubtractsMarginsAndIntercellSpacingExactly()
    {
        PrintLayoutMetrics metrics = PrintLayoutHelper.CreateMetrics(600, 800, new PrintJobSettings
        {
            FitAsManyAsPossible = false,
            CodesPerPage = 4,
            MarginMm = 25.4,
            SpacingMm = 12.7,
            CodeSizeMm = 25.4,
            ShowLabels = false,
        });
        Assert.AreEqual(72, metrics.MarginPoints, 1e-10);
        Assert.AreEqual(36, metrics.SpacingPoints, 1e-10);
        Assert.AreEqual(210, metrics.CellWidth, 1e-10);
        Assert.AreEqual(310, metrics.CellHeight, 1e-10);
        Assert.AreEqual(72, metrics.ActualCodeSizePoints, 1e-10);
        Assert.AreEqual(600, metrics.PageWidthPoints, 1e-10);
        Assert.AreEqual(800, metrics.PageHeightPoints, 1e-10);
    }

    [TestMethod]
    public void AutoFitUsesRequestedImageSizePaddingAndLabelHeight()
    {
        PrintJobSettings settings = new() { MarginMm = 0, SpacingMm = 0, CodeSizeMm = 25.4, ShowLabels = false };
        PrintLayoutMetrics square = PrintLayoutHelper.CreateMetrics(612, 792, settings);
        Assert.AreEqual(6, square.Columns);
        Assert.AreEqual(8, square.Rows);
        Assert.AreEqual(48, square.CodesPerPage);
        Assert.AreEqual(102, square.CellWidth, 1e-10);
        Assert.AreEqual(99, square.CellHeight, 1e-10);
        Assert.AreEqual(72, square.ActualCodeSizePoints, 1e-10);
        PrintLayoutMetrics labeled = PrintLayoutHelper.CreateMetrics(612, 792, settings with { ShowLabels = true });
        Assert.AreEqual(6, labeled.Columns);
        Assert.AreEqual(6, labeled.Rows);
        PrintLayoutMetrics framed = PrintLayoutHelper.CreateMetrics(612, 792, settings, QrImageLayoutMetrics.FromScaleProfile(2, 3));
        Assert.AreEqual(3, framed.Columns);
        Assert.AreEqual(3, framed.Rows);
    }

    [TestMethod]
    public void PlacementPreservesFrameAspectRatioAndReservesLabels()
    {
        PrintJobSettings settings = new() { CodeSizeMm = 25.4, ShowLabels = false };
        QrImageLayoutMetrics image = QrImageLayoutMetrics.FromScaleProfile(2, 3);
        PrintCodePlacement placement = PrintLayoutHelper.CalculateCodePlacement(200, 200, settings, image);
        Assert.AreEqual(176, placement.ImageHeightPoints, 1e-10);
        Assert.AreEqual(176.0 / 3, placement.ActualCodeSizePoints, 1e-10);
        Assert.AreEqual(176.0 * 2 / 3, placement.ImageWidthPoints, 1e-10);
        PrintCodePlacement labeled = PrintLayoutHelper.CalculateCodePlacement(200, 200, settings with { ShowLabels = true }, image);
        Assert.AreEqual(146, labeled.ImageHeightPoints, 1e-10);
        Assert.AreEqual(146.0 / 3, labeled.ActualCodeSizePoints, 1e-10);
        Assert.AreEqual(72, PrintLayoutHelper.CalculateActualCodeSizePoints(1000, 1000, settings), 1e-10);
        Assert.AreEqual(placement.ActualCodeSizePoints, PrintLayoutHelper.CalculateActualCodeSizePoints(200, 200, settings, image), 1e-10);
    }

    [TestMethod]
    public void TinyPagesAndInvalidProfilesStillHaveFinitePositiveGeometry()
    {
        PrintJobSettings settings = new() { MarginMm = 50, CodeSizeMm = 250, SpacingMm = 25 };
        PrintLayoutMetrics metrics = PrintLayoutHelper.CreateMetrics(1, 1, settings, new QrImageLayoutMetrics(double.NaN, double.PositiveInfinity, 0, 0));
        Assert.AreEqual(1, metrics.Rows);
        Assert.AreEqual(1, metrics.Columns);
        Assert.IsTrue(double.IsFinite(metrics.ActualCodeSizePoints) && metrics.ActualCodeSizePoints >= 1);
        PrintCodePlacement placement = PrintLayoutHelper.CalculateCodePlacement(-10, 0, settings, QrImageLayoutMetrics.Square);
        Assert.AreEqual(1, placement.ImageWidthPoints, 1e-10);
        Assert.AreEqual(1, placement.ImageHeightPoints, 1e-10);
    }

    [TestMethod]
    public void ExtremeFiniteFrameRatioDoesNotOverflowIntoNanPlacement()
    {
        PrintCodePlacement placement = PrintLayoutHelper.CalculateCodePlacement(200, 200,
            new PrintJobSettings { ShowLabels = false }, QrImageLayoutMetrics.FromScaleProfile(double.MaxValue, 1));
        Assert.AreEqual(176, placement.ImageWidthPoints, 1e-10);
        Assert.AreEqual(1, placement.ImageHeightPoints, 1e-10);
        Assert.AreEqual(1, placement.ActualCodeSizePoints, 1e-10);
    }

    [TestMethod]
    public void FixedGridGeometryReconstructsPageAndNeverUpscalesRequestedCode()
    {
        foreach (int count in PrintJobSettings.SupportedCodesPerPage)
        {
            foreach (PrintPageLayout orientation in Enum.GetValues<PrintPageLayout>())
            {
                PrintJobSettings settings = new()
                {
                    CodesPerPage = count,
                    FitAsManyAsPossible = false,
                    PageType = PrintPageType.A4,
                    PageLayout = orientation,
                    MarginMm = 10,
                    SpacingMm = 4,
                };
                PrintLayoutMetrics metrics = PrintLayoutHelper.CreateMetrics(settings);
                Assert.AreEqual(metrics.PageWidthPoints, metrics.CellWidth * metrics.Columns + metrics.SpacingPoints * (metrics.Columns - 1) + metrics.MarginPoints * 2, 1e-9);
                Assert.AreEqual(metrics.PageHeightPoints, metrics.CellHeight * metrics.Rows + metrics.SpacingPoints * (metrics.Rows - 1) + metrics.MarginPoints * 2, 1e-9);
                Assert.IsTrue(metrics.ActualCodeSizePoints > 0 && metrics.ActualCodeSizePoints <= metrics.RequestedCodeSizePoints);
                Assert.IsTrue(metrics.ActualCodeSizePoints <= metrics.CellWidth - 24);
                Assert.IsTrue(metrics.ActualCodeSizePoints <= metrics.CellHeight - 54);
            }
        }
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void RejectsNonphysicalPageDimensions(double value)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PrintLayoutHelper.CreateMetrics(value, 792, new PrintJobSettings()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PrintLayoutHelper.CreateMetrics(612, value, new PrintJobSettings()));
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void RejectsNonfiniteCellDimensions(double value)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PrintLayoutHelper.CalculateActualCodeSizePoints(value, 200, new PrintJobSettings()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PrintLayoutHelper.CalculateActualCodeSizePoints(200, value, new PrintJobSettings()));
    }
}
