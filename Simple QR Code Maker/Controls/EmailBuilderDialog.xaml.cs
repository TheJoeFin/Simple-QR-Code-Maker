using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.Controls;

[ObservableObject]
public sealed partial class EmailBuilderDialog : ContentDialog
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

    public string? ResultText { get; private set; }

    public EmailBuilderDialog(string? initialText)
    {
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Loaded += OnLoaded;

        if (EmailBuilderHelper.TryParse(initialText, out EmailBuilderState parsedState))
            ApplyState(parsedState);

        IsPrimaryButtonEnabled = CanApply;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ToTextBox.Focus(FocusState.Programmatic);
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        string serializedText = EmailBuilderHelper.Serialize(CreateCurrentState());
        if (string.IsNullOrWhiteSpace(serializedText))
        {
            args.Cancel = true;
            return;
        }

        ResultText = serializedText;
    }

    private void ApplyState(EmailBuilderState state)
    {
        To = state.To;
        Cc = state.Cc;
        Bcc = state.Bcc;
        Subject = state.Subject;
        Body = state.Body;
        RefreshState();
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

    private void RefreshState()
    {
        OnPropertyChanged(nameof(PreviewEmailUri));
        OnPropertyChanged(nameof(CanApply));
        IsPrimaryButtonEnabled = CanApply;
    }

    partial void OnToChanged(string value) => RefreshState();

    partial void OnCcChanged(string value) => RefreshState();

    partial void OnBccChanged(string value) => RefreshState();

    partial void OnSubjectChanged(string value) => RefreshState();

    partial void OnBodyChanged(string value) => RefreshState();
}
