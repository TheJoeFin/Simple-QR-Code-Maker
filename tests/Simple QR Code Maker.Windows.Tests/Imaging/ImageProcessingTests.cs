using System;
using System.Collections.Generic;
using System.Drawing;
using ImageMagick;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.Windows.Tests.Imaging;

[TestClass]
public sealed class ImageProcessingTests
{
    [TestMethod]
    public void GrayscaleAndInvertReturnIndependentImagesWithExpectedChannels()
    {
        using Bitmap bitmap = new(8, 6);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            graphics.Clear(Color.FromArgb(255, 20, 80, 160));
        using MagickImage source = ImageProcessingHelper.LoadImageFromBitmap(bitmap);
        using MagickImage grayscale = ImageProcessingHelper.ApplyGrayscale(source);
        using MagickImage inverted = ImageProcessingHelper.InvertColors(source);
        Color gray = Pixel(grayscale, 2, 2);
        Assert.AreEqual(gray.R, gray.G);
        Assert.AreEqual(gray.G, gray.B);
        Color inverse = Pixel(inverted, 2, 2);
        Assert.AreEqual(235, (int)inverse.R);
        Assert.AreEqual(175, (int)inverse.G);
        Assert.AreEqual(95, (int)inverse.B);
        Assert.AreEqual(Color.FromArgb(255, 20, 80, 160).ToArgb(), Pixel(source, 2, 2).ToArgb());
        Assert.AreNotSame(source, grayscale);
        Assert.AreNotSame(source, inverted);
    }

    [TestMethod]
    public void ContrastAndLevelsTransformLuminanceWithoutMutatingSource()
    {
        using MagickImage source = CreateGrayRamp();
        using MagickImage neutral = ImageProcessingHelper.AdjustContrast(source, 0);
        Assert.AreEqual(Pixel(source, 1, 1).ToArgb(), Pixel(neutral, 1, 1).ToArgb());
        using MagickImage contrast = ImageProcessingHelper.AdjustContrast(source, 30);
        Assert.IsTrue(Pixel(contrast, 1, 1).R < Pixel(source, 1, 1).R);
        Assert.IsTrue(Pixel(contrast, 13, 1).R > Pixel(source, 13, 1).R);
        using MagickImage levels = ImageProcessingHelper.AdjustLevels(source, 25, 75);
        Assert.AreEqual(0, (int)Pixel(levels, 1, 1).R);
        Assert.AreEqual(255, (int)Pixel(levels, 13, 1).R);
        Assert.AreEqual(32, (int)Pixel(source, 1, 1).R);
        Assert.AreEqual(224, (int)Pixel(source, 13, 1).R);
    }

    [TestMethod]
    public void BlackAndWhitePointsUseQ16QuantumRangeRatherThanByteRange()
    {
        using MagickImage source = CreateGrayRamp();
        using MagickImage black = ImageProcessingHelper.SetBlackPoint(source, new MagickColor("#404040"));
        using MagickImage white = ImageProcessingHelper.SetWhitePoint(source, new MagickColor("#C0C0C0"));
        Assert.AreEqual(0, (int)Pixel(black, 1, 1).R);
        Assert.AreEqual(255, (int)Pixel(white, 13, 1).R);
        Assert.IsTrue(Pixel(black, 9, 1).R is > 150 and < 200, "A 25% black point should retain mid/high range detail.");
        Assert.IsTrue(Pixel(white, 1, 1).R is > 35 and < 50, "A 75% white point must not crush the full image to black.");
        Assert.AreEqual(32, (int)Pixel(source, 1, 1).R);
    }

    [TestMethod]
    public void CropAndBorderHaveExactDimensionsAndDoNotChangeOriginal()
    {
        using Bitmap bitmap = new(20, 10);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Blue);
            graphics.FillRectangle(Brushes.Red, 5, 2, 6, 4);
        }
        using MagickImage source = ImageProcessingHelper.LoadImageFromBitmap(bitmap);
        using MagickImage crop = ImageProcessingHelper.CropRegion(source, 5, 2, 6, 4);
        Assert.AreEqual(6u, crop.Width);
        Assert.AreEqual(4u, crop.Height);
        Assert.AreEqual(Color.Red.ToArgb(), Pixel(crop, 0, 0).ToArgb());
        Assert.AreEqual(0, crop.Page.X);
        Assert.AreEqual(0, crop.Page.Y);
        using MagickImage border = ImageProcessingHelper.AddWhiteBorder(source, 10);
        Assert.AreEqual(24u, border.Width);
        Assert.AreEqual(14u, border.Height);
        Assert.AreEqual(Color.White.ToArgb(), Pixel(border, 0, 0).ToArgb());
        Assert.AreEqual(Color.Blue.ToArgb(), Pixel(border, 2, 2).ToArgb());
        Assert.AreEqual(Color.Red.ToArgb(), Pixel(border, 7, 4).ToArgb());
        Assert.AreEqual(20u, source.Width);
        Assert.AreEqual(10u, source.Height);
    }

    [TestMethod]
    public void PerspectiveAndPolynomialTransformsUseRequestedGeometry()
    {
        using MagickImage source = new(MagickColors.Red, 32, 32);
        using MagickImage corrected = ImageProcessingHelper.CorrectPerspectiveDistortion(source, new(5, 5), new(25, 5), new(25, 25), new(5, 25), 2);
        Assert.AreEqual(24u, corrected.Width);
        Assert.AreEqual(24u, corrected.Height);
        Assert.AreEqual(Color.White.ToArgb(), Pixel(corrected, 0, 0).ToArgb());
        Assert.AreEqual(Color.Red.ToArgb(), Pixel(corrected, 12, 12).ToArgb());
        List<(Point src, PointF dst)> points = [(new(0, 0), new(0, 0)), (new(31, 0), new(31, 0)), (new(0, 31), new(0, 31))];
        using MagickImage unwarped = ImageProcessingHelper.ApplyPolynomialUnwarp(source, points, 20, 16, 2);
        Assert.AreEqual(24u, unwarped.Width);
        Assert.AreEqual(20u, unwarped.Height);
        Assert.AreEqual(32u, source.Width);
    }

    [TestMethod]
    public void PerspectiveRejectsOutOfBoundsDegenerateCrossedAndTooSmallCorners()
    {
        using MagickImage source = new(MagickColors.White, 32, 32);
        Assert.ThrowsExactly<ArgumentException>(() => ImageProcessingHelper.CorrectPerspectiveDistortion(source, new(-1, 0), new(20, 0), new(20, 20), new(0, 20)));
        Assert.ThrowsExactly<ArgumentException>(() => ImageProcessingHelper.CorrectPerspectiveDistortion(source, new(1, 1), new(1, 1), new(1, 1), new(1, 1)));
        Assert.ThrowsExactly<ArgumentException>(() => ImageProcessingHelper.CorrectPerspectiveDistortion(source, new(0, 0), new(10, 10), new(20, 20), new(30, 30)));
        Assert.ThrowsExactly<ArgumentException>(() => ImageProcessingHelper.CorrectPerspectiveDistortion(source, new(0, 0), new(20, 20), new(20, 0), new(0, 20)));
        Assert.ThrowsExactly<ArgumentException>(() => ImageProcessingHelper.CorrectPerspectiveDistortion(source, new(0, 0), new(6, 0), new(6, 6), new(0, 6)));
        Assert.ThrowsExactly<ArgumentException>(() => ImageProcessingHelper.ApplyPolynomialUnwarp(source, [], 20, 20));
    }

    [TestMethod]
    public void BitmapConversionRemainsUsableAfterSourceAndInternalStreamsAreDisposed()
    {
        Bitmap converted;
        using (MagickImage image = new(MagickColors.Blue, 7, 5))
            converted = ImageProcessingHelper.ConvertToBitmap(image);
        using (converted)
        {
            Assert.AreEqual(7, converted.Width);
            Assert.AreEqual(5, converted.Height);
            Assert.AreEqual(Color.Blue.ToArgb(), converted.GetPixel(3, 2).ToArgb());
            converted.SetPixel(3, 2, Color.Red);
            using System.IO.MemoryStream stream = new();
            converted.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            Assert.IsTrue(stream.Length > 0);
        }
    }

    private static MagickImage CreateGrayRamp()
    {
        using Bitmap bitmap = new(16, 4);
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                int value = x < 4 ? 32 : x < 8 ? 64 : x < 12 ? 192 : 224;
                bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
            }
        return ImageProcessingHelper.LoadImageFromBitmap(bitmap);
    }

    private static Color Pixel(MagickImage image, int x, int y)
    {
        using Bitmap bitmap = ImageProcessingHelper.ConvertToBitmap(image);
        return bitmap.GetPixel(x, y);
    }
}
