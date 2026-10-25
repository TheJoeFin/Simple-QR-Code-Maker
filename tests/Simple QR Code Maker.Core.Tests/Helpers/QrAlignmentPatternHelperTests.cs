using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Core.Tests.Helpers;

[TestClass]
public sealed class QrAlignmentPatternHelperTests
{
    [TestMethod]
    [DataRow(1, 21)]
    [DataRow(7, 45)]
    [DataRow(40, 177)]
    public void VersionDeterminesModuleDimension(int version, int expected)
    {
        Assert.AreEqual(expected, QrAlignmentPatternHelper.GetDimension(version));
    }

    [TestMethod]
    public void MatchesIndependentSmallVersionCenters()
    {
        Assert.AreEqual(0, QrAlignmentPatternHelper.GetAlignmentPatternCenters(1).Count);
        CollectionAssert.AreEqual(new[] { (18, 18) }, QrAlignmentPatternHelper.GetAlignmentPatternCenters(2));
        CollectionAssert.AreEqual(new[] { (6, 22), (22, 6), (22, 22), (22, 38), (38, 22), (38, 38) },
            QrAlignmentPatternHelper.GetAlignmentPatternCenters(7));
    }

    [TestMethod]
    [DataRow(36, "6,24,50,76,102,128,154")]
    [DataRow(37, "6,28,54,80,106,132,158")]
    [DataRow(38, "6,32,58,84,110,136,162")]
    [DataRow(39, "6,26,54,82,110,138,166")]
    [DataRow(40, "6,30,58,86,114,142,170")]
    public void HighVersionCoordinatesMatchIsoAlignmentPositions(int version, string expectedPositions)
    {
        int[] expected = expectedPositions.Split(',').Select(int.Parse).ToArray();
        int[] actual = QrAlignmentPatternHelper.GetAlignmentPatternCenters(version).Select(static point => point.Row).Distinct().Order().ToArray();
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void AllVersionsKeepCentersInBoundsAndOutsideFinderZones()
    {
        for (int version = 2; version <= 40; version++)
        {
            int dimension = QrAlignmentPatternHelper.GetDimension(version);
            List<(int Row, int Col)> centers = QrAlignmentPatternHelper.GetAlignmentPatternCenters(version);
            Assert.AreEqual(centers.Count, centers.Distinct().Count());
            foreach ((int row, int column) in centers)
            {
                Assert.IsTrue(row >= 2 && row < dimension - 2 && column >= 2 && column < dimension - 2, $"Version {version}: ({row}, {column})");
                Assert.IsFalse(row <= 8 && column <= 8);
                Assert.IsFalse(row <= 8 && column >= dimension - 8);
                Assert.IsFalse(row >= dimension - 8 && column <= 8);
            }
        }
    }

    [TestMethod]
    public void PixelCoordinatesUseModuleCentersAndCorrectAxes()
    {
        (double x, double y) = QrAlignmentPatternHelper.GetIdealPixelCoord(1, 2, 1, 210);
        Assert.AreEqual(25, x, 1e-12);
        Assert.AreEqual(15, y, 1e-12);
    }

    [TestMethod]
    public void BilinearInterpolationUsesAllFourCorners()
    {
        PointF center = QrAlignmentPatternHelper.EstimateImagePosition(new(0, 0), new(200, 0), new(0, 100), new(300, 200), 10, 10, 1);
        Assert.AreEqual(125f, center.X, 1e-5f);
        Assert.AreEqual(75f, center.Y, 1e-5f);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(41)]
    public void InvalidVersionsAreRejectedByGeometryAndHaveNoCenters(int version)
    {
        Assert.AreEqual(0, QrAlignmentPatternHelper.GetAlignmentPatternCenters(version).Count);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QrAlignmentPatternHelper.GetDimension(version));
    }

    [TestMethod]
    public void RejectsInvalidModulePositionsAndOutputSize()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QrAlignmentPatternHelper.GetIdealPixelCoord(-1, 0, 1, 210));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QrAlignmentPatternHelper.GetIdealPixelCoord(0, 21, 1, 210));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QrAlignmentPatternHelper.GetIdealPixelCoord(0, 0, 1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QrAlignmentPatternHelper.EstimateImagePosition(default, default, default, default, 21, 0, 1));
    }
}
