using Microsoft.UI.Xaml.Media.Imaging;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Extensions;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using SkiaSharp;
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
            LogoImage = emojiAsset.PreviewBitmap.Copy(),
            SvgContent = emojiAsset.SvgContent,
        };
    }

    public async Task<BitmapImage?> CreateBitmapImageAsync(SKBitmap? bitmap)
    {
        if (bitmap is null)
            return null;

        byte[] png = SkiaImaging.EncodePng(bitmap);
        return await SkiaImaging.ToBitmapImageAsync(png);
    }

    public async Task<BitmapImage> RenderEmojiPreviewAsync(string emoji, EmojiLogoStyle style, Windows.UI.Color foregroundColor, int pixelSize = 96)
    {
        using SKBitmap bitmap = await EmojiLogoHelper.RenderEmojiToBitmapAsync(
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
        SKBitmap bitmap = SKBitmap.Decode(stream.AsStreamForRead())
            ?? throw new InvalidOperationException("The selected image could not be decoded.");

        LogoImageResult result = new()
        {
            LogoImage = bitmap,
            LogoPath = logoPath,
        };

        return Task.FromResult(result);
    }

    public async Task<string?> SaveLogoImageToDiskAsync(SKBitmap? logoImage, string? logoSvgContent)
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

        byte[] png = SkiaImaging.EncodePng(logoImage);
        await FileIO.WriteBytesAsync(logoFile, png);

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
