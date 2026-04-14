using Windows.UI;

namespace Simple_QR_Code_Maker.Models;

public sealed class ColorPickerListItem
{
    public ColorPickerListItem(Color color, string label)
    {
        Color = color;
        Label = label;
    }

    public Color Color { get; }

    public string Label { get; }
}
