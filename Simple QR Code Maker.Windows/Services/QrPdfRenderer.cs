using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using Windows.Foundation;

namespace Simple_QR_Code_Maker.Services;

public static class QrPdfRenderer
{
    private readonly record struct PrintableQrCodeAsset(string Label, byte[] PngBytes, QrImageLayoutMetrics ImageLayout);

    public static Task<byte[]> GenerateQrPdfBytesAsync(
        IReadOnlyList<RequestedQrCodeItem> codes,
        QrRenderSettingsSnapshot renderSettings,
        PrintJobSettings printSettings,
        CancellationToken cancellationToken = default)
    {
        PrintJobSettings normalizedSettings = printSettings.Normalize();

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<PrintableQrCodeAsset> printableCodes = codes.Select(code =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using QrRenderSettingsSnapshot? commonSettings = renderSettings.CreateCommonBrandOverride(code);
                QrRenderSettingsSnapshot effectiveSettings = commonSettings ?? renderSettings;
                string? resolvedFrameText = QrFrameTextResolver.Resolve(
                    effectiveSettings.FramePreset,
                    effectiveSettings.FrameTextSource,
                    effectiveSettings.FrameText,
                    code.CodeAsText,
                    code.ContentKind,
                    code.MultiLineCodeModeOverride);
                using MemoryStream ms = new();
                QrCodeRenderer.SaveQrCodePngToStream(
                    ms,
                    code.CodeAsText,
                    effectiveSettings.ErrorCorrectionLevel,
                    effectiveSettings.ForegroundColor,
                    effectiveSettings.BackgroundColor,
                    effectiveSettings.LogoImage,
                    effectiveSettings.LogoSizePercentage,
                    effectiveSettings.LogoPaddingPixels,
                    effectiveSettings.QrPaddingModules,
                    effectiveSettings.FramePreset,
                    resolvedFrameText,
                    out QrImageLayoutMetrics imageLayout,
                    effectiveSettings.Appearance);
                string label = code.ContentKind switch
                {
                    QrContentKind.VCard => VCardBuilderHelper.GetDisplayName(code.CodeAsText),
                    QrContentKind.WiFi => WifiBuilderHelper.GetDisplayName(code.CodeAsText),
                    QrContentKind.Email => EmailBuilderHelper.GetDisplayName(code.CodeAsText),
                    _ => code.CodeAsText,
                };
                return new PrintableQrCodeAsset(label, ms.ToArray(), imageLayout);
            }).ToList();

            cancellationToken.ThrowIfCancellationRequested();

            using PdfDocumentBuilder document = new();
            document.DocumentInformation.Title = "QR Codes";
            document.DocumentInformation.Creator = "Simple QR Code Maker";
            PdfLabelFont? labelFont = normalizedSettings.ShowLabels ? new PdfLabelFont(document) : null;

            QrImageLayoutMetrics maximumImageLayout = GetMaximumImageLayout(printableCodes);
            PrintLayoutMetrics pageLayout = PrintLayoutHelper.CreateMetrics(normalizedSettings, maximumImageLayout);
            int pageCount = (int)Math.Ceiling((double)printableCodes.Count / pageLayout.CodesPerPage);

            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                PdfPageBuilder page = document.AddPage(pageLayout.PageWidthPoints, pageLayout.PageHeightPoints);
                DrawPage(page, pageIndex, printableCodes, normalizedSettings, maximumImageLayout, labelFont);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return document.Build();
        }, cancellationToken);
    }

    private static void DrawPage(
        PdfPageBuilder page,
        int pageIndex,
        IReadOnlyList<PrintableQrCodeAsset> printableCodes,
        PrintJobSettings settings,
        QrImageLayoutMetrics maximumImageLayout,
        PdfLabelFont? labelFont)
    {
        PrintLayoutMetrics pageLayout = PrintLayoutHelper.CreateMetrics(page.PageSize.Width, page.PageSize.Height, settings, maximumImageLayout);
        page.SetStrokeColor(255, 255, 255);
        page.SetTextAndFillColor(255, 255, 255);
        page.DrawRectangle(new PdfPoint(0, 0), page.PageSize.Width, page.PageSize.Height, 0, fill: true);
        page.ResetColor();

        int start = pageIndex * pageLayout.CodesPerPage;
        int end = Math.Min(start + pageLayout.CodesPerPage, printableCodes.Count);

        for (int i = start; i < end; i++)
        {
            int local = i - start;
            Rect cellRect = new(
                pageLayout.MarginPoints + ((local % pageLayout.Columns) * (pageLayout.CellWidth + pageLayout.SpacingPoints)),
                pageLayout.MarginPoints + ((local / pageLayout.Columns) * (pageLayout.CellHeight + pageLayout.SpacingPoints)),
                pageLayout.CellWidth,
                pageLayout.CellHeight);

            DrawCell(page, cellRect, printableCodes[i], settings, labelFont);
        }
    }

    private static void DrawCell(PdfPageBuilder page, Rect cellRect, PrintableQrCodeAsset printableCode, PrintJobSettings settings, PdfLabelFont? labelFont)
    {
        Rect paddedRect = new(
            cellRect.X + PrintLayoutHelper.CellPaddingPoints,
            cellRect.Y + PrintLayoutHelper.CellPaddingPoints,
            Math.Max(cellRect.Width - (PrintLayoutHelper.CellPaddingPoints * 2), 1),
            Math.Max(cellRect.Height - (PrintLayoutHelper.CellPaddingPoints * 2), 1));

        double reservedLabelHeight = settings.ShowLabels ? PrintLayoutHelper.LabelHeightPoints + PrintLayoutHelper.LabelSpacingPoints : 0;
        double imageAreaHeight = Math.Max(paddedRect.Height - reservedLabelHeight, 1);
        PrintCodePlacement placement = PrintLayoutHelper.CalculateCodePlacement(
            cellRect.Width,
            cellRect.Height,
            settings,
            printableCode.ImageLayout);
        double imageX = paddedRect.X + ((paddedRect.Width - placement.ImageWidthPoints) / 2);
        double imageY = paddedRect.Y + ((imageAreaHeight - placement.ImageHeightPoints) / 2);

        // Layout uses top-left coordinates; PDF coordinates start at the bottom-left.
        double imageBottom = page.PageSize.Height - imageY - placement.ImageHeightPoints;
        page.AddPng(printableCode.PngBytes, new PdfRectangle(
            imageX,
            imageBottom,
            imageX + placement.ImageWidthPoints,
            imageBottom + placement.ImageHeightPoints));

        if (labelFont is null)
            return;

        Rect labelRect = new(
            paddedRect.X,
            paddedRect.Bottom - PrintLayoutHelper.LabelHeightPoints,
            paddedRect.Width,
            PrintLayoutHelper.LabelHeightPoints);

        DrawWrappedLabel(page, printableCode.Label, labelRect, labelFont);
    }

    private static void DrawWrappedLabel(PdfPageBuilder page, string value, Rect labelRect, PdfLabelFont labelFont)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        double fontSize = GetLabelFontSize(page, value, labelFont, labelRect.Width);
        List<string> lines = WrapLabelLines(
            page,
            value,
            labelFont,
            fontSize,
            labelRect.Width,
            PrintLayoutHelper.LabelMaxLineCount,
            out _);
        double lineHeight = fontSize * PrintLayoutHelper.LabelLineHeightMultiplier;
        double totalTextHeight = lines.Count * lineHeight;
        double top = labelRect.Y + Math.Max((labelRect.Height - totalTextHeight) / 2, 0);

        foreach (string line in lines)
        {
            IReadOnlyList<Letter> letters = labelFont.MeasureText(page, line, fontSize);
            double width = GetTextWidth(letters);
            double glyphCenter = (letters.Max(letter => letter.BoundingBox.Top) + letters.Min(letter => letter.BoundingBox.Bottom)) / 2;
            PdfPoint baseline = new(
                labelRect.X + ((labelRect.Width - width) / 2),
                page.PageSize.Height - top - (lineHeight / 2) - glyphCenter);
            labelFont.DrawText(page, line, fontSize, baseline);
            top += lineHeight;
        }
    }

    private static double GetLabelFontSize(PdfPageBuilder page, string value, PdfLabelFont font, double availableWidth)
    {
        for (double fontSize = PrintLayoutHelper.LabelFontSizePoints;
             fontSize >= PrintLayoutHelper.MinimumLabelFontSizePoints;
             fontSize -= 0.5)
        {
            _ = WrapLabelLines(
                page,
                value,
                font,
                fontSize,
                availableWidth,
                PrintLayoutHelper.LabelMaxLineCount,
                out bool fitsCompletely);

            if (fitsCompletely)
                return fontSize;
        }

        return PrintLayoutHelper.MinimumLabelFontSizePoints;
    }

    private static List<string> WrapLabelLines(
        PdfPageBuilder page,
        string value,
        PdfLabelFont font,
        double fontSize,
        double availableWidth,
        int maxLineCount,
        out bool fitsCompletely)
    {
        string normalized = value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        List<string> lines = [];

        if (string.IsNullOrWhiteSpace(normalized))
        {
            fitsCompletely = true;
            return lines;
        }

        int start = 0;
        while (start < normalized.Length && lines.Count < maxLineCount)
        {
            while (start < normalized.Length && char.IsWhiteSpace(normalized[start]))
                start++;

            if (start >= normalized.Length)
                break;

            int nextBreak = FindWrapBreakIndex(page, normalized, start, font, fontSize, availableWidth);
            string line = normalized[start..nextBreak].Trim();
            if (line.Length == 0)
            {
                nextBreak = Math.Min(start + 1, normalized.Length);
                line = normalized[start..nextBreak];
            }

            lines.Add(line);
            start = nextBreak;
        }

        while (start < normalized.Length && char.IsWhiteSpace(normalized[start]))
            start++;

        fitsCompletely = start >= normalized.Length;
        return lines;
    }

    private static int FindWrapBreakIndex(
        PdfPageBuilder page,
        string value,
        int start,
        PdfLabelFont font,
        double fontSize,
        double availableWidth)
    {
        int lastPreferredBreak = -1;

        for (int index = start + 1; index <= value.Length; index++)
        {
            string candidate = value[start..index].TrimEnd();
            if (candidate.Length == 0)
                continue;

            if (GetTextWidth(font.MeasureText(page, candidate, fontSize)) <= availableWidth)
            {
                if (index < value.Length && IsPreferredWrapBoundary(value[index - 1]))
                    lastPreferredBreak = index;

                continue;
            }

            if (lastPreferredBreak > start)
                return lastPreferredBreak;

            return Math.Max(start + 1, index - 1);
        }

        return value.Length;
    }

    private static bool IsPreferredWrapBoundary(char character)
    {
        return char.IsWhiteSpace(character)
            || character is '-' or '_' or '/' or '\\' or '.' or '?' or '&' or '=';
    }


    private static double GetTextWidth(IReadOnlyList<Letter> letters)
    {
        return letters.Count == 0 ? 0 : letters[^1].EndBaseLine.X;
    }

    private sealed class PdfLabelFont
    {
        private readonly PdfDocumentBuilder _document;
        private readonly byte[] _fontBytes;
        private readonly HashSet<char> _characters = [];
        private PdfDocumentBuilder.AddedFont _font;

        internal PdfLabelFont(PdfDocumentBuilder document)
        {
            _document = document;
            _fontBytes = File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"));
            _font = document.AddTrueTypeFont(_fontBytes);
        }

        internal IReadOnlyList<Letter> MeasureText(PdfPageBuilder page, string text, double fontSize)
        {
            return page.MeasureText(text, fontSize, new PdfPoint(0, 0), _font);
        }

        internal void DrawText(PdfPageBuilder page, string text, double fontSize, PdfPoint baseline)
        {
            int start = 0;
            for (int index = 0; index < text.Length; index++)
            {
                if (_characters.Contains(text[index]))
                    continue;

                // PdfPig's simple TrueType fonts can encode only 255 distinct characters.
                if (_characters.Count == byte.MaxValue)
                {
                    string run = text[start..index];
                    page.AddText(run, fontSize, baseline, _font);
                    baseline = new PdfPoint(baseline.X + GetTextWidth(MeasureText(page, run, fontSize)), baseline.Y);
                    _font = _document.AddTrueTypeFont(_fontBytes);
                    _characters.Clear();
                    start = index;
                }

                _characters.Add(text[index]);
            }

            page.AddText(text[start..], fontSize, baseline, _font);
        }
    }


    private static QrImageLayoutMetrics GetMaximumImageLayout(IReadOnlyList<PrintableQrCodeAsset> printableCodes)
    {
        if (printableCodes.Count == 0)
        {
            return QrImageLayoutMetrics.Square;
        }

        double maxWidthPerQrSize = printableCodes.Max(static code => code.ImageLayout.WidthPerQrSize);
        double maxHeightPerQrSize = printableCodes.Max(static code => code.ImageLayout.HeightPerQrSize);
        return QrImageLayoutMetrics.FromScaleProfile(maxWidthPerQrSize, maxHeightPerQrSize);
    }

}