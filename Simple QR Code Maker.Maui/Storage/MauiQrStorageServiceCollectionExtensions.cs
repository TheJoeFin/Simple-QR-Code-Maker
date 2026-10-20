using Microsoft.Extensions.DependencyInjection;
using Simple_QR_Code_Maker.Core.Storage;

namespace Simple_QR_Code_Maker.Maui.Storage;

public static class MauiQrStorageServiceCollectionExtensions
{
    public static IServiceCollection AddQrStorage(this IServiceCollection services)
    {
        services.AddSingleton<AtomicJsonFileStore>(_ => new AtomicJsonFileStore(FileSystem.AppDataDirectory));
        services.AddSingleton<QrDraftRepository>();
        services.AddSingleton<QrDesignRepository>();
        services.AddSingleton<QrHistoryRepository>();
        services.AddSingleton<QrLogoAssetRepository>();
        return services;
    }
}
