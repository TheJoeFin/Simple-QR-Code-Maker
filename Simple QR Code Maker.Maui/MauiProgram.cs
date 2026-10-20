using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Simple_QR_Code_Maker.Core.Services;
using Simple_QR_Code_Maker.Maui.Services;
using Simple_QR_Code_Maker.Maui.Storage;
using Simple_QR_Code_Maker.Maui.ViewModels;
using Simple_QR_Code_Maker.Rendering;

namespace Simple_QR_Code_Maker.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<QrMatrixEncoder>();
        builder.Services.AddSingleton<QrRenderer>();
        builder.Services.AddQrStorage();
        builder.Services.AddSingleton<IQrOutputPreparationService, QrOutputPreparationService>();
        builder.Services.AddSingleton<CreatorViewModel>();
        builder.Services.AddSingleton<HistoryViewModel>();
        builder.Services.AddSingleton<DesignsViewModel>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<HistoryPage>();
        builder.Services.AddSingleton<DesignsPage>();
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
