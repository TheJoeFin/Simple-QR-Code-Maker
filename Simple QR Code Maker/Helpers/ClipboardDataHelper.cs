using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Simple_QR_Code_Maker.Helpers;

internal static class ClipboardDataHelper
{
    private const string PngMimeType = "image/png";
    private const string SvgMimeType = "image/svg+xml";

    public static DataPackage CreateFilePackage(IReadOnlyList<StorageFile> files, StorageFile? bitmapFile = null)
    {
        DataPackage package = new();
        PopulateFilePackage(package, files, bitmapFile);
        return package;
    }

    public static void PopulateFilePackage(DataPackage package, IReadOnlyList<StorageFile> files, StorageFile? bitmapFile = null)
    {
        IStorageItem[] items = [.. files];
        if (items.Length == 0)
            throw new ArgumentException("At least one clipboard file is required.", nameof(files));

        package.SetStorageItems(items);

        if (bitmapFile is not null)
        {
            package.SetBitmap(RandomAccessStreamReference.CreateFromFile(bitmapFile));
            package.SetData(PngMimeType, RandomAccessStreamReference.CreateFromFile(bitmapFile));
        }

        if (files.Count == 1 && string.Equals(files[0].FileType, ".svg", StringComparison.OrdinalIgnoreCase))
            package.SetData(SvgMimeType, RandomAccessStreamReference.CreateFromFile(files[0]));
    }

    public static void SetFiles(IReadOnlyList<StorageFile> files, StorageFile? bitmapFile = null)
    {
        DataPackage package = CreateFilePackage(files, bitmapFile);
        Clipboard.SetContentWithOptions(package, new ClipboardContentOptions { IsAllowedInHistory = true });
        Clipboard.Flush();
    }
}
