using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Controls;

[ObservableObject]
public sealed partial class WifiBuilderDialog : ContentDialog
{
    private bool _isUpdatingAuthenticationSelection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewWifiPayload))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial string Ssid { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewWifiPayload))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyPropertyChangedFor(nameof(PasswordVisibility))]
    [NotifyPropertyChangedFor(nameof(OpenNetworkInfoVisibility))]
    [NotifyPropertyChangedFor(nameof(LegacySecurityWarningVisibility))]
    public partial WifiAuthenticationType AuthenticationType { get; set; } = WifiAuthenticationType.WPA;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewWifiPayload))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewWifiPayload))]
    public partial bool IsHiddenNetwork { get; set; }

    public string PreviewWifiPayload => WifiBuilderHelper.Serialize(CreateCurrentState());

    public bool CanApply => !string.IsNullOrWhiteSpace(Ssid)
        && (AuthenticationType == WifiAuthenticationType.None || !string.IsNullOrWhiteSpace(Password));

    public Visibility PasswordVisibility => AuthenticationType == WifiAuthenticationType.None
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility OpenNetworkInfoVisibility => AuthenticationType == WifiAuthenticationType.None
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LegacySecurityWarningVisibility => AuthenticationType == WifiAuthenticationType.WEP
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string? ResultText { get; private set; }

    public WifiBuilderDialog(string? initialText)
    {
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;

        if (WifiBuilderHelper.TryParse(initialText, out WifiBuilderState parsedState))
            ApplyState(parsedState);
        else
            SyncAuthenticationSelection();

        IsPrimaryButtonEnabled = CanApply;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        SsidTextBox.Focus(FocusState.Programmatic);
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        string serializedText = WifiBuilderHelper.Serialize(CreateCurrentState());
        if (string.IsNullOrWhiteSpace(serializedText))
        {
            args.Cancel = true;
            return;
        }

        ResultText = serializedText;
    }

    private void AuthenticationTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingAuthenticationSelection)
            return;

        if (AuthenticationTypeComboBox.SelectedItem is ComboBoxItem selectedItem
            && selectedItem.Tag is string authenticationTag
            && Enum.TryParse(authenticationTag, ignoreCase: true, out WifiAuthenticationType authenticationType))
        {
            AuthenticationType = authenticationType;
        }
    }

    private void ApplyState(WifiBuilderState state)
    {
        Ssid = state.Ssid;
        AuthenticationType = state.AuthenticationType;
        Password = state.Password;
        IsHiddenNetwork = state.IsHiddenNetwork;
        SyncAuthenticationSelection();
        RefreshState();
    }

    private WifiBuilderState CreateCurrentState()
    {
        return new WifiBuilderState
        {
            Ssid = Ssid,
            AuthenticationType = AuthenticationType,
            Password = Password,
            IsHiddenNetwork = IsHiddenNetwork,
        };
    }

    private void SyncAuthenticationSelection()
    {
        _isUpdatingAuthenticationSelection = true;

        AuthenticationTypeComboBox.SelectedIndex = AuthenticationType switch
        {
            WifiAuthenticationType.WEP => 1,
            WifiAuthenticationType.None => 2,
            _ => 0,
        };

        _isUpdatingAuthenticationSelection = false;
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(PreviewWifiPayload));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(PasswordVisibility));
        OnPropertyChanged(nameof(OpenNetworkInfoVisibility));
        OnPropertyChanged(nameof(LegacySecurityWarningVisibility));
        IsPrimaryButtonEnabled = CanApply;
    }

    partial void OnSsidChanged(string value) => RefreshState();

    partial void OnAuthenticationTypeChanged(WifiAuthenticationType value)
    {
        if (value == WifiAuthenticationType.None)
            Password = string.Empty;

        SyncAuthenticationSelection();
        RefreshState();
    }

    partial void OnPasswordChanged(string value) => RefreshState();

    partial void OnIsHiddenNetworkChanged(bool value) => RefreshState();
}
