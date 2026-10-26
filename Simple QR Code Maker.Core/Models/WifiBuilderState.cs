namespace Simple_QR_Code_Maker.Models;

public enum WifiAuthenticationType
{
    WPA = 0,
    WEP = 1,
    None = 2,
}

public sealed class WifiBuilderState
{
    public string Ssid { get; set; } = string.Empty;

    public WifiAuthenticationType AuthenticationType { get; set; } = WifiAuthenticationType.WPA;

    public string Password { get; set; } = string.Empty;

    public bool IsHiddenNetwork { get; set; }

    public WifiBuilderState Clone()
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
