using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Simple_QR_Code_Maker.Contracts.Services;

public interface ILogoService
{
    IReadOnlyList<string> SupportedLogoFileTypes { get; }

    Task<LogoImageResult> CreateEmojiLogoAsync(string emoji, EmojiLogoStyle style, Windows.UI.Color foregroundColor);

    Task<BitmapImage?> CreateBitmapImageAsync(System.Drawing.Bitmap? bitmap);

    Task<BitmapImage> RenderEmojiPreviewAsync(string emoji, EmojiLogoStyle style, Windows.UI.Color foregroundColor, int pixelSize = 96);

    Task<LogoImageResult> LoadFromStorageFileAsync(StorageFile file);

    Task<LogoImageResult> LoadRasterFromStreamAsync(IRandomAccessStreamWithContentType stream, string? logoPath);

    Task<string?> SaveLogoImageToDiskAsync(System.Drawing.Bitmap? logoImage, string? logoSvgContent);

    bool IsSupportedLogoFile(StorageFile file);

    Task<bool> ClipboardContainsSupportedLogoFileAsync(DataPackageView clipboardData);
}
