using Simple_QR_Code_Maker.Rendering;

namespace Simple_QR_Code_Maker.Maui.Services;

public enum QrOutputFormat
{
    Png,
    Svg,
}

public sealed record QrPreparedOutput(string Path, string ContentType);

public interface IQrOutputPreparationService
{
    Task<QrPreparedOutput> PrepareAsync(QrRenderRequest request, QrOutputFormat format, CancellationToken cancellationToken = default);
}

public sealed class QrOutputPreparationService : IQrOutputPreparationService
{
    private readonly QrRenderer renderer;

    public QrOutputPreparationService(QrRenderer renderer)
    {
        this.renderer = renderer;
    }

    public async Task<QrPreparedOutput> PrepareAsync(
        QrRenderRequest request,
        QrOutputFormat format,
        CancellationToken cancellationToken = default)
    {
        string outputDirectory = Path.Combine(FileSystem.CacheDirectory, "prepared-qr-output");
        Directory.CreateDirectory(outputDirectory);

        return format switch
        {
            QrOutputFormat.Png => await PreparePngAsync(request, outputDirectory, cancellationToken),
            QrOutputFormat.Svg => await PrepareSvgAsync(request, outputDirectory, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    private async Task<QrPreparedOutput> PreparePngAsync(
        QrRenderRequest request,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(outputDirectory, "qr-code.png");
        byte[] png = await Task.Run(() => renderer.RenderPng(request), cancellationToken);
        await File.WriteAllBytesAsync(path, png, cancellationToken);
        return new QrPreparedOutput(path, "image/png");
    }

    private async Task<QrPreparedOutput> PrepareSvgAsync(
        QrRenderRequest request,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(outputDirectory, "qr-code.svg");
        string svg = await Task.Run(() => renderer.RenderSvg(request), cancellationToken);
        await File.WriteAllTextAsync(path, svg, cancellationToken);
        return new QrPreparedOutput(path, "image/svg+xml");
    }
}
