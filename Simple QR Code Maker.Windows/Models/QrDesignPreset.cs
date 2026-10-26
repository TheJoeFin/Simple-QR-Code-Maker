using Windows.UI;

namespace Simple_QR_Code_Maker.Models;

public sealed record QrDesignPreset(
    string Name,
    string Description,
    Color Foreground,
    Color Background,
    QrFramePreset Frame,
    string Caption,
    QrCodeAppearance Appearance)
{
    public QrCodeDesignState ApplyTo(QrCodeDesignState state) => state with
    {
        Foreground = Foreground,
        Background = Background,
        FramePreset = Frame,
        FrameText = Caption,
        FrameTextSource = QrFrameTextSource.Manual,
        Appearance = Appearance,
        QrPaddingModules = 4,
    };
}

public static class QrDesignPresetCatalog
{
    public static IReadOnlyList<QrDesignPreset> All { get; } =
    [
        new("Classic", "Crisp, simple, and ready for anything.", Rgb(0x000000), Rgb(0xFFFFFF),
            QrFramePreset.None, string.Empty, new()),
        new("Bubblegum", "Plum dots in a candy-colored speech bubble.", Rgb(0x4C1D5F), Rgb(0xFFFFFF),
            QrFramePreset.SpeechBubble, "Say hello!", new()
            {
                ModuleStyle = QrModuleStyle.Dots, EyeStyle = QrEyeStyle.Rounded,
                FrameAccent = Rgb(0xE86BA6), FrameGradientEnd = Rgb(0xF6B468), CaptionIcon = QrCaptionIcon.Arrow,
            }),
        new("Gig Ticket", "An admission ticket with a warm sunset border.", Rgb(0x15284B), Rgb(0xFFFFFF),
            QrFramePreset.Ticket, "Get tickets", new()
            {
                EyeStyle = QrEyeStyle.Rounded, FrameAccent = Rgb(0xFFBC42),
                FrameGradientEnd = Rgb(0xFF6B45), CaptionIcon = QrCaptionIcon.Arrow,
            }),
        new("Cafe", "Soft corners and warm paper tones.", Rgb(0x4B3329), Rgb(0xFFF9EC),
            QrFramePreset.Polaroid, "View the menu", new()
            {
                ModuleStyle = QrModuleStyle.Rounded, EyeStyle = QrEyeStyle.Rounded,
                FrameAccent = Rgb(0xD1B089), CaptionIcon = QrCaptionIcon.Link,
            }),
        new("Pop Sticker", "Lime, round eyes, and a bold offset shadow.", Rgb(0x1F2245), Rgb(0xFFFFFF),
            QrFramePreset.Sticker, "Open me", new()
            {
                ModuleStyle = QrModuleStyle.Dots, EyeStyle = QrEyeStyle.Circle,
                FrameAccent = Rgb(0xADF06D), CaptionIcon = QrCaptionIcon.Arrow,
            }),
        new("Festival", "A bright comic burst with a little extra energy.", Rgb(0x242052), Rgb(0xFFFFFF),
            QrFramePreset.Burst, "Join the fun!", new()
            {
                ModuleStyle = QrModuleStyle.Rounded, EyeStyle = QrEyeStyle.Circle,
                FrameAccent = Rgb(0xFFBE3D), FrameGradientEnd = Rgb(0xF27DB5), CaptionIcon = QrCaptionIcon.Arrow,
            }),
        new("Ocean", "A turquoise badge with a fresh blue gradient.", Rgb(0x123D4A), Rgb(0xFFFFFF),
            QrFramePreset.ScallopedBadge, "Dive in", new()
            {
                ModuleStyle = QrModuleStyle.Dots, EyeStyle = QrEyeStyle.Rounded,
                FrameAccent = Rgb(0x49CBBE), FrameGradientEnd = Rgb(0x8EDBF5), CaptionIcon = QrCaptionIcon.Link,
            }),
        new("Postcard", "A calm blue frame for sharing something personal.", Rgb(0x143B6B), Rgb(0xFFFFFF),
            QrFramePreset.RoundedFrame, "Keep in touch", new()
            {
                EyeStyle = QrEyeStyle.Rounded, FrameAccent = Rgb(0xA9C7FF), CaptionIcon = QrCaptionIcon.Contact,
            }),
    ];

    private static Color Rgb(uint value) => Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
}
