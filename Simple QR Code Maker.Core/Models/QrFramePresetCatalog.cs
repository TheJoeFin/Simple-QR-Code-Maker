namespace Simple_QR_Code_Maker.Models;

public static class QrFramePresetCatalog
{
    public static IReadOnlyList<QrFramePresetOption> All { get; } =
    [
        new(QrFramePreset.None, "Frame", "No frame", string.Empty),
        new(QrFramePreset.BottomLabel, "Label", "Bottom label", "Scan Me"),
        new(QrFramePreset.RoundedFrame, "Rounded", "Rounded frame", "Scan Here"),
        new(QrFramePreset.CornerCallout, "Callout", "Corner callout", "Scan QR Code"),
        new(QrFramePreset.SpeechBubble, "Bubble", "Speech bubble", "Scan Me"),
        new(QrFramePreset.Ticket, "Ticket", "Event ticket", "Get tickets"),
        new(QrFramePreset.Sticker, "Sticker", "Pop sticker", "Open me"),
        new(QrFramePreset.ScallopedBadge, "Badge", "Scalloped badge", "Scan Me"),
        new(QrFramePreset.Polaroid, "Photo", "Caption card", "Make a memory"),
        new(QrFramePreset.Burst, "Burst", "Comic burst", "Try me!"),
    ];

    public static QrFramePresetOption GetOption(QrFramePreset preset)
    {
        return All.FirstOrDefault(option => option.Preset == preset, All[0]);
    }

    public static string? ResolveText(QrFramePreset preset, string? frameText)
    {
        if (preset == QrFramePreset.None)
            return null;

        string trimmedText = frameText?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmedText)
            ? GetOption(preset).DefaultText
            : trimmedText;
    }
}
