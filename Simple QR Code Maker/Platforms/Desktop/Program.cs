using Uno.UI.Hosting;

namespace Simple_QR_Code_Maker.Platforms.Desktop;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        UnoPlatformHost host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWin32()
            .UseX11()
            .UseMacOS()
            .Build();

        host.Run();
    }
}
