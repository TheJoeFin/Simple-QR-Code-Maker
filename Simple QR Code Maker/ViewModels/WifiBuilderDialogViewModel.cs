using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.ViewModels;

public sealed partial class WifiBuilderDialogViewModel : ObservableRecipient
{
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
    public partial int SelectedAuthenticationTypeIndex { get; set; }

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

    public void Initialize(string? initialText)
    {
        if (!WifiBuilderHelper.TryParse(initialText, out WifiBuilderState parsedState))
            return;

        ApplyState(parsedState);
    }

    public bool TryCreateResultText(out string resultText)
    {
        resultText = WifiBuilderHelper.Serialize(CreateCurrentState());
        return !string.IsNullOrWhiteSpace(resultText);
    }

    partial void OnAuthenticationTypeChanged(WifiAuthenticationType value)
    {
        if (value == WifiAuthenticationType.None)
            Password = string.Empty;

        SelectedAuthenticationTypeIndex = value switch
        {
            WifiAuthenticationType.WEP => 1,
            WifiAuthenticationType.None => 2,
            _ => 0,
        };
    }

    partial void OnSelectedAuthenticationTypeIndexChanged(int value)
    {
        WifiAuthenticationType selectedAuthenticationType = value switch
        {
            1 => WifiAuthenticationType.WEP,
            2 => WifiAuthenticationType.None,
            _ => WifiAuthenticationType.WPA,
        };

        AuthenticationType = selectedAuthenticationType;
    }

    private void ApplyState(WifiBuilderState state)
    {
        Ssid = state.Ssid;
        AuthenticationType = state.AuthenticationType;
        Password = state.Password;
        IsHiddenNetwork = state.IsHiddenNetwork;
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
}
