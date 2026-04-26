namespace Simple_QR_Code_Maker.Models;

public readonly record struct QrFramePresetOption(
    QrFramePreset Preset,
    string ShortDescription,
    string Description,
    string DefaultText);
