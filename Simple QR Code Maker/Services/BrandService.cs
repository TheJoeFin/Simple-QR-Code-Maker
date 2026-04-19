using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Simple_QR_Code_Maker.Services;

public class BrandService : IBrandService
{
    [RequiresUnreferencedCode("Calls BrandStorageHelper.LoadBrandsAsync")]
    public async Task LoadAsync(ObservableCollection<BrandItem> brandItems)
    {
        ObservableCollection<BrandItem> loadedBrands = await BrandStorageHelper.LoadBrandsAsync();

        brandItems.Clear();
        foreach (BrandItem item in loadedBrands)
        {
            brandItems.Add(item);
        }
    }

    [RequiresUnreferencedCode("Calls BrandStorageHelper.SaveBrandsAsync")]
    public async Task AddOrReplaceAndSaveAsync(ObservableCollection<BrandItem> brandItems, BrandItem brand)
    {
        brandItems.Remove(brand);
        brandItems.Insert(0, brand);
        await BrandStorageHelper.SaveBrandsAsync(brandItems);
    }

    [RequiresUnreferencedCode("Calls BrandStorageHelper.SaveBrandsAsync")]
    public async Task DeleteAndSaveAsync(ObservableCollection<BrandItem> brandItems, BrandItem brand)
    {
        brandItems.Remove(brand);
        await BrandStorageHelper.SaveBrandsAsync(brandItems);
    }

    [RequiresUnreferencedCode("Calls BrandStorageHelper.SaveBrandsAsync")]
    public async Task SetDefaultAndSaveAsync(ObservableCollection<BrandItem> brandItems, BrandItem brand)
    {
        BrandItem? previousDefault = brandItems.FirstOrDefault(item => item.IsDefault);
        bool isAlreadyDefault = brand.IsDefault;

        foreach (BrandItem item in brandItems)
            item.IsDefault = !isAlreadyDefault && item.Equals(brand);

        if (previousDefault is not null && !previousDefault.Equals(brand))
            RefreshBrandItemInList(brandItems, previousDefault);
        RefreshBrandItemInList(brandItems, brand);

        await BrandStorageHelper.SaveBrandsAsync(brandItems);
    }

    [RequiresUnreferencedCode("Calls BrandStorageHelper.SaveBrandsAsync")]
    public async Task<bool> ReplaceAndSaveAsync(ObservableCollection<BrandItem> brandItems, BrandItem existingBrand, BrandItem replacementBrand)
    {
        int index = brandItems.IndexOf(existingBrand);
        if (index < 0)
            return false;

        brandItems.RemoveAt(index);
        brandItems.Insert(index, replacementBrand);
        await BrandStorageHelper.SaveBrandsAsync(brandItems);
        return true;
    }

    private static void RefreshBrandItemInList(ObservableCollection<BrandItem> brandItems, BrandItem brand)
    {
        int index = brandItems.IndexOf(brand);
        if (index < 0)
            return;

        brandItems.RemoveAt(index);
        brandItems.Insert(index, brand);
    }
}
