using Simple_QR_Code_Maker.Core.Models;
using Simple_QR_Code_Maker.Core.Services;
using SkiaSharp;
using System.Globalization;
using System.Text;

namespace Simple_QR_Code_Maker.Rendering;

public sealed class QrRenderer
{
    private readonly QrMatrixEncoder matrixEncoder;

    public QrRenderer(QrMatrixEncoder matrixEncoder)
    {
        this.matrixEncoder = matrixEncoder;
    }

    public byte[] RenderPng(QrRenderRequest request)
    {
        ValidateRequest(request);

        QrMatrix matrix = matrixEncoder.Encode(request.Content, request.ErrorCorrectionLevel);
        int moduleCount = matrix.Size + (request.QuietZoneModules * 2);
        int moduleSize = Math.Max(1, request.PixelSize / moduleCount);
        int imageSize = moduleCount * moduleSize;

        using SKBitmap bitmap = new(imageSize, imageSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(ToSkColor(request.Background));

        using SKPaint paint = new()
        {
            Color = ToSkColor(request.Foreground),
            IsAntialias = false,
            Style = SKPaintStyle.Fill,
        };

        for (int row = 0; row < matrix.Size; row++)
        {
            for (int column = 0; column < matrix.Size; column++)
            {
                if (!matrix[row, column])
                    continue;

                float x = (column + request.QuietZoneModules) * moduleSize;
                float y = (row + request.QuietZoneModules) * moduleSize;
                canvas.DrawRect(x, y, moduleSize, moduleSize, paint);
            }
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public string RenderSvg(QrRenderRequest request)
    {
        ValidateRequest(request);

        QrMatrix matrix = matrixEncoder.Encode(request.Content, request.ErrorCorrectionLevel);
        int moduleCount = matrix.Size + (request.QuietZoneModules * 2);
        StringBuilder builder = new();
        string background = ToCssColor(request.Background);
        string foreground = ToCssColor(request.Foreground);

        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ");
        builder.Append(moduleCount.ToString(CultureInfo.InvariantCulture));
        builder.Append(' ');
        builder.Append(moduleCount.ToString(CultureInfo.InvariantCulture));
        builder.Append("\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"");
        builder.Append(background);
        builder.Append("\"/><path fill=\"");
        builder.Append(foreground);
        builder.Append("\" d=\"");

        for (int row = 0; row < matrix.Size; row++)
        {
            for (int column = 0; column < matrix.Size; column++)
            {
                if (!matrix[row, column])
                    continue;

                builder.Append('M');
                builder.Append((column + request.QuietZoneModules).ToString(CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append((row + request.QuietZoneModules).ToString(CultureInfo.InvariantCulture));
                builder.Append("h1v1h-1z");
            }
        }

        builder.Append("\"/></svg>");
        return builder.ToString();
    }

    private static void ValidateRequest(QrRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PixelSize is < 128 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(request), "Pixel size must be between 128 and 4096.");
        if (request.QuietZoneModules is < 0 or > 16)
            throw new ArgumentOutOfRangeException(nameof(request), "Quiet zone must be between zero and sixteen modules.");
    }

    private static SKColor ToSkColor(QrColor color) => new(color.Red, color.Green, color.Blue, color.Alpha);

    private static string ToCssColor(QrColor color) =>
        $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}{color.Alpha:X2}";
}
