using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Windows.UI;

namespace Simple_QR_Code_Maker.Windows.Tests.Rendering;

[TestClass]
public sealed class SizeRecommendationTests
{
    private static Color Black => Color.FromArgb(255, 0, 0, 0);
    private static Color White => Color.FromArgb(255, 255, 255, 255);

    [TestMethod]
    public void ContrastMatchesKnownWcagRatiosAndIsSymmetric()
    {
        Assert.AreEqual(21, ColorHelpers.GetContrastRatio(Black, White), 1e-10);
        Assert.AreEqual(1, ColorHelpers.GetContrastRatio(Black, Black), 1e-10);
        Color gray = Color.FromArgb(255, 128, 128, 128);
        Assert.AreEqual(3.94943965, ColorHelpers.GetContrastRatio(gray, White), 1e-6);
        Assert.AreEqual(ColorHelpers.GetContrastRatio(gray, White), ColorHelpers.GetContrastRatio(White, gray), 1e-10);
        Assert.AreEqual(1, QrCodeRenderer.ContrastRatioLossFrac(21), 1e-10);
        Assert.AreEqual(0.8, QrCodeRenderer.ContrastRatioLossFrac(2.5), 1e-10);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(4, true)]
    [DataRow(5, false)]
    [DataRow(10, false)]
    public void OnlyPredictableQuietZonesHaveExactRecommendations(int padding, bool expected)
        => Assert.AreEqual(expected, QrCodeRenderer.IsSizeRecommendationAvailableForPadding(padding));

    [TestMethod]
    public void MinimumSizeMatchesFormulaGrowsWithDistanceModulesPaddingAndContrastLoss()
    {
        const double distance = 64;
        double expectedInches = (distance + 2.721) / 1759.1 * 25;
        Assert.AreEqual(expectedInches, QrCodeRenderer.SmallestCodeSide(distance, 21), 1e-10);
        Assert.AreEqual(0, QrCodeRenderer.SmallestCodeSide(1, 21));
        Assert.IsTrue(QrCodeRenderer.TryGetMinimumRecommendedSideMillimeters(distance, 21, Black, White, out double minimum));
        Assert.AreEqual(expectedInches * 25.4, minimum, 1e-10);
        Assert.IsTrue(QrCodeRenderer.TryGetMinimumRecommendedSideMillimeters(128, 21, Black, White, out double farther));
        Assert.IsTrue(farther > minimum);
        Assert.IsTrue(QrCodeRenderer.TryGetMinimumRecommendedSideMillimeters(distance, 41, Black, White, out double denser));
        Assert.IsTrue(denser > minimum);
        Assert.IsTrue(QrCodeRenderer.TryGetMinimumRecommendedSideMillimeters(distance, 21, Black, White, out double padded, 4));
        Assert.IsTrue(padded > minimum);
        Assert.IsTrue(QrCodeRenderer.TryGetMinimumRecommendedSideMillimeters(distance, 21, Color.FromArgb(255, 100, 100, 100), White, out double reducedContrast));
        Assert.IsTrue(reducedContrast > minimum);
        Assert.IsTrue(QrCodeRenderer.GetSizeRecommendation(distance, 21, Black, White).IsExact);
    }

    [TestMethod]
    public void RecommendationClassifiesTransparencyPaddingLowContrastAndShortDistance()
    {
        AssertKind(QrCodeSizeRecommendationKind.TransparencyDependent, Color.FromArgb(254, 0, 0, 0), White, 2, 64);
        AssertKind(QrCodeSizeRecommendationKind.TransparencyDependent, Black, Color.FromArgb(0, 255, 255, 255), 2, 64);
        AssertKind(QrCodeSizeRecommendationKind.PaddingDependent, Black, White, 0, 64);
        AssertKind(QrCodeSizeRecommendationKind.PaddingDependent, Black, White, 5, 64);
        AssertKind(QrCodeSizeRecommendationKind.LowContrast, White, White, 2, 64);
        AssertKind(QrCodeSizeRecommendationKind.Error, Black, White, 2, 1);
    }

    [TestMethod]
    [DataRow(double.NaN, 21)]
    [DataRow(double.PositiveInfinity, 21)]
    [DataRow(-1.0, 21)]
    [DataRow(64.0, 0)]
    [DataRow(64.0, -1)]
    public void InvalidDistanceOrModuleCountsNeverProduceExactNanRecommendations(double distance, int modules)
    {
        Assert.AreEqual(0, QrCodeRenderer.SmallestCodeSide(distance, modules));
        Assert.IsFalse(QrCodeRenderer.TryGetMinimumRecommendedSideMillimeters(distance, modules, Black, White, out double side));
        Assert.AreEqual(0, side);
        Assert.AreEqual(QrCodeSizeRecommendationKind.Error, QrCodeRenderer.GetSizeRecommendation(distance, modules, Black, White).Kind);
    }

    private static void AssertKind(QrCodeSizeRecommendationKind expected, Color foreground, Color background, double padding, double distance)
    {
        var recommendation = QrCodeRenderer.GetSizeRecommendation(distance, 21, foreground, background, padding);
        Assert.AreEqual(expected, recommendation.Kind);
        Assert.IsFalse(recommendation.IsExact);
        Assert.IsFalse(string.IsNullOrWhiteSpace(recommendation.Text));
        Assert.IsFalse(QrCodeRenderer.TryGetMinimumRecommendedSideMillimeters(distance, 21, foreground, background, out double size, padding));
        Assert.AreEqual(0, size);
    }
}
