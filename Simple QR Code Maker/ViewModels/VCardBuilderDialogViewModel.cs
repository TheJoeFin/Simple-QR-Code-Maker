using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.ViewModels;

[ObservableObject]
public sealed partial class VCardBuilderDialogViewModel
{
    private readonly HashSet<VCardOptionalFieldKind> _visibleOptionalFields = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial string FormattedName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial string FirstName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial string LastName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial string MiddleName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial string Prefix { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial string Suffix { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string Organization { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string JobTitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string MobilePhone { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string WorkPhone { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string HomePhone { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string Url { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string Nickname { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string Birthday { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string StreetAddress { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string Locality { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string Region { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string PostalCode { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string Country { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewVCard))]
    public partial string Note { get; set; } = string.Empty;

    public string PreviewVCard => VCardBuilderHelper.Serialize(CreateCurrentState());

    public bool CanApply => !string.IsNullOrWhiteSpace(VCardBuilderHelper.GetDisplayName(CreateCurrentState()));

    public Visibility WorkPhoneVisibility => GetVisibility(VCardOptionalFieldKind.WorkPhone);

    public Visibility HomePhoneVisibility => GetVisibility(VCardOptionalFieldKind.HomePhone);

    public Visibility NicknameVisibility => GetVisibility(VCardOptionalFieldKind.Nickname);

    public Visibility BirthdayVisibility => GetVisibility(VCardOptionalFieldKind.Birthday);

    public Visibility AddressVisibility => GetVisibility(VCardOptionalFieldKind.Address);

    public Visibility NoteVisibility => GetVisibility(VCardOptionalFieldKind.Note);

    public void Initialize(string? initialText)
    {
        if (!VCardBuilderHelper.TryParse(initialText, out VCardBuilderState parsedState))
            return;

        ApplyState(parsedState);
    }

    public bool TryCreateResultText(out string resultText)
    {
        VCardBuilderState state = CreateCurrentState();
        resultText = VCardBuilderHelper.Serialize(state);
        return !string.IsNullOrWhiteSpace(resultText);
    }

    [RelayCommand]
    private void AddOptionalField(string? fieldName)
    {
        if (TryGetOptionalField(fieldName, out VCardOptionalFieldKind fieldKind))
            SetOptionalFieldVisible(fieldKind, true);
    }

    [RelayCommand]
    private void RemoveOptionalField(string? fieldName)
    {
        if (TryGetOptionalField(fieldName, out VCardOptionalFieldKind fieldKind))
            SetOptionalFieldVisible(fieldKind, false);
    }

    private void ApplyState(VCardBuilderState state)
    {
        FormattedName = state.FormattedName;
        FirstName = state.FirstName;
        LastName = state.LastName;
        MiddleName = state.MiddleName;
        Prefix = state.Prefix;
        Suffix = state.Suffix;
        Organization = state.Organization;
        JobTitle = state.Title;
        MobilePhone = state.MobilePhone;
        WorkPhone = state.WorkPhone;
        HomePhone = state.HomePhone;
        Email = state.Email;
        Url = state.Url;
        Nickname = state.Nickname;
        Birthday = state.Birthday;
        StreetAddress = state.StreetAddress;
        Locality = state.Locality;
        Region = state.Region;
        PostalCode = state.PostalCode;
        Country = state.Country;
        Note = state.Note;

        EnsureVisibleWhenFilled(VCardOptionalFieldKind.WorkPhone, WorkPhone);
        EnsureVisibleWhenFilled(VCardOptionalFieldKind.HomePhone, HomePhone);
        EnsureVisibleWhenFilled(VCardOptionalFieldKind.Nickname, Nickname);
        EnsureVisibleWhenFilled(VCardOptionalFieldKind.Birthday, Birthday);
        EnsureVisibleWhenFilled(VCardOptionalFieldKind.Note, Note);

        if (!string.IsNullOrWhiteSpace(StreetAddress)
            || !string.IsNullOrWhiteSpace(Locality)
            || !string.IsNullOrWhiteSpace(Region)
            || !string.IsNullOrWhiteSpace(PostalCode)
            || !string.IsNullOrWhiteSpace(Country))
        {
            SetOptionalFieldVisible(VCardOptionalFieldKind.Address, true);
        }

        RefreshState();
    }

    private void EnsureVisibleWhenFilled(VCardOptionalFieldKind fieldKind, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            SetOptionalFieldVisible(fieldKind, true);
    }

    private VCardBuilderState CreateCurrentState()
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
            Title = JobTitle,
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

    private void SetOptionalFieldVisible(VCardOptionalFieldKind fieldKind, bool isVisible)
    {
        if (isVisible)
            _visibleOptionalFields.Add(fieldKind);
        else
            _visibleOptionalFields.Remove(fieldKind);

        switch (fieldKind)
        {
            case VCardOptionalFieldKind.WorkPhone:
                OnPropertyChanged(nameof(WorkPhoneVisibility));
                break;
            case VCardOptionalFieldKind.HomePhone:
                OnPropertyChanged(nameof(HomePhoneVisibility));
                break;
            case VCardOptionalFieldKind.Nickname:
                OnPropertyChanged(nameof(NicknameVisibility));
                break;
            case VCardOptionalFieldKind.Birthday:
                OnPropertyChanged(nameof(BirthdayVisibility));
                break;
            case VCardOptionalFieldKind.Address:
                OnPropertyChanged(nameof(AddressVisibility));
                break;
            case VCardOptionalFieldKind.Note:
                OnPropertyChanged(nameof(NoteVisibility));
                break;
        }

        RefreshState();
    }

    private Visibility GetVisibility(VCardOptionalFieldKind fieldKind)
    {
        return _visibleOptionalFields.Contains(fieldKind)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool TryGetOptionalField(string? fieldName, out VCardOptionalFieldKind fieldKind)
    {
        fieldKind = default;
        return fieldName is not null
            && Enum.TryParse(fieldName, ignoreCase: true, out fieldKind);
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(PreviewVCard));
        OnPropertyChanged(nameof(CanApply));
    }

    partial void OnFormattedNameChanged(string value) => RefreshState();

    partial void OnFirstNameChanged(string value) => RefreshState();

    partial void OnLastNameChanged(string value) => RefreshState();

    partial void OnMiddleNameChanged(string value) => RefreshState();

    partial void OnPrefixChanged(string value) => RefreshState();

    partial void OnSuffixChanged(string value) => RefreshState();

    partial void OnOrganizationChanged(string value) => RefreshState();

    partial void OnJobTitleChanged(string value) => RefreshState();

    partial void OnMobilePhoneChanged(string value) => RefreshState();

    partial void OnWorkPhoneChanged(string value) => RefreshState();

    partial void OnHomePhoneChanged(string value) => RefreshState();

    partial void OnEmailChanged(string value) => RefreshState();

    partial void OnUrlChanged(string value) => RefreshState();

    partial void OnNicknameChanged(string value) => RefreshState();

    partial void OnBirthdayChanged(string value) => RefreshState();

    partial void OnStreetAddressChanged(string value) => RefreshState();

    partial void OnLocalityChanged(string value) => RefreshState();

    partial void OnRegionChanged(string value) => RefreshState();

    partial void OnPostalCodeChanged(string value) => RefreshState();

    partial void OnCountryChanged(string value) => RefreshState();

    partial void OnNoteChanged(string value) => RefreshState();
}
