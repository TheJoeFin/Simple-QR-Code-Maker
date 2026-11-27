namespace Simple_QR_Code_Maker.Models;

public sealed class VCardBuilderState
{
    public string FormattedName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string MiddleName { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public string Suffix { get; set; } = string.Empty;

    public string Organization { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string MobilePhone { get; set; } = string.Empty;

    public string WorkPhone { get; set; } = string.Empty;

    public string HomePhone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public string Birthday { get; set; } = string.Empty;

    public string StreetAddress { get; set; } = string.Empty;

    public string Locality { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public VCardBuilderState Clone()
    {
        return new VCardBuilderState
        {
            FormattedName = FormattedName,
            FirstName = FirstName,
            LastName = LastName,
            MiddleName = MiddleName,
            Prefix = Prefix,
            Suffix = Suffix,
            Organization = Organization,
            Title = Title,
            MobilePhone = MobilePhone,
            WorkPhone = WorkPhone,
            HomePhone = HomePhone,
            Email = Email,
            Url = Url,
            Nickname = Nickname,
            Birthday = Birthday,
            StreetAddress = StreetAddress,
            Locality = Locality,
            Region = Region,
            PostalCode = PostalCode,
            Country = Country,
            Note = Note,
        };
    }
}
