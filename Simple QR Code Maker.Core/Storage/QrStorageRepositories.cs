#nullable enable

using Simple_QR_Code_Maker.Core.Storage.Models;

namespace Simple_QR_Code_Maker.Core.Storage;

public sealed class QrDraftRepository
{
    private const string DraftFileName = "draft.json";
    private readonly AtomicJsonFileStore fileStore;

    public QrDraftRepository(AtomicJsonFileStore fileStore)
    {
        this.fileStore = fileStore;
    }

    public Task<QrDocument?> LoadAsync(CancellationToken cancellationToken = default) =>
        fileStore.LoadAsync(DraftFileName, QrStorageJsonContext.Default.QrDocument, cancellationToken);

    public Task SaveAsync(QrDocument draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return fileStore.SaveAsync(DraftFileName, draft, QrStorageJsonContext.Default.QrDocument, cancellationToken);
    }

    public Task ClearAsync() => fileStore.DeleteAsync(DraftFileName);

    public async Task<DraftRecoveryDecision> LoadForRecoveryAsync(
        DateTimeOffset? persistedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        QrDocument? draft = await LoadAsync(cancellationToken);
        DraftRecoveryDecision decision = DraftRecoveryPolicy.Evaluate(draft, persistedAtUtc);

        if (decision.Kind == DraftRecoveryKind.Invalid)
            await ClearAsync();

        return decision;
    }
}

public sealed class QrLogoAssetRepository
{
    private readonly AtomicJsonFileStore fileStore;

    public QrLogoAssetRepository(AtomicJsonFileStore fileStore)
    {
        this.fileStore = fileStore;
    }

    public async Task<LogoAssetId> SaveAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        string normalizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
            ? extension
            : $".{extension}";
        if (normalizedExtension.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '.'))
            throw new ArgumentException("Logo file extensions may only contain ASCII letters, digits, and a leading period.", nameof(extension));

        LogoAssetId assetId = LogoAssetId.Create($"logos/{Guid.NewGuid():N}{normalizedExtension.ToLowerInvariant()}");
        string destinationPath = fileStore.GetStoragePath(assetId.Value);
        string temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        try
        {
            await using (FileStream destination = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
                destination.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, destinationPath, overwrite: false);
            return assetId;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public Task<Stream?> OpenReadAsync(LogoAssetId assetId)
    {
        ArgumentNullException.ThrowIfNull(assetId);

        if (!LogoAssetId.TryCreate(assetId.Value, out _))
            throw new ArgumentException("The logo asset ID is invalid.", nameof(assetId));

        string path = fileStore.GetStoragePath(assetId.Value);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(LogoAssetId assetId)
    {
        ArgumentNullException.ThrowIfNull(assetId);

        if (!LogoAssetId.TryCreate(assetId.Value, out _))
            throw new ArgumentException("The logo asset ID is invalid.", nameof(assetId));

        return fileStore.DeleteAsync(assetId.Value);
    }
}

public sealed class QrDesignRepository
{
    private const string DesignsFileName = "designs.json";
    private readonly AtomicJsonFileStore fileStore;

    public QrDesignRepository(AtomicJsonFileStore fileStore)
    {
        this.fileStore = fileStore;
    }

    public async Task<QrDesignDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        QrDesignDocument? document = await fileStore.LoadAsync(
            DesignsFileName,
            QrStorageJsonContext.Default.QrDesignDocument,
            cancellationToken);

        return document?.SchemaVersion == QrStorageSchema.CurrentVersion ? document : new();
    }

    public Task SaveAsync(QrDesignDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        return fileStore.SaveAsync(
            DesignsFileName,
            document with { SchemaVersion = QrStorageSchema.CurrentVersion },
            QrStorageJsonContext.Default.QrDesignDocument,
            cancellationToken);
    }

    public async Task<QrSavedDesign?> LoadDefaultAsync(CancellationToken cancellationToken = default)
    {
        QrDesignDocument document = await LoadAsync(cancellationToken);
        return document.Designs.FirstOrDefault(design => design.IsDefault);
    }

    public async Task<QrSavedDesign> SaveDesignAsync(QrSavedDesign design, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(design);

        string name = design.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A saved design needs a name.", nameof(design));
        if (!QrDesignPolicy.IsValid(design.Design))
            throw new ArgumentException("The saved design is invalid.", nameof(design));

        QrDesignDocument document = await LoadAsync(cancellationToken);
        QrSavedDesign? existing = document.Designs.FirstOrDefault(item => item.Id == design.Id);
        if (document.Designs.Any(item => item.Id != design.Id && DesignMatchingPolicy.Matches(item, name)))
            throw new InvalidOperationException("A saved design already uses that name.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        QrSavedDesign saved = design with
        {
            Name = name,
            CreatedAtUtc = existing?.CreatedAtUtc ?? design.CreatedAtUtc,
            UpdatedAtUtc = now,
        };
        List<QrSavedDesign> designs = document.Designs
            .Where(item => item.Id != saved.Id)
            .Select(item => item with { IsDefault = saved.IsDefault ? false : item.IsDefault })
            .Append(saved)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await SaveAsync(new QrDesignDocument { Designs = designs }, cancellationToken);
        return saved;
    }

    public async Task DeleteDesignAsync(Guid designId, CancellationToken cancellationToken = default)
    {
        QrDesignDocument document = await LoadAsync(cancellationToken);
        List<QrSavedDesign> designs = document.Designs.Where(item => item.Id != designId).ToList();
        if (designs.Count == document.Designs.Count)
            return;

        await SaveAsync(new QrDesignDocument { Designs = designs }, cancellationToken);
    }

    public async Task SetDefaultAsync(Guid designId, CancellationToken cancellationToken = default)
    {
        QrDesignDocument document = await LoadAsync(cancellationToken);
        if (!document.Designs.Any(item => item.Id == designId))
            throw new KeyNotFoundException("The saved design was not found.");

        List<QrSavedDesign> designs = document.Designs
            .Select(item => item with { IsDefault = item.Id == designId })
            .ToList();
        await SaveAsync(new QrDesignDocument { Designs = designs }, cancellationToken);
    }
}

public sealed class QrHistoryRepository
{
    private const string HistoryFileName = "history.json";
    private const string PrivacyFileName = "privacy.json";
    private readonly AtomicJsonFileStore fileStore;

    public QrHistoryRepository(AtomicJsonFileStore fileStore)
    {
        this.fileStore = fileStore;
    }

    public async Task<QrPrivacyDocument> LoadPrivacyAsync(CancellationToken cancellationToken = default)
    {
        QrPrivacyDocument? document = await fileStore.LoadAsync(
            PrivacyFileName,
            QrStorageJsonContext.Default.QrPrivacyDocument,
            cancellationToken);

        return document?.SchemaVersion == QrStorageSchema.CurrentVersion ? document : new();
    }

    public async Task SetHistoryEnabledAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        QrPrivacyDocument privacy = new() { IsHistoryEnabled = isEnabled };
        await fileStore.SaveAsync(
            PrivacyFileName,
            privacy,
            QrStorageJsonContext.Default.QrPrivacyDocument,
            cancellationToken);

        if (!isEnabled)
            await fileStore.DeleteAsync(HistoryFileName);
    }

    public async Task<QrHistoryDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        QrPrivacyDocument privacy = await LoadPrivacyAsync(cancellationToken);
        if (!privacy.IsHistoryEnabled)
            return new();

        QrHistoryDocument? document = await fileStore.LoadAsync(
            HistoryFileName,
            QrStorageJsonContext.Default.QrHistoryDocument,
            cancellationToken);

        return document?.SchemaVersion == QrStorageSchema.CurrentVersion ? document : new();
    }

    public async Task SaveAsync(QrHistoryDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        QrPrivacyDocument privacy = await LoadPrivacyAsync(cancellationToken);
        if (!privacy.IsHistoryEnabled)
        {
            await fileStore.DeleteAsync(HistoryFileName);
            return;
        }

        await fileStore.SaveAsync(
            HistoryFileName,
            document with { SchemaVersion = QrStorageSchema.CurrentVersion },
            QrStorageJsonContext.Default.QrHistoryDocument,
            cancellationToken);
    }

    public async Task AddAsync(QrHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!QrDesignPolicy.IsValid(entry.Design))
            throw new ArgumentException("The history entry design is invalid.", nameof(entry));

        QrPrivacyDocument privacy = await LoadPrivacyAsync(cancellationToken);
        if (!privacy.IsHistoryEnabled)
            return;

        QrHistoryDocument document = await LoadAsync(cancellationToken);
        List<QrHistoryEntry> entries = document.Entries
            .Where(item => !HistoryMatchingPolicy.Matches(item, entry.Design))
            .Prepend(entry with { CreatedAtUtc = DateTimeOffset.UtcNow })
            .ToList();
        await SaveAsync(new QrHistoryDocument { Entries = entries }, cancellationToken);
    }

    public async Task DeleteAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        QrHistoryDocument document = await LoadAsync(cancellationToken);
        List<QrHistoryEntry> entries = document.Entries.Where(entry => entry.Id != entryId).ToList();
        if (entries.Count == document.Entries.Count)
            return;

        await SaveAsync(new QrHistoryDocument { Entries = entries }, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        SaveAsync(new QrHistoryDocument(), cancellationToken);
}
