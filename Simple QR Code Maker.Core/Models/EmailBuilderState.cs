namespace Simple_QR_Code_Maker.Models;

public sealed class EmailBuilderState
{
    public string To { get; set; } = string.Empty;

    public string Cc { get; set; } = string.Empty;

    public string Bcc { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}
