using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Models;

public sealed class QrRenderSettingsSnapshot : IDisposable
{
    private QrRenderSettingsSnapshot(
        ErrorCorrectionLevel errorCorrectionLevel,
        System.Drawing.Color foregroundColor,
        System.Drawing.Color backgroundColor,
        System.Drawing.Bitmap? logoImage,
        double logoSizePercentage,
        double logoPaddingPixels,
        string? logoSvgContent,
        double qrPaddingModules,
        QrFramePreset framePreset,
        string? frameText)
    {
        ErrorCorrectionLevel = errorCorrectionLevel;
        ForegroundColor = foregroundColor;
        BackgroundColor = backgroundColor;
        LogoImage = logoImage;
        LogoSizePercentage = logoSizePercentage;
        LogoPaddingPixels = logoPaddingPixels;
        LogoSvgContent = logoSvgContent;
        QrPaddingModules = qrPaddingModules;
        FramePreset = framePreset;
        FrameText = frameText;
    }

    public ErrorCorrectionLevel ErrorCorrectionLevel { get; }

    public System.Drawing.Color ForegroundColor { get; }

    public System.Drawing.Color BackgroundColor { get; }

    public System.Drawing.Bitmap? LogoImage { get; }

    public double LogoSizePercentage { get; }

    public double LogoPaddingPixels { get; }

    public string? LogoSvgContent { get; }

    public double QrPaddingModules { get; }

    public QrFramePreset FramePreset { get; }

    public string? FrameText { get; }

    public static QrRenderSettingsSnapshot Create(
        ErrorCorrectionLevel errorCorrectionLevel,
        System.Drawing.Color foregroundColor,
        System.Drawing.Color backgroundColor,
        System.Drawing.Bitmap? logoImage,
        double logoSizePercentage,
        double logoPaddingPixels,
        string? logoSvgContent,
        double qrPaddingModules,
        QrFramePreset framePreset,
        string? frameText)
    {
        return new QrRenderSettingsSnapshot(
            errorCorrectionLevel,
            foregroundColor,
            backgroundColor,
            logoImage is null ? null : new System.Drawing.Bitmap(logoImage),
            logoSizePercentage,
            logoPaddingPixels,
            logoSvgContent,
            qrPaddingModules,
            framePreset,
            frameText);
    }

    public void Dispose()
    {
        LogoImage?.Dispose();
    }
}
