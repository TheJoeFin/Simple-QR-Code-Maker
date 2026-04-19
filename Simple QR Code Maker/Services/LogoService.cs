using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Simple_QR_Code_Maker.Services;

public class LogoService : ILogoService
{
    public IReadOnlyList<string> SupportedLogoFileTypes { get; } =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".svg",
    ];

    public async Task<LogoImageResult> CreateEmojiLogoAsync(string emoji, EmojiLogoStyle style, Windows.UI.Color foregroundColor)
    {
        using EmojiLogoAsset emojiAsset = await EmojiLogoHelper.CreateEmojiLogoAssetAsync(
            emoji,
            style,
            foregroundColor.ToSystemDrawingColor());

        return new LogoImageResult
        {
            LogoImage = new System.Drawing.Bitmap(emojiAsset.PreviewBitmap),
            SvgContent = emojiAsset.SvgContent,
        };
    }

    public async Task<BitmapImage?> CreateBitmapImageAsync(System.Drawing.Bitmap? bitmap)
    {
        if (bitmap is null)
            return null;

        using MemoryStream ms = new();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;

        BitmapImage bitmapImage = new();
        using InMemoryRandomAccessStream randomAccessStream = new();
        await randomAccessStream.WriteAsync(ms.ToArray().AsBuffer());
        randomAccessStream.Seek(0);
        await bitmapImage.SetSourceAsync(randomAccessStream);
        return bitmapImage;
    }

    public async Task<BitmapImage> RenderEmojiPreviewAsync(string emoji, EmojiLogoStyle style, Windows.UI.Color foregroundColor, int pixelSize = 96)
    {
        using System.Drawing.Bitmap bitmap = await EmojiLogoHelper.RenderEmojiToBitmapAsync(
            emoji,
            style,
            foregroundColor.ToSystemDrawingColor(),
            pixelSize);

        return (await CreateBitmapImageAsync(bitmap))!;
    }

    public async Task<LogoImageResult> LoadFromStorageFileAsync(StorageFile file)
    {
        if (file.FileType.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            string svgContent = await FileIO.ReadTextAsync(file);
            return new LogoImageResult
            {
                SvgContent = svgContent,
                LogoImage = BarcodeHelpers.RasterizeSvgToBitmap(svgContent, 512, 512),
                LogoPath = string.IsNullOrWhiteSpace(file.Path) ? null : file.Path,
            };
        }

        using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
        return await LoadRasterFromStreamAsync(stream, string.IsNullOrWhiteSpace(file.Path) ? null : file.Path);
    }

    public Task<LogoImageResult> LoadRasterFromStreamAsync(IRandomAccessStreamWithContentType stream, string? logoPath)
    {
        using System.Drawing.Bitmap temporaryBitmap = new(stream.AsStreamForRead());

        LogoImageResult result = new()
        {
            LogoImage = new System.Drawing.Bitmap(temporaryBitmap),
            LogoPath = logoPath,
        };

        return Task.FromResult(result);
    }

    public async Task<string?> SaveLogoImageToDiskAsync(System.Drawing.Bitmap? logoImage, string? logoSvgContent)
    {
        if (logoImage is null)
            return null;

        StorageFolder logoFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("LogoImages", CreationCollisionOption.OpenIfExists);

        if (logoSvgContent is not null)
        {
            string svgFileName = $"logo_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.svg";
            StorageFile svgFile = await logoFolder.CreateFileAsync(svgFileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(svgFile, logoSvgContent);
            return svgFile.Path;
        }

        string fileName = $"logo_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.png";
        StorageFile logoFile = await logoFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

        using IRandomAccessStream stream = await logoFile.OpenAsync(FileAccessMode.ReadWrite);
        using IOutputStream outputStream = stream.GetOutputStreamAt(0);
        using DataWriter dataWriter = new(outputStream);
        using MemoryStream ms = new();
        logoImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        dataWriter.WriteBytes(ms.ToArray());
        await dataWriter.StoreAsync();

        return logoFile.Path;
    }

    public bool IsSupportedLogoFile(StorageFile file)
    {
        return SupportedLogoFileTypes.Contains(file.FileType, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> ClipboardContainsSupportedLogoFileAsync(DataPackageView clipboardData)
    {
        if (!clipboardData.Contains(StandardDataFormats.StorageItems))
            return false;

        IReadOnlyList<IStorageItem> clipboardItems = await clipboardData.GetStorageItemsAsync();
        return clipboardItems.OfType<StorageFile>().Any(IsSupportedLogoFile);
    }
}
