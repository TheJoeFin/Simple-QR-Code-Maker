namespace Simple_QR_Code_Maker.Models;

public sealed class LibraryInfo
{
    public string Name { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public string GitHubUrl { get; set; } = string.Empty;
    public string LicenseText { get; set; } = string.Empty;
    public Uri GitHubUri => new(GitHubUrl);
}
