using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Simple_QR_Code_Maker.Models;

public partial class EmojiLogoOption : ObservableObject
{
    public string Emoji { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial BitmapImage? PreviewImage { get; set; }

    public EmojiLogoOption(string emoji, string name)
    {
        Emoji = emoji;
        Name = name;
    }
}
