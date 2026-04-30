using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Simple_QR_Code_Maker.Models;

public partial class FolderFileItem : ObservableObject
{
    public StorageFile File { get; }

    public string FileName => File.Name;

    [ObservableProperty]
    public partial BitmapImage? Thumbnail { get; set; }

    public FolderFileItem(StorageFile file)
    {
        File = file;
    }

    public async Task LoadThumbnailAsync()
    {
        try
        {
            StorageItemThumbnail stream = await File.GetThumbnailAsync(ThumbnailMode.SingleItem, 56);
            BitmapImage image = new();
            await image.SetSourceAsync(stream);
            Thumbnail = image;
        }
        catch
        {
            // Thumbnail not critical; leave null and the Border background shows instead.
        }
    }
}
