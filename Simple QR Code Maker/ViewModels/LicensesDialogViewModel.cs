using CommunityToolkit.Mvvm.ComponentModel;
using Simple_QR_Code_Maker.Models;
using System.Collections.ObjectModel;
using System.IO;
using Windows.ApplicationModel;

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
            return "[License text unavailable — see https://www.apache.org/licenses/LICENSE-2.0]";
        }
    }

    private static IEnumerable<LibraryInfo> BuildLibraries(string apacheText)
    {
        yield return new() { Name = "CommunityToolkit.Mvvm",       LicenseType = "MIT License",        GitHubUrl = "https://github.com/CommunityToolkit/dotnet",        LicenseText = MitText };
        yield return new() { Name = "CommunityToolkit WinUI",       LicenseType = "MIT License",        GitHubUrl = "https://github.com/CommunityToolkit/Windows",        LicenseText = MitText };
        yield return new() { Name = "Humanizer",                    LicenseType = "MIT License",        GitHubUrl = "https://github.com/Humanizr/Humanizer",              LicenseText = MitText };
        yield return new() { Name = "Magick.NET",                   LicenseType = "Apache-2.0 License", GitHubUrl = "https://github.com/dlemstra/Magick.NET",             LicenseText = apacheText };
        yield return new() { Name = "Microsoft.Extensions.Hosting", LicenseType = "MIT License",        GitHubUrl = "https://github.com/dotnet/runtime",                  LicenseText = MitText };
        yield return new() { Name = "Microsoft.Graphics.Win2D",     LicenseType = "MIT License",        GitHubUrl = "https://github.com/microsoft/Win2D",                 LicenseText = MitText };
        yield return new() { Name = "PDFsharp",                     LicenseType = "MIT License",        GitHubUrl = "https://github.com/empira/PDFsharp",                 LicenseText = MitText };
        yield return new() { Name = "Windows App SDK",              LicenseType = "MIT License",        GitHubUrl = "https://github.com/microsoft/WindowsAppSDK",         LicenseText = MitText };
        yield return new() { Name = "WinUI 3",                      LicenseType = "MIT License",        GitHubUrl = "https://github.com/Microsoft/microsoft-ui-xaml",     LicenseText = MitText };
        yield return new() { Name = "WinUI.TableView",              LicenseType = "MIT License",        GitHubUrl = "https://github.com/w-ahmad/WinUI.TableView",         LicenseText = MitText };
        yield return new() { Name = "WinUIEx",                      LicenseType = "MIT License",        GitHubUrl = "https://github.com/dotMorten/WinUIEx",               LicenseText = MitText };
        yield return new() { Name = "XAML Behaviors",               LicenseType = "MIT License",        GitHubUrl = "https://github.com/microsoft/XamlBehaviors",         LicenseText = MitText };
        yield return new() { Name = "ZXing.NET",                    LicenseType = "Apache-2.0 License", GitHubUrl = "https://github.com/micjahn/ZXing.Net",               LicenseText = apacheText };
    }

    private const string MitText =
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

        ──────────────────────────────────────────────────────────────────────────
        Note: The specific copyright year(s) and holder(s) for this library are
        listed in its repository on GitHub. Use "View on GitHub" above to see the
        full copyright notice.
        """;
}
