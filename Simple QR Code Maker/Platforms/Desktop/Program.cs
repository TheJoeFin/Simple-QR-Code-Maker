using System.Threading.Tasks;
using Uno.UI.Hosting;

namespace Simple_QR_Code_Maker.Platforms.Desktop;

internal class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        UnoPlatformHost host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWin32()
            .UseX11()
            .UseMacOS()
            .Build();

        await host.RunAsync();
    }
}
