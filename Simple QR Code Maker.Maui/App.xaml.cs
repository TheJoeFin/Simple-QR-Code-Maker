using Microsoft.Extensions.DependencyInjection;

namespace Simple_QR_Code_Maker.Maui;

public partial class App : Application
{
    private readonly IServiceProvider services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        this.services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(services.GetRequiredService<AppShell>());
}