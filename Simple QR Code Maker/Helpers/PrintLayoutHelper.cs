using PdfSharp;
using PdfSharp.Drawing;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Helpers;

internal readonly record struct PrintLayoutMetrics(
    int Rows,
    int Columns,
    int CodesPerPage,
    double MarginPoints,
    double SpacingPoints,
    double CellWidth,
    double CellHeight,
    double RequestedCodeSizePoints,
    double ActualCodeSizePoints,
    double PageWidthPoints,
    double PageHeightPoints);

internal readonly record struct PrintCodePlacement(
    double ImageWidthPoints,
    double ImageHeightPoints,
    double ActualCodeSizePoints);

internal static class PrintLayoutHelper
{
    internal const double PointsPerInch = 72.0;
    internal const double MillimetersPerInch = 25.4;
    internal const double CellPaddingPoints = 12.0;
    internal const double LabelFontSizePoints = 10.0;
    internal const double MinimumLabelFontSizePoints = 6.0;
    internal const double LabelLineHeightMultiplier = 1.2;
    internal const int LabelMaxLineCount = 2;
    internal const double LabelHeightPoints = LabelFontSizePoints * LabelLineHeightMultiplier * LabelMaxLineCount;
    internal const double LabelSpacingPoints = 6.0;

    internal static PrintLayoutMetrics CreateMetrics(PrintJobSettings settings)
    {
        return CreateMetrics(settings, QrImageLayoutMetrics.Square);
    }

    internal static PrintLayoutMetrics CreateMetrics(PrintJobSettings settings, QrImageLayoutMetrics imageLayoutMetrics)
    {
        PrintJobSettings normalizedSettings = settings.Normalize();
        PageSize pageSize = GetPageSize(normalizedSettings.PageType);
        (double pageWidthPoints, double pageHeightPoints) = GetPageDimensions(pageSize, normalizedSettings.PageLayout);
        return CreateMetrics(pageWidthPoints, pageHeightPoints, normalizedSettings, imageLayoutMetrics);
    }

    internal static PrintLayoutMetrics CreateMetrics(double pageWidthPoints, double pageHeightPoints, PrintJobSettings settings)
    {
        return CreateMetrics(pageWidthPoints, pageHeightPoints, settings, QrImageLayoutMetrics.Square);
    }

    internal static PrintLayoutMetrics CreateMetrics(double pageWidthPoints, double pageHeightPoints, PrintJobSettings settings, QrImageLayoutMetrics imageLayoutMetrics)
    {
        PrintJobSettings normalizedSettings = settings.Normalize();
        double marginPoints = MillimetersToPoints(normalizedSettings.MarginMm);
        double spacingPoints = MillimetersToPoints(normalizedSettings.SpacingMm);
        double availableWidth = Math.Max(pageWidthPoints - (marginPoints * 2), 1);
        double availableHeight = Math.Max(pageHeightPoints - (marginPoints * 2), 1);

        (int rows, int columns) = normalizedSettings.FitAsManyAsPossible
            ? GetAutoFitGridDimensions(availableWidth, availableHeight, normalizedSettings, spacingPoints, imageLayoutMetrics)
            : GetGridDimensions(normalizedSettings.CodesPerPage, normalizedSettings.PageLayout);

        double usableWidth = Math.Max(availableWidth - (spacingPoints * (columns - 1)), 1);
        double usableHeight = Math.Max(availableHeight - (spacingPoints * (rows - 1)), 1);
        double cellWidth = usableWidth / columns;
        double cellHeight = usableHeight / rows;
        double requestedCodeSizePoints = MillimetersToPoints(normalizedSettings.CodeSizeMm);
        PrintCodePlacement placement = CalculateCodePlacement(cellWidth, cellHeight, normalizedSettings, imageLayoutMetrics);

        return new PrintLayoutMetrics(
            rows,
            columns,
            Math.Max(rows * columns, 1),
            marginPoints,
            spacingPoints,
            cellWidth,
            cellHeight,
            requestedCodeSizePoints,
            placement.ActualCodeSizePoints,
            pageWidthPoints,
            pageHeightPoints);
    }

    internal static double CalculateActualCodeSizePoints(double cellWidth, double cellHeight, PrintJobSettings settings)
    {
        return CalculateActualCodeSizePoints(cellWidth, cellHeight, settings, QrImageLayoutMetrics.Square);
    }

    internal static double CalculateActualCodeSizePoints(double cellWidth, double cellHeight, PrintJobSettings settings, QrImageLayoutMetrics imageLayoutMetrics)
    {
        return CalculateCodePlacement(cellWidth, cellHeight, settings, imageLayoutMetrics).ActualCodeSizePoints;
    }

    internal static PrintCodePlacement CalculateCodePlacement(double cellWidth, double cellHeight, PrintJobSettings settings, QrImageLayoutMetrics imageLayoutMetrics)
    {
        PrintJobSettings normalizedSettings = settings.Normalize();
        double reservedLabelHeight = normalizedSettings.ShowLabels ? LabelHeightPoints + LabelSpacingPoints : 0;
        double paddedWidth = Math.Max(cellWidth - (CellPaddingPoints * 2), 1);
        double paddedHeight = Math.Max(cellHeight - (CellPaddingPoints * 2), 1);
        double imageAreaHeight = Math.Max(paddedHeight - reservedLabelHeight, 1);
        double requestedCodeSizePoints = MillimetersToPoints(normalizedSettings.CodeSizeMm);
        double requestedImageWidth = requestedCodeSizePoints * imageLayoutMetrics.WidthPerQrSize;
        double requestedImageHeight = requestedCodeSizePoints * imageLayoutMetrics.HeightPerQrSize;
        double widthScale = requestedImageWidth <= 0 ? 1 : paddedWidth / requestedImageWidth;
        double heightScale = requestedImageHeight <= 0 ? 1 : imageAreaHeight / requestedImageHeight;
        double appliedScale = Math.Max(Math.Min(Math.Min(widthScale, heightScale), 1), 0);

        return new PrintCodePlacement(
            Math.Max(requestedImageWidth * appliedScale, 1),
            Math.Max(requestedImageHeight * appliedScale, 1),
            Math.Max(requestedCodeSizePoints * appliedScale, 1));
    }

    internal static double MillimetersToPoints(double millimeters) => millimeters * PointsPerInch / MillimetersPerInch;

    internal static double PointsToMillimeters(double points) => points * MillimetersPerInch / PointsPerInch;

    internal static (double WidthPoints, double HeightPoints) GetPageDimensions(PageSize pageSize, PrintPageLayout pageLayout)
    {
        XSize portraitSize = PageSizeConverter.ToSize(pageSize);
        return pageLayout == PrintPageLayout.Landscape
            ? (portraitSize.Height, portraitSize.Width)
            : (portraitSize.Width, portraitSize.Height);
    }

    internal static PageSize GetPageSize(PrintPageType pageType)
    {
        return PrintPageTypeHelper.Resolve(pageType) switch
        {
            PrintPageType.A3 => PageSize.A3,
            PrintPageType.A4 => PageSize.A4,
            PrintPageType.A5 => PageSize.A5,
            PrintPageType.B4 => PageSize.B4,
            PrintPageType.B5 => PageSize.B5,
            PrintPageType.Letter => PageSize.Letter,
            PrintPageType.Legal => PageSize.Legal,
            PrintPageType.Statement => PageSize.Statement,
            PrintPageType.Executive => PageSize.Executive,
            PrintPageType.Tabloid => PageSize.Tabloid,
            PrintPageType.Ledger => PageSize.Ledger,
            _ => PageSize.Letter,
        };
    }

    private static (int Rows, int Columns) GetGridDimensions(int codesPerPage, PrintPageLayout pageLayout) => codesPerPage switch
    {
        1 => (1, 1),
        2 => pageLayout == PrintPageLayout.Landscape ? (1, 2) : (2, 1),
        6 => pageLayout == PrintPageLayout.Landscape ? (2, 3) : (3, 2),
        9 => (3, 3),
        12 => pageLayout == PrintPageLayout.Landscape ? (3, 4) : (4, 3),
        16 => (4, 4),
        _ => (2, 2),
    };

    private static (int Rows, int Columns) GetAutoFitGridDimensions(
        double availableWidth,
        double availableHeight,
        PrintJobSettings settings,
        double spacingPoints,
        QrImageLayoutMetrics imageLayoutMetrics)
    {
        double requestedCodeSizePoints = MillimetersToPoints(settings.CodeSizeMm);
        double reservedLabelHeight = settings.ShowLabels ? LabelHeightPoints + LabelSpacingPoints : 0;
        double requestedImageWidth = requestedCodeSizePoints * imageLayoutMetrics.WidthPerQrSize;
        double requestedImageHeight = requestedCodeSizePoints * imageLayoutMetrics.HeightPerQrSize;
        double requiredCellWidth = Math.Max(requestedImageWidth + (CellPaddingPoints * 2), 1);
        double requiredCellHeight = Math.Max(requestedImageHeight + reservedLabelHeight + (CellPaddingPoints * 2), 1);

        int columns = Math.Max(1, (int)Math.Floor((availableWidth + spacingPoints) / (requiredCellWidth + spacingPoints)));
        int rows = Math.Max(1, (int)Math.Floor((availableHeight + spacingPoints) / (requiredCellHeight + spacingPoints)));
        return (rows, columns);
    }
}
