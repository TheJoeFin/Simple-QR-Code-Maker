using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Services;

public sealed class BrandStore(IJsonFileStore files) : IBrandStore
{
    public async Task<ObservableCollection<BrandItem>> LoadAsync()
    {
        try
        {
            string? json = await files.ReadAsync("Brands.json");
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("Brands.json is missing or empty - starting with empty brands.");
                return [];
            }

            ObservableCollection<BrandItem> brands = JsonSerializer.Deserialize(json, BrandJsonContext.Default.ObservableCollectionBrandItem) ?? [];
            Debug.WriteLine($"Loaded {brands.Count} brands from Brands.json");
            return brands;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading Brands.json: {ex.GetType().Name}: {ex.Message}");
            return [];
        }
    }

    public async Task SaveAsync(ObservableCollection<BrandItem> items)
    {
        string json = JsonSerializer.Serialize(items, BrandJsonContext.Default.ObservableCollectionBrandItem);
        try
        {
            await files.WriteAsync("Brands.json", json).ConfigureAwait(false);
            Debug.WriteLine($"Saved {items.Count} brands to Brands.json ({json.Length} bytes)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save brands: {ex.Message}");
            throw;
        }
    }
}
