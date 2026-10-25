namespace Simple_QR_Code_Maker.Models;

public struct QrFramePresetOption
{
    public QrFramePreset Preset { get; set; }

    public string ShortDescription { get; set; }

    public string Description { get; set; }

    public string DefaultText { get; set; }

    public QrFramePresetOption(QrFramePreset preset, string shortDescription, string description, string defaultText)
    {
        Preset = preset;
        ShortDescription = shortDescription;
        Description = description;
        DefaultText = defaultText;
    }
}
