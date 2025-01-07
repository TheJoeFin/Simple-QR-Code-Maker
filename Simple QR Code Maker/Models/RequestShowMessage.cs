using Microsoft.UI.Xaml.Controls;

namespace Simple_QR_Code_Maker.Models;

internal class RequestShowMessage
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public InfoBarSeverity Severity { get; set; } = InfoBarSeverity.Informational;

    public RequestShowMessage(string title, string message, InfoBarSeverity severity)
    {
        Title = title;
        Message = message;
        Severity = severity;
    }
}
