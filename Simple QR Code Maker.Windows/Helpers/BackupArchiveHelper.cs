using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Simple_QR_Code_Maker.Helpers;

internal static class BackupArchiveHelper
{
    internal static async Task<string?> AddImageToArchiveAsync(
        ZipArchive archive,
        string? originalPath,
        Dictionary<string, string> imagePathMap,
        string category,
        string nameHint,
        DateTime timestamp)
    {
        if (string.IsNullOrEmpty(originalPath))
            return null;
        if (imagePathMap.TryGetValue(originalPath, out string? existingZipPath))
            return existingZipPath;
        if (!File.Exists(originalPath))
            return null;

        string zipPath = CreateArchiveImagePath(category, nameHint, originalPath, timestamp, imagePathMap.Count + 1);
        ZipArchiveEntry entry = archive.CreateEntry(zipPath, CompressionLevel.NoCompression);
        using (Stream output = entry.Open())
        using (FileStream input = File.OpenRead(originalPath))
            await input.CopyToAsync(output);

        imagePathMap[originalPath] = zipPath;
        return zipPath;
    }

    internal static async Task<Dictionary<string, string>> ExtractImagesAsync(ZipArchive archive, string localFolderPath)
    {
        Dictionary<string, string> importedPaths = [];
        string directory = Path.Combine(localFolderPath, "ImportedImages", $"import_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith("images/", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Name))
                continue;

            string localPath = CreateUniqueImportedImagePath(directory, entry.Name);
            using (Stream input = entry.Open())
            using (FileStream output = new(localPath, FileMode.CreateNew, FileAccess.Write))
                await input.CopyToAsync(output);

            importedPaths[entry.FullName] = localPath;
        }

        return importedPaths;
    }

    internal static string? ResolveImportedImagePath(string? logoPath, IReadOnlyDictionary<string, string> importedPaths)
    {
        if (string.IsNullOrEmpty(logoPath))
            return null;
        return logoPath.StartsWith("images/", StringComparison.Ordinal)
            ? importedPaths.GetValueOrDefault(logoPath)
            : logoPath;
    }

    internal static ObservableCollection<HistoryItem> MergeHistory(
        IEnumerable<HistoryItem> existing, IReadOnlyList<HistoryItem> imported)
    {
        ObservableCollection<HistoryItem> merged = new(existing);
        for (int index = imported.Count - 1; index >= 0; index--)
        {
            merged.Remove(imported[index]);
            merged.Insert(0, imported[index]);
        }

        return merged;
    }

    internal static ObservableCollection<BrandItem> MergeBrands(
        IEnumerable<BrandItem> existing, IReadOnlyList<BrandItem> imported)
    {
        ObservableCollection<BrandItem> merged = new(existing);
        BrandItem? preferredDefault = null;
        for (int index = imported.Count - 1; index >= 0; index--)
        {
            BrandItem brand = imported[index];
            if (brand.IsDefault)
                preferredDefault = brand;
            merged.Remove(brand);
            merged.Insert(0, brand);
        }

        preferredDefault ??= merged.FirstOrDefault(brand => brand.IsDefault);
        bool assigned = false;
        foreach (BrandItem brand in merged)
        {
            brand.IsDefault = !assigned && preferredDefault is not null && brand.Equals(preferredDefault);
            assigned |= brand.IsDefault;
        }

        return merged;
    }

    internal static string CreateArchiveImagePath(
        string category, string nameHint, string originalPath, DateTime timestamp, int imageNumber, TimeProvider? clock = null)
    {
        string extension = Path.GetExtension(originalPath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".img";

        string categorySegment = SanitizeFileNameSegment(category, 16);
        string timestampSegment = (timestamp == default ? (clock ?? TimeProvider.System).GetLocalNow().DateTime : timestamp)
            .ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string hintSegment = SanitizeFileNameSegment(nameHint, 36);
        string originalSegment = SanitizeFileNameSegment(Path.GetFileNameWithoutExtension(originalPath), 24);
        List<string> parts = [string.IsNullOrWhiteSpace(categorySegment) ? "image" : categorySegment, timestampSegment];
        if (!string.IsNullOrWhiteSpace(hintSegment))
            parts.Add(hintSegment);
        if (!string.IsNullOrWhiteSpace(originalSegment) && !string.Equals(originalSegment, hintSegment, StringComparison.OrdinalIgnoreCase))
            parts.Add(originalSegment);
        parts.Add($"logo_{imageNumber:D3}");
        return $"images/{string.Join("_", parts)}{extension}";
    }

    private static string CreateUniqueImportedImagePath(string directory, string entryName)
    {
        string name = SanitizeFileNameSegment(Path.GetFileNameWithoutExtension(entryName), 96);
        if (string.IsNullOrWhiteSpace(name))
            name = "imported_logo";
        string extension = Path.GetExtension(entryName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".img";
        string path = Path.Combine(directory, $"{name}{extension}");
        int duplicateIndex = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{name}_{duplicateIndex:D2}{extension}");
            duplicateIndex++;
        }

        return path;
    }

    internal static string SanitizeFileNameSegment(string? value, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        StringBuilder builder = new(maxLength);
        bool lastWasSeparator = false;
        foreach (char character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (builder.Length >= maxLength)
                    break;
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
            }
            else if (builder.Length > 0 && !lastWasSeparator && builder.Length < maxLength)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim('_');
    }
}
