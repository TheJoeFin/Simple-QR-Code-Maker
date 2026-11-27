using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_QR_Code_Maker.Contracts.Services;

public interface IBrandService
{
    Task LoadAsync(ObservableCollection<BrandItem> brandItems);

    Task AddOrReplaceAndSaveAsync(ObservableCollection<BrandItem> brandItems, BrandItem brand);

    Task DeleteAndSaveAsync(ObservableCollection<BrandItem> brandItems, BrandItem brand);

    Task SetDefaultAndSaveAsync(ObservableCollection<BrandItem> brandItems, BrandItem brand);

    Task<bool> ReplaceAndSaveAsync(ObservableCollection<BrandItem> brandItems, BrandItem existingBrand, BrandItem replacementBrand);
}
