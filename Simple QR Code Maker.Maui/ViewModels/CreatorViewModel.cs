using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_QR_Code_Maker.Core.Models;
using Simple_QR_Code_Maker.Core.Storage;
using Simple_QR_Code_Maker.Core.Storage.Models;
using Simple_QR_Code_Maker.Maui.Services;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;
using Simple_QR_Code_Maker.Rendering;
using System.Globalization;

namespace Simple_QR_Code_Maker.Maui.ViewModels;

public sealed partial class CreatorViewModel : ObservableObject
{
    private const string PlainTextFlow = "Plain text";
    private const string UrlFlow = "URL";
    private const string WifiFlow = "Wi-Fi";
    private const string EmailFlow = "Email";
    private const string VCardFlow = "vCard";

    private readonly QrRenderer renderer;
    private readonly QrDraftRepository draftRepository;
    private readonly QrDesignRepository designRepository;
    private readonly QrHistoryRepository historyRepository;
    private readonly IQrOutputPreparationService outputPreparationService;
    private readonly SemaphoreSlim draftSaveGate = new(1, 1);
    private CancellationTokenSource? previewCancellationSource;
    private Task? initializationTask;
    private long requestedPreviewVersion;
    private bool isRestoringDraft;
    private bool isInitialized;

    [ObservableProperty]
    private string selectedContentFlow = PlainTextFlow;

    [ObservableProperty]
    private string plainText = "https://github.com/TheJoeFin/Simple-QR-Code-Maker";

    [ObservableProperty]
    private string urlText = string.Empty;

    [ObservableProperty]
    private string wifiSsid = string.Empty;

    [ObservableProperty]
    private string wifiPassword = string.Empty;

    [ObservableProperty]
    private string selectedWifiAuthentication = "WPA";

    [ObservableProperty]
    private bool isWifiHidden;

    [ObservableProperty]
    private string emailTo = string.Empty;

    [ObservableProperty]
    private string emailCc = string.Empty;

    [ObservableProperty]
    private string emailBcc = string.Empty;

    [ObservableProperty]
    private string emailSubject = string.Empty;

    [ObservableProperty]
    private string emailBody = string.Empty;

    [ObservableProperty]
    private string vCardFormattedName = string.Empty;

    [ObservableProperty]
    private string vCardFirstName = string.Empty;

    [ObservableProperty]
    private string vCardLastName = string.Empty;

    [ObservableProperty]
    private string vCardOrganization = string.Empty;

    [ObservableProperty]
    private string vCardMobilePhone = string.Empty;

    [ObservableProperty]
    private string vCardEmail = string.Empty;

    [ObservableProperty]
    private string vCardUrl = string.Empty;

    [ObservableProperty]
    private string vCardNote = string.Empty;

    [ObservableProperty]
    private string content = string.Empty;

    [ObservableProperty]
    private string selectedCorrection = "Medium";

    [ObservableProperty]
    private string selectedSize = "1024";

    [ObservableProperty]
    private int quietZoneModules = 4;

    [ObservableProperty]
    private string foregroundHex = "#000000";

    [ObservableProperty]
    private string backgroundHex = "#FFFFFF";

    [ObservableProperty]
    private ImageSource? preview;

    [ObservableProperty]
    private string status = "Loading your draft…";

    [ObservableProperty]
    private string preparedPngPath = string.Empty;

    [ObservableProperty]
    private string preparedSvgPath = string.Empty;

    public CreatorViewModel(
        QrRenderer renderer,
        QrDraftRepository draftRepository,
        QrDesignRepository designRepository,
        QrHistoryRepository historyRepository,
        IQrOutputPreparationService outputPreparationService)
    {
        this.renderer = renderer;
        this.draftRepository = draftRepository;
        this.designRepository = designRepository;
        this.historyRepository = historyRepository;
        this.outputPreparationService = outputPreparationService;
    }

    public IReadOnlyList<string> ContentFlows { get; } = [PlainTextFlow, UrlFlow, WifiFlow, EmailFlow, VCardFlow];

    public IReadOnlyList<string> CorrectionLevels { get; } = ["Low", "Medium", "Quartile", "High"];

    public IReadOnlyList<string> OutputSizes { get; } = ["1024", "2048", "4096"];

    public IReadOnlyList<string> WifiAuthenticationTypes { get; } = ["WPA", "WEP", "None"];

    public bool IsPlainTextFlow => SelectedContentFlow == PlainTextFlow;

    public bool IsUrlFlow => SelectedContentFlow == UrlFlow;

    public bool IsWifiFlow => SelectedContentFlow == WifiFlow;

    public bool IsEmailFlow => SelectedContentFlow == EmailFlow;

    public bool IsVCardFlow => SelectedContentFlow == VCardFlow;

    public Task InitializeAsync()
    {
        initializationTask ??= InitializeCoreAsync();
        return initializationTask;
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        SynchronizeContentFromEditor();
        try
        {
            QrDesign design = CreateDesign();
            if (string.IsNullOrWhiteSpace(design.Content))
            {
                Status = "Enter content before adding it to history.";
                return;
            }

            await historyRepository.AddAsync(new QrHistoryEntry { Design = design });
            Status = "QR code added to history.";
        }
        catch (Exception exception)
        {
            Status = $"Could not add QR code to history: {exception.Message}";
        }

        SchedulePreview();
    }

    [RelayCommand]
    private async Task PreparePngAsync()
    {
        await PrepareOutputAsync(QrOutputFormat.Png);
    }

    [RelayCommand]
    private async Task PrepareSvgAsync()
    {
        await PrepareOutputAsync(QrOutputFormat.Svg);
    }

    [RelayCommand]
    private Task SavePngAsync() => SaveOutputAsync(QrOutputFormat.Png);

    [RelayCommand]
    private Task SaveSvgAsync() => SaveOutputAsync(QrOutputFormat.Svg);

    [RelayCommand]
    private Task SharePngAsync() => ShareOutputAsync(QrOutputFormat.Png);

    [RelayCommand]
    private Task ShareSvgAsync() => ShareOutputAsync(QrOutputFormat.Svg);

    public QrDesign GetCurrentDesign()
    {
        SynchronizeContentFromEditor();
        return CreateDesign();
    }

    public void ApplyDesign(QrDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);
        RestoreDesign(design);
        SchedulePreview();
    }

    partial void OnSelectedContentFlowChanged(string value)
    {
        OnPropertyChanged(nameof(IsPlainTextFlow));
        OnPropertyChanged(nameof(IsUrlFlow));
        OnPropertyChanged(nameof(IsWifiFlow));
        OnPropertyChanged(nameof(IsEmailFlow));
        OnPropertyChanged(nameof(IsVCardFlow));
        SynchronizeContentFromEditor();
    }

    partial void OnPlainTextChanged(string value) => SynchronizeContentFromEditor();

    partial void OnUrlTextChanged(string value) => SynchronizeContentFromEditor();

    partial void OnWifiSsidChanged(string value) => SynchronizeContentFromEditor();

    partial void OnWifiPasswordChanged(string value) => SynchronizeContentFromEditor();

    partial void OnSelectedWifiAuthenticationChanged(string value) => SynchronizeContentFromEditor();

    partial void OnIsWifiHiddenChanged(bool value) => SynchronizeContentFromEditor();

    partial void OnEmailToChanged(string value) => SynchronizeContentFromEditor();

    partial void OnEmailCcChanged(string value) => SynchronizeContentFromEditor();

    partial void OnEmailBccChanged(string value) => SynchronizeContentFromEditor();

    partial void OnEmailSubjectChanged(string value) => SynchronizeContentFromEditor();

    partial void OnEmailBodyChanged(string value) => SynchronizeContentFromEditor();

    partial void OnVCardFormattedNameChanged(string value) => SynchronizeContentFromEditor();

    partial void OnVCardFirstNameChanged(string value) => SynchronizeContentFromEditor();

    partial void OnVCardLastNameChanged(string value) => SynchronizeContentFromEditor();

    partial void OnVCardOrganizationChanged(string value) => SynchronizeContentFromEditor();

    partial void OnVCardMobilePhoneChanged(string value) => SynchronizeContentFromEditor();

    partial void OnVCardEmailChanged(string value) => SynchronizeContentFromEditor();

    partial void OnVCardUrlChanged(string value) => SynchronizeContentFromEditor();

    partial void OnVCardNoteChanged(string value) => SynchronizeContentFromEditor();

    partial void OnSelectedCorrectionChanged(string value) => SchedulePreview();

    partial void OnSelectedSizeChanged(string value) => SchedulePreview();

    partial void OnQuietZoneModulesChanged(int value) => SchedulePreview();

    partial void OnForegroundHexChanged(string value) => SchedulePreview();

    partial void OnBackgroundHexChanged(string value) => SchedulePreview();

    private async Task InitializeCoreAsync()
    {
        try
        {
            DraftRecoveryDecision decision = await draftRepository.LoadForRecoveryAsync();
            if (decision.Kind == DraftRecoveryKind.Recover && decision.Draft is not null)
            {
                RestoreDesign(decision.Draft.Design);
                Status = $"Recovered draft saved {decision.Draft.UpdatedAtUtc.LocalDateTime:g}.";
            }
            else if (decision.Kind == DraftRecoveryKind.Invalid)
            {
                Status = "An invalid draft was discarded. Start creating a QR code.";
            }
            else
            {
                QrSavedDesign? defaultDesign = await designRepository.LoadDefaultAsync();
                if (defaultDesign is not null)
                {
                    RestoreDesign(defaultDesign.Design);
                    Status = $"Applied default design “{defaultDesign.Name}”.";
                }
                else
                {
                    Content = PlainText;
                    Status = "Enter content to create a QR code.";
                }
            }

            isInitialized = true;
            SchedulePreview();
        }
        catch (Exception exception)
        {
            isInitialized = true;
            Content = PlainText;
            Status = $"Draft recovery failed: {exception.Message}";
            SchedulePreview();
        }
    }

    private void RestoreDesign(QrDesign design)
    {
        isRestoringDraft = true;
        try
        {
            SelectedCorrection = design.ErrorCorrectionLevel.ToString();
            SelectedSize = design.PixelSize.ToString(CultureInfo.InvariantCulture);
            QuietZoneModules = design.QuietZoneModules;
            ForegroundHex = ToHex(design.Foreground);
            BackgroundHex = ToHex(design.Background);
            Content = design.Content;

            if (VCardBuilderHelper.TryParse(Content, out VCardBuilderState vCard))
            {
                SelectedContentFlow = VCardFlow;
                VCardFormattedName = vCard.FormattedName;
                VCardFirstName = vCard.FirstName;
                VCardLastName = vCard.LastName;
                VCardOrganization = vCard.Organization;
                VCardMobilePhone = vCard.MobilePhone;
                VCardEmail = vCard.Email;
                VCardUrl = vCard.Url;
                VCardNote = vCard.Note;
            }
            else if (EmailBuilderHelper.TryParse(Content, out EmailBuilderState email))
            {
                SelectedContentFlow = EmailFlow;
                EmailTo = email.To;
                EmailCc = email.Cc;
                EmailBcc = email.Bcc;
                EmailSubject = email.Subject;
                EmailBody = email.Body;
            }
            else if (WifiBuilderHelper.TryParse(Content, out WifiBuilderState wifi))
            {
                SelectedContentFlow = WifiFlow;
                WifiSsid = wifi.Ssid;
                WifiPassword = wifi.Password;
                SelectedWifiAuthentication = wifi.AuthenticationType.ToString();
                IsWifiHidden = wifi.IsHiddenNetwork;
            }
            else if (LooksLikeUrl(Content))
            {
                SelectedContentFlow = UrlFlow;
                UrlText = Content;
            }
            else
            {
                SelectedContentFlow = PlainTextFlow;
                PlainText = Content;
            }
        }
        finally
        {
            isRestoringDraft = false;
        }
    }

    private void SynchronizeContentFromEditor()
    {
        if (isRestoringDraft)
            return;

        Content = SelectedContentFlow switch
        {
            PlainTextFlow => PlainText,
            UrlFlow => SerializeUrl(UrlText),
            WifiFlow => WifiBuilderHelper.Serialize(new WifiBuilderState
            {
                Ssid = WifiSsid,
                Password = WifiPassword,
                AuthenticationType = ParseWifiAuthentication(SelectedWifiAuthentication),
                IsHiddenNetwork = IsWifiHidden,
            }),
            EmailFlow => EmailBuilderHelper.Serialize(new EmailBuilderState
            {
                To = EmailTo,
                Cc = EmailCc,
                Bcc = EmailBcc,
                Subject = EmailSubject,
                Body = EmailBody,
            }),
            VCardFlow => VCardBuilderHelper.Serialize(new VCardBuilderState
            {
                FormattedName = VCardFormattedName,
                FirstName = VCardFirstName,
                LastName = VCardLastName,
                Organization = VCardOrganization,
                MobilePhone = VCardMobilePhone,
                Email = VCardEmail,
                Url = VCardUrl,
                Note = VCardNote,
            }),
            _ => string.Empty,
        };

        SchedulePreview();
    }

    private void SchedulePreview()
    {
        if (!isInitialized || isRestoringDraft)
            return;

        PreparedPngPath = string.Empty;
        PreparedSvgPath = string.Empty;
        long requestVersion = Interlocked.Increment(ref requestedPreviewVersion);
        previewCancellationSource?.Cancel();
        previewCancellationSource?.Dispose();
        previewCancellationSource = new CancellationTokenSource();
        CancellationToken cancellationToken = previewCancellationSource.Token;
        _ = RefreshPreviewAsync(requestVersion, cancellationToken);
    }

    private async Task RefreshPreviewAsync(long requestVersion, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            QrDesign design = CreateDesign();

            if (string.IsNullOrWhiteSpace(design.Content))
            {
                if (IsCurrentRequest(requestVersion, cancellationToken))
                {
                    Preview = null;
                    Status = "Enter content before generating a QR code.";
                }

                return;
            }

            await SaveDraftAsync(design);
            QrRenderRequest request = ToRenderRequest(design);
            byte[] png = await Task.Run(() => renderer.RenderPng(request), cancellationToken);

            if (!IsCurrentRequest(requestVersion, cancellationToken))
                return;

            Preview = ImageSource.FromStream(() => new MemoryStream(png, writable: false));
            Status = $"Preview updated: {request.PixelSize} px, {request.QuietZoneModules}-module margin.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!IsCurrentRequest(requestVersion, cancellationToken))
                return;

            Preview = null;
            Status = $"QR generation failed: {exception.Message}";
        }
    }

    private async Task PrepareOutputAsync(QrOutputFormat format)
    {
        try
        {
            await EnsurePreparedOutputAsync(format);
        }
        catch (Exception exception)
        {
            Status = $"Could not prepare output: {exception.Message}";
        }
    }

    private async Task<QrPreparedOutput> EnsurePreparedOutputAsync(QrOutputFormat format)
    {
        string existingPath = format == QrOutputFormat.Png ? PreparedPngPath : PreparedSvgPath;
        if (!string.IsNullOrWhiteSpace(existingPath) && File.Exists(existingPath))
            return new QrPreparedOutput(existingPath, GetContentType(format));

        QrDesign design = CreateDesign();
        if (string.IsNullOrWhiteSpace(design.Content))
            throw new InvalidOperationException("Enter content before preparing an output file.");

        QrPreparedOutput output = await outputPreparationService.PrepareAsync(ToRenderRequest(design), format);
        if (format == QrOutputFormat.Png)
            PreparedPngPath = output.Path;
        else
            PreparedSvgPath = output.Path;

        Status = $"{format.ToString().ToUpperInvariant()} output prepared: {Path.GetFileName(output.Path)}.";
        return output;
    }

    private async Task SaveOutputAsync(QrOutputFormat format)
    {
        try
        {
            QrPreparedOutput output = await EnsurePreparedOutputAsync(format);
            await using FileStream stream = new(output.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileSaverResult result = await FileSaver.Default.SaveAsync(Path.GetFileName(output.Path), stream);
            if (result.IsSuccessful)
            {
                Status = $"{format.ToString().ToUpperInvariant()} saved to {result.FilePath}.";
            }
            else if (result.Exception is OperationCanceledException)
            {
                Status = "Save canceled.";
            }
            else
            {
                Status = $"Could not save output: {result.Exception?.Message ?? "No destination was selected."}";
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Save canceled.";
        }
        catch (Exception exception)
        {
            Status = $"Could not save output: {exception.Message}";
        }
    }

    private async Task ShareOutputAsync(QrOutputFormat format)
    {
        try
        {
            QrPreparedOutput output = await EnsurePreparedOutputAsync(format);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Share QR code ({format.ToString().ToUpperInvariant()})",
                File = new ShareFile(output.Path, output.ContentType),
            });
            Status = "Share sheet closed.";
        }
        catch (OperationCanceledException)
        {
            Status = "Share canceled.";
        }
        catch (Exception exception)
        {
            Status = $"Could not share output: {exception.Message}";
        }
    }

    private async Task SaveDraftAsync(QrDesign design)
    {
        await draftSaveGate.WaitAsync();
        try
        {
            await draftRepository.SaveAsync(new QrDocument
            {
                Design = design,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        finally
        {
            draftSaveGate.Release();
        }
    }

    private QrDesign CreateDesign()
    {
        if (!int.TryParse(SelectedSize, NumberStyles.None, CultureInfo.InvariantCulture, out int pixelSize))
            throw new InvalidOperationException("Choose a valid output size.");
        if (!TryParseColor(ForegroundHex, out QrColor foreground))
            throw new InvalidOperationException("Foreground color must be #RRGGBB or #RRGGBBAA.");
        if (!TryParseColor(BackgroundHex, out QrColor background))
            throw new InvalidOperationException("Background color must be #RRGGBB or #RRGGBBAA.");

        return new QrDesign
        {
            Content = Content.Trim(),
            ErrorCorrectionLevel = ParseCorrectionLevel(SelectedCorrection),
            PixelSize = pixelSize,
            QuietZoneModules = QuietZoneModules,
            Foreground = foreground,
            Background = background,
        };
    }

    private static QrRenderRequest ToRenderRequest(QrDesign design) =>
        new(
            design.Content,
            design.ErrorCorrectionLevel,
            design.PixelSize,
            design.QuietZoneModules,
            design.Foreground,
            design.Background);

    private bool IsCurrentRequest(long requestVersion, CancellationToken cancellationToken) =>
        isInitialized && requestVersion == Interlocked.Read(ref requestedPreviewVersion) && !cancellationToken.IsCancellationRequested;

    private static string SerializeUrl(string value) =>
        UrlBuilderHelper.SerializeText(UrlBuilderHelper.ParseText(value));

    private static bool LooksLikeUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && !string.IsNullOrWhiteSpace(uri.Host);

    private static WifiAuthenticationType ParseWifiAuthentication(string authentication) => authentication switch
    {
        "WEP" => WifiAuthenticationType.WEP,
        "None" => WifiAuthenticationType.None,
        _ => WifiAuthenticationType.WPA,
    };

    private static QrErrorCorrectionLevel ParseCorrectionLevel(string correction) => correction switch
    {
        "Low" => QrErrorCorrectionLevel.Low,
        "Medium" => QrErrorCorrectionLevel.Medium,
        "Quartile" => QrErrorCorrectionLevel.Quartile,
        "High" => QrErrorCorrectionLevel.High,
        _ => throw new ArgumentOutOfRangeException(nameof(correction)),
    };

    private static bool TryParseColor(string value, out QrColor color)
    {
        color = default;
        string hex = value.Trim().TrimStart('#');
        if (hex.Length is not 6 and not 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packed))
            return false;

        byte red = (byte)(packed >> (hex.Length == 8 ? 24 : 16));
        byte green = (byte)(packed >> (hex.Length == 8 ? 16 : 8));
        byte blue = (byte)(packed >> (hex.Length == 8 ? 8 : 0));
        byte alpha = hex.Length == 8 ? (byte)packed : byte.MaxValue;
        color = new QrColor(red, green, blue, alpha);
        return true;
    }

    private static string ToHex(QrColor color) =>
        color.Alpha == byte.MaxValue
            ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
            : $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}{color.Alpha:X2}";

    private static string GetContentType(QrOutputFormat format) => format switch
    {
        QrOutputFormat.Png => "image/png",
        QrOutputFormat.Svg => "image/svg+xml",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}
