using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Diagnostics;
using System.Globalization;

namespace Simple_QR_Code_Maker.Services;

public class PrintService : IPrintService
{
    private const double PointsPerInch = 72.0;
    private const double MillimetersPerInch = 25.4;
    private const double CellPaddingPoints = 12.0;
    private const double LabelHeightPoints = 20.0;
    private const double LabelSpacingPoints = 6.0;
    private static readonly object FontSettingsGate = new();
    private static bool fontSettingsInitialized;

    public Task<string> GenerateQrPdfAsync(
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot renderSettings,
        PrintJobSettings printSettings,
        CancellationToken cancellationToken = default)
    {
        string pdfPath = CreatePreviewPdfPath();

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePdfSharpFontSettings();

            List<byte[]> pngBytesList = codes.Select(code =>
            {
                using MemoryStream ms = new();
                BarcodeHelpers.SaveQrCodePngToStream(
                    ms,
                    code.CodeAsText,
                    renderSettings.ErrorCorrectionLevel,
                    renderSettings.ForegroundColor,
                    renderSettings.BackgroundColor,
                    renderSettings.LogoImage,
                    renderSettings.LogoSizePercentage,
                    renderSettings.LogoPaddingPixels,
                    renderSettings.QrPaddingModules);
                return ms.ToArray();
            }).ToList();

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using PdfDocument document = new();
                document.Info.Title = "QR Codes";
                document.Info.Creator = "Simple QR Code Maker";

                PageSize pageSize = GetPreviewPageSize();
                int pageCount = (int)Math.Ceiling((double)pngBytesList.Count / printSettings.CodesPerPage);

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PdfPage page = document.AddPage();
                    page.Size = pageSize;
                    DrawPage(page, pageIndex, pngBytesList, codes, printSettings);
                }

                document.Save(pdfPath);
                return pdfPath;
            }
            catch
            {
                TryDeleteFile(pdfPath);
                throw;
            }
        }, cancellationToken);
    }

    private static void DrawPage(
        PdfPage page,
        int pageIndex,
        IList<byte[]> qrPngBytes,
        IReadOnlyList<RequestedQrCodeItem> codes,
        PrintJobSettings settings)
    {
        (int rows, int cols) = GetGridDimensions(settings.CodesPerPage);
        double marginPoints = MillimetersToPoints(settings.MarginMm);
        double usableWidth = Math.Max(page.Width.Point - (marginPoints * 2), 1);
        double usableHeight = Math.Max(page.Height.Point - (marginPoints * 2), 1);
        double cellWidth = usableWidth / cols;
        double cellHeight = usableHeight / rows;

        using XGraphics graphics = XGraphics.FromPdfPage(page);
        graphics.DrawRectangle(XBrushes.White, 0, 0, page.Width.Point, page.Height.Point);

        int start = pageIndex * settings.CodesPerPage;
        int end = Math.Min(start + settings.CodesPerPage, qrPngBytes.Count);

        for (int i = start; i < end; i++)
        {
            int local = i - start;
            XRect cellRect = new(
                marginPoints + ((local % cols) * cellWidth),
                marginPoints + ((local / cols) * cellHeight),
                cellWidth,
                cellHeight);

            DrawCell(graphics, cellRect, qrPngBytes[i], codes[i].CodeAsText, settings.ShowLabels);
        }
    }

    private static void DrawCell(XGraphics graphics, XRect cellRect, byte[] qrPngBytes, string label, bool showLabel)
    {
        XRect paddedRect = new(
            cellRect.X + CellPaddingPoints,
            cellRect.Y + CellPaddingPoints,
            Math.Max(cellRect.Width - (CellPaddingPoints * 2), 1),
            Math.Max(cellRect.Height - (CellPaddingPoints * 2), 1));

        double reservedLabelHeight = showLabel ? LabelHeightPoints + LabelSpacingPoints : 0;
        double imageAreaHeight = Math.Max(paddedRect.Height - reservedLabelHeight, 1);
        double imageSize = Math.Max(Math.Min(paddedRect.Width, imageAreaHeight), 1);
        double imageX = paddedRect.X + ((paddedRect.Width - imageSize) / 2);
        double imageY = paddedRect.Y + ((imageAreaHeight - imageSize) / 2);

        using MemoryStream imageStream = new(qrPngBytes, writable: false);
        using XImage image = XImage.FromStream(imageStream);
        graphics.DrawImage(image, imageX, imageY, imageSize, imageSize);

        if (!showLabel)
            return;

        XFont labelFont = new("Arial", 10);
        XRect labelRect = new(
            paddedRect.X,
            paddedRect.Bottom - LabelHeightPoints,
            paddedRect.Width,
            LabelHeightPoints);

        string trimmedLabel = TrimTextToWidth(graphics, label, labelFont, labelRect.Width);
        graphics.DrawString(trimmedLabel, labelFont, XBrushes.Black, labelRect, XStringFormats.Center);
    }

    private static string TrimTextToWidth(XGraphics graphics, string value, XFont font, double availableWidth)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (graphics.MeasureString(value, font).Width <= availableWidth)
            return value;

        const string ellipsis = "...";
        for (int length = value.Length - 1; length > 0; length--)
        {
            string candidate = $"{value[..length].TrimEnd()}{ellipsis}";
            if (graphics.MeasureString(candidate, font).Width <= availableWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
    }

    private static (int rows, int cols) GetGridDimensions(int codesPerPage) => codesPerPage switch
    {
        1 => (1, 1),
        2 => (2, 1),
        6 => (3, 2),
        9 => (3, 3),
        12 => (4, 3),
        _ => (2, 2),
    };

    private static string CreatePreviewPdfPath()
    {
        string previewDirectory = Path.Combine(Path.GetTempPath(), "Simple_QR_Code_Maker", "PrintPreview");
        Directory.CreateDirectory(previewDirectory);
        return Path.Combine(previewDirectory, $"qr-print-{DateTimeOffset.UtcNow.Ticks}.pdf");
    }

    private static void EnsurePdfSharpFontSettings()
    {
        if (fontSettingsInitialized)
        {
            return;
        }

        lock (FontSettingsGate)
        {
            if (fontSettingsInitialized)
            {
                return;
            }

            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            fontSettingsInitialized = true;
        }
    }

    private static double MillimetersToPoints(double millimeters) => millimeters * PointsPerInch / MillimetersPerInch;

    private static PageSize GetPreviewPageSize()
    {
        try
        {
            RegionInfo region = new(CultureInfo.CurrentCulture.Name);
            return region.IsMetric ? PageSize.A4 : PageSize.Letter;
        }
        catch (ArgumentException)
        {
            return PageSize.Letter;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete print preview file '{path}': {ex.Message}");
        }
    }
}
