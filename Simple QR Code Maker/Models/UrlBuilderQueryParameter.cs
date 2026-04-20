using CommunityToolkit.Mvvm.ComponentModel;

namespace Simple_QR_Code_Maker.Models;

[ObservableObject]
public sealed partial class UrlBuilderQueryParameter
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    public UrlBuilderQueryParameter()
    {
    }

    public UrlBuilderQueryParameter(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public UrlBuilderQueryParameter Clone() => new(Name, Value);
}
