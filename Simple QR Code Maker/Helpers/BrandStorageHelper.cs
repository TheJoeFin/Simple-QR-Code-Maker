using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Windows.Storage;

namespace Simple_QR_Code_Maker.Helpers;

public static class BrandStorageHelper
{
    private const string BrandsFileName = "Brands.json";

    public static async Task<ObservableCollection<BrandItem>> LoadBrandsAsync()
    {
        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile brandsFile = await localFolder.GetFileAsync(BrandsFileName);
            string json = await FileIO.ReadTextAsync(brandsFile);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine($"{BrandsFileName} exists but is empty");
                return [];
            }

            ObservableCollection<BrandItem>? brands = JsonSerializer.Deserialize(json, BrandJsonContext.Default.ObservableCollectionBrandItem);

            Debug.WriteLine($"Loaded {brands?.Count ?? 0} brands from {BrandsFileName}");
            return brands ?? [];
        }
        catch (FileNotFoundException)
        {
            Debug.WriteLine($"{BrandsFileName} not found - starting with empty brands");
            return [];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading {BrandsFileName}: {ex.GetType().Name}: {ex.Message}");
            return [];
        }
    }

    public static async Task SaveBrandsAsync(ObservableCollection<BrandItem> brandItems)
    {
        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;

            string json = JsonSerializer.Serialize(brandItems, BrandJsonContext.Default.ObservableCollectionBrandItem);

            string tempFileName = BrandsFileName + ".tmp";
            StorageFile tempFile = await localFolder.CreateFileAsync(
                tempFileName,
                CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(tempFile, json);

            await tempFile.RenameAsync(BrandsFileName, NameCollisionOption.ReplaceExisting);

            Debug.WriteLine($"Saved {brandItems.Count} brands to {BrandsFileName} ({json.Length} bytes)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save brands: {ex.Message}");
            throw;
        }
    }
}
