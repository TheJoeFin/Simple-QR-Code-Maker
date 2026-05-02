using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Simple_QR_Code_Maker.Helpers;
using Simple_QR_Code_Maker.Models;

namespace Simple_QR_Code_Maker.ViewModels;

[ObservableObject]
public sealed partial class GenerateIdsDialogViewModel
{
    private const int PreviewLimit = 5;
    private List<string> _previewValues = [];

    [ObservableProperty]
    public partial double GeneratedIdCount { get; set; } = 20;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNanoIdLengthEnabled))]
    [NotifyPropertyChangedFor(nameof(NanoIdLengthVisibility))]
    public partial SpreadsheetGeneratedIdFormatOption? SelectedGeneratedIdFormatOption { get; set; } = SpreadsheetGeneratedIdFormatOption.All[0];

    [ObservableProperty]
    public partial double NanoIdLength { get; set; } = GeneratedIdGenerator.DefaultNanoIdLength;

    [ObservableProperty]
    public partial string PrefixText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuffixText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValidationVisible))]
    public partial string ValidationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PreviewDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PreviewText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanApply { get; set; }

    public IReadOnlyList<SpreadsheetGeneratedIdFormatOption> GeneratedIdFormatOptions => SpreadsheetGeneratedIdFormatOption.All;

    public bool IsNanoIdLengthEnabled => SelectedGeneratedIdFormatOption?.Format == SpreadsheetGeneratedIdFormat.NanoId;

    public Visibility NanoIdLengthVisibility => IsNanoIdLengthEnabled ? Visibility.Visible : Visibility.Collapsed;

    public bool IsValidationVisible => !string.IsNullOrWhiteSpace(ValidationMessage);

    public GenerateIdsDialogViewModel()
    {
        RefreshState();
    }

    public bool TryCreateResultText(out string resultText)
    {
        resultText = string.Empty;

        if (!TryBuildRequest(out int count, out GeneratedIdOptions options, out string validationMessage))
        {
            ValidationMessage = validationMessage;
            return false;
        }

        int previewCount = Math.Min(count, PreviewLimit);
        List<string> values = new(count);

        if (_previewValues.Count == previewCount)
            values.AddRange(_previewValues);
        else
            values.AddRange(GeneratedIdGenerator.CreateMany(previewCount, options));

        if (count > previewCount)
            values.AddRange(GeneratedIdGenerator.CreateMany(count - previewCount, options));

        resultText = string.Join("\r", values);
        return true;
    }

    partial void OnGeneratedIdCountChanged(double value) => RefreshState();

    partial void OnSelectedGeneratedIdFormatOptionChanged(SpreadsheetGeneratedIdFormatOption? value) => RefreshState();

    partial void OnNanoIdLengthChanged(double value) => RefreshState();

    partial void OnPrefixTextChanged(string value) => RefreshState();

    partial void OnSuffixTextChanged(string value) => RefreshState();

    private void RefreshState()
    {
        ValidationMessage = string.Empty;

        if (!TryBuildRequest(out int count, out GeneratedIdOptions options, out _))
        {
            _previewValues.Clear();
            PreviewDescription = string.Empty;
            PreviewText = string.Empty;
            CanApply = false;
            return;
        }

        int previewCount = Math.Min(count, PreviewLimit);
        _previewValues = [.. GeneratedIdGenerator.CreateMany(previewCount, options)];
        PreviewDescription = count > previewCount
            ? $"Showing the first {previewCount} of {count} generated IDs."
            : $"Showing all {count} generated ID{(count == 1 ? "" : "s")}.";
        PreviewText = string.Join("\r", _previewValues);
        CanApply = true;
    }

    private bool TryBuildRequest(out int count, out GeneratedIdOptions options, out string validationMessage)
    {
        count = 0;
        options = new();
        validationMessage = string.Empty;

        if (!TryGetWholeNumber(GeneratedIdCount, out count))
        {
            validationMessage = "Enter a whole number of IDs to generate.";
            return false;
        }

        int nanoIdLength = GeneratedIdGenerator.DefaultNanoIdLength;
        if (IsNanoIdLengthEnabled
            && !TryGetWholeNumber(NanoIdLength, out nanoIdLength))
        {
            validationMessage = "Enter a whole-number NanoID length.";
            return false;
        }

        SpreadsheetGeneratedIdFormat format = SelectedGeneratedIdFormatOption?.Format ?? SpreadsheetGeneratedIdFormat.Guid;
        options = new GeneratedIdOptions
        {
            Format = format,
            NanoIdLength = nanoIdLength,
            Prefix = PrefixText,
            Suffix = SuffixText,
        };

        return true;
    }

    private static bool TryGetWholeNumber(double value, out int result)
    {
        result = 0;

        if (double.IsNaN(value) || double.IsInfinity(value))
            return false;

        if (value < 1 || value > int.MaxValue)
            return false;

        if (Math.Floor(value) != value)
            return false;

        result = Convert.ToInt32(value);
        return true;
    }
}
