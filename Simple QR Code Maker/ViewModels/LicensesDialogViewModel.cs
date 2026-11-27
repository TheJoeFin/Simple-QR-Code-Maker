using CommunityToolkit.Mvvm.ComponentModel;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using Windows.ApplicationModel;

using Simple_QR_Code_Maker.Helpers;

namespace Simple_QR_Code_Maker.ViewModels;

public sealed partial class LicensesDialogViewModel : ObservableRecipient
{
    public ObservableCollection<LibraryInfo> Libraries { get; } = [];

    [ObservableProperty]
    public partial LibraryInfo? SelectedLibrary { get; set; }

    public LicensesDialogViewModel()
    {
        string apacheText = ReadApacheLicense();

        foreach (LibraryInfo lib in BuildLibraries(apacheText))
            Libraries.Add(lib);

        Libraries.Add(new LibraryInfo
        {
            Name = "SuperTinyIcons",
            LicenseType = "Licenses_Mit".GetLocalized(),
            GitHubUrl = "https://github.com/edent/SuperTinyIcons",
            LicenseText = File.ReadAllText(Path.Combine(Package.Current.InstalledLocation.Path, "Licenses", "SuperTinyIcons.txt")),
        });

        SelectedLibrary = Libraries[0];
    }

    private static string ReadApacheLicense()
    {
        try
        {
            string path = Path.Combine(Package.Current.InstalledLocation.Path, "Licenses", "Apache-2.0.txt");
            return File.ReadAllText(path);
        }
        catch
        {
            return "Licenses_Unavailable".GetLocalized();
        }
    }

    private static IEnumerable<LibraryInfo> BuildLibraries(string apacheText)
    {
        yield return new() { Name = "CommunityToolkit.Mvvm", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/CommunityToolkit/dotnet", LicenseText = MitText };
        yield return new() { Name = "CommunityToolkit WinUI", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/CommunityToolkit/Windows", LicenseText = MitText };
        yield return new() { Name = "Humanizer", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/Humanizr/Humanizer", LicenseText = MitText };
        yield return new() { Name = "Magick.NET", LicenseType = "Licenses_Apache20".GetLocalized(), GitHubUrl = "https://github.com/dlemstra/Magick.NET", LicenseText = apacheText };
        yield return new() { Name = "Microsoft.Extensions.Hosting", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/dotnet/runtime", LicenseText = MitText };
        yield return new() { Name = "Microsoft.Graphics.Win2D", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/microsoft/Win2D", LicenseText = MitText };
        yield return new() { Name = "PdfPig", LicenseType = "Licenses_Apache20".GetLocalized(), GitHubUrl = "https://github.com/UglyToad/PdfPig", LicenseText = apacheText };
        yield return new() { Name = "Windows App SDK", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/microsoft/WindowsAppSDK", LicenseText = MitText };
        yield return new() { Name = "WinUI 3", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/Microsoft/microsoft-ui-xaml", LicenseText = MitText };
        yield return new() { Name = "WinUI.TableView", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/w-ahmad/WinUI.TableView", LicenseText = MitText };
        yield return new() { Name = "WinUIEx", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/dotMorten/WinUIEx", LicenseText = MitText };
        yield return new() { Name = "XAML Behaviors", LicenseType = "Licenses_Mit".GetLocalized(), GitHubUrl = "https://github.com/microsoft/XamlBehaviors", LicenseText = MitText };
        yield return new() { Name = "ZXing.NET", LicenseType = "Licenses_Apache20".GetLocalized(), GitHubUrl = "https://github.com/micjahn/ZXing.Net", LicenseText = apacheText };
    }

    private const string MitLicense =
        """
        MIT License

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.

        """;

    private static string MitText => $"{MitLicense.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{"Licenses_MitCopyrightNote".GetLocalized()}";
}