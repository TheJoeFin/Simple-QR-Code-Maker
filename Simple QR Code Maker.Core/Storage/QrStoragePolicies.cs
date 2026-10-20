#nullable enable

using Simple_QR_Code_Maker.Core.Storage.Models;

namespace Simple_QR_Code_Maker.Core.Storage;

public enum DraftRecoveryKind
{
    None,
    Recover,
    Superseded,
    Invalid,
}

public sealed record DraftRecoveryDecision(DraftRecoveryKind Kind, QrDocument? Draft)
{
    public static DraftRecoveryDecision None { get; } = new(DraftRecoveryKind.None, null);
}

public static class DraftRecoveryPolicy
{
    public static DraftRecoveryDecision Evaluate(QrDocument? draft, DateTimeOffset? persistedAtUtc = null)
    {
        if (draft is null)
            return DraftRecoveryDecision.None;

        if (draft.SchemaVersion != QrStorageSchema.CurrentVersion || !QrDesignPolicy.IsValid(draft.Design))
            return new(DraftRecoveryKind.Invalid, draft);

        if (persistedAtUtc.HasValue && draft.UpdatedAtUtc <= persistedAtUtc.Value)
            return new(DraftRecoveryKind.Superseded, draft);

        return new(DraftRecoveryKind.Recover, draft);
    }
}

public static class HistoryMatchingPolicy
{
    public static bool Matches(QrHistoryEntry entry, QrDesign design)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(design);

        return string.Equals(entry.Design.Content, design.Content, StringComparison.Ordinal);
    }
}

public static class DesignMatchingPolicy
{
    public static bool Matches(QrSavedDesign design, string name)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(name);

        return string.Equals(design.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool Matches(QrDesign first, QrDesign second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return first == second;
    }
}

public static class QrDesignPolicy
{
    public static bool IsValid(QrDesign? design)
    {
        if (design is null
            || string.IsNullOrWhiteSpace(design.Content)
            || design.PixelSize is < 128 or > 4096
            || design.QuietZoneModules is < 0 or > 16)
        {
            return false;
        }

        return design.LogoAssetId is null
            || LogoAssetId.TryCreate(design.LogoAssetId.Value, out _);
    }
}
