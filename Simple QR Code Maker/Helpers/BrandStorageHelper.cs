using System.Collections.ObjectModel;
using Simple_QR_Code_Maker.Contracts.Services;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Helpers;

public static class BrandStorageHelper
{
    public static Task<ObservableCollection<BrandItem>> LoadBrandsAsync() =>
        App.GetService<IBrandStore>().LoadAsync();

    public static Task SaveBrandsAsync(ObservableCollection<BrandItem> brandItems) =>
        App.GetService<IBrandStore>().SaveAsync(brandItems);
}