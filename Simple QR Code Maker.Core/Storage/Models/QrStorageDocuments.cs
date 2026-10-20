#nullable enable

namespace Simple_QR_Code_Maker.Core.Storage.Models;

public static class QrStorageSchema
{
    public const int CurrentVersion = 1;
}

public sealed record QrDocument
{
    public int SchemaVersion { get; init; } = QrStorageSchema.CurrentVersion;

    public QrDesign Design { get; init; } = new();

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record QrSavedDesign
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public QrDesign Design { get; init; } = new();

    public bool IsDefault { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record QrDesignDocument
{
    public int SchemaVersion { get; init; } = QrStorageSchema.CurrentVersion;

    public List<QrSavedDesign> Designs { get; init; } = [];
}

public sealed record QrHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public QrDesign Design { get; init; } = new();

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record QrHistoryDocument
{
    public int SchemaVersion { get; init; } = QrStorageSchema.CurrentVersion;

    public List<QrHistoryEntry> Entries { get; init; } = [];
}

public sealed record QrPrivacyDocument
{
    public int SchemaVersion { get; init; } = QrStorageSchema.CurrentVersion;

    public bool IsHistoryEnabled { get; init; } = true;
}
