using CommunityToolkit.Mvvm.ComponentModel;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.ViewModels;

[ObservableObject]
public sealed partial class EmailBuilderDialogViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewEmailUri))]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial string To { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewEmailUri))]
    public partial string Cc { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewEmailUri))]
    public partial string Bcc { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewEmailUri))]
    public partial string Subject { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewEmailUri))]
    public partial string Body { get; set; } = string.Empty;

    public string PreviewEmailUri => EmailBuilderHelper.Serialize(CreateCurrentState());

    public bool CanApply => !string.IsNullOrWhiteSpace(To);

    public void Initialize(string? initialText)
    {
        if (!EmailBuilderHelper.TryParse(initialText, out EmailBuilderState parsedState))
            return;

        ApplyState(parsedState);
    }

    public bool TryCreateResultText(out string resultText)
    {
        resultText = EmailBuilderHelper.Serialize(CreateCurrentState());
        return !string.IsNullOrWhiteSpace(resultText);
    }

    private void ApplyState(EmailBuilderState state)
    {
        To = state.To;
        Cc = state.Cc;
        Bcc = state.Bcc;
        Subject = state.Subject;
        Body = state.Body;
    }

    private EmailBuilderState CreateCurrentState()
    {
        return new EmailBuilderState
        {
            To = To,
            Cc = Cc,
            Bcc = Bcc,
            Subject = Subject,
            Body = Body,
        };
    }
}
