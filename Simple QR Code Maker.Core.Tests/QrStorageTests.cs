using Simple_QR_Code_Maker.Core.Models;
using Simple_QR_Code_Maker.Core.Storage;
using Simple_QR_Code_Maker.Core.Storage.Models;
using Xunit;

namespace Simple_QR_Code_Maker.Core.Tests;

public sealed class QrStorageTests : IDisposable
{
    private readonly string storageDirectory = Path.Combine(
        Directory.GetCurrentDirectory(),
        ".test-artifacts",
        "SimpleQrCodeMaker",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DraftRepository_RoundTripsVersionedDocumentAtomically()
    {
        AtomicJsonFileStore store = new(storageDirectory);
        QrDraftRepository repository = new(store);
        QrDocument draft = new()
        {
            Design = CreateDesign(),
            UpdatedAtUtc = new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero),
        };

        await repository.SaveAsync(draft);
        QrDocument? restored = await repository.LoadAsync();

        Assert.Equal(draft, restored);
        Assert.DoesNotContain(Directory.EnumerateFiles(storageDirectory), path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HistoryRepository_DisablingHistoryDeletesExistingHistory()
    {
        AtomicJsonFileStore store = new(storageDirectory);
        QrHistoryRepository repository = new(store);
        QrHistoryDocument history = new()
        {
            Entries = [new QrHistoryEntry { Design = CreateDesign() }],
        };

        await repository.SaveAsync(history);
        await repository.SetHistoryEnabledAsync(false);

        QrPrivacyDocument privacy = await repository.LoadPrivacyAsync();
        QrHistoryDocument restored = await repository.LoadAsync();

        Assert.False(privacy.IsHistoryEnabled);
        Assert.Empty(restored.Entries);
        Assert.False(File.Exists(Path.Combine(storageDirectory, "history.json")));
    }

    [Fact]
    public async Task DesignRepository_UpdatesDefaultAndDeletesSavedDesigns()
    {
        AtomicJsonFileStore store = new(storageDirectory);
        QrDesignRepository repository = new(store);
        QrSavedDesign first = await repository.SaveDesignAsync(new QrSavedDesign
        {
            Name = "Personal",
            Design = CreateDesign(),
            IsDefault = true,
        });
        QrSavedDesign second = await repository.SaveDesignAsync(new QrSavedDesign
        {
            Name = "Work",
            Design = CreateDesign() with { Content = "https://work.example.com" },
            IsDefault = true,
        });

        QrDesignDocument document = await repository.LoadAsync();
        QrSavedDesign? defaultDesign = await repository.LoadDefaultAsync();
        await repository.DeleteDesignAsync(second.Id);

        Assert.False(document.Designs.Single(item => item.Id == first.Id).IsDefault);
        Assert.Equal(second, defaultDesign);
        Assert.Equal(first with { IsDefault = false }, (await repository.LoadAsync()).Designs.Single());
    }

    [Fact]
    public async Task HistoryRepository_AddingMatchingContentKeepsOnlyNewestEntry()
    {
        AtomicJsonFileStore store = new(storageDirectory);
        QrHistoryRepository repository = new(store);

        await repository.AddAsync(new QrHistoryEntry { Design = CreateDesign() });
        await repository.AddAsync(new QrHistoryEntry
        {
            Design = CreateDesign() with { Foreground = QrColor.White },
        });

        QrHistoryDocument history = await repository.LoadAsync();

        Assert.Single(history.Entries);
        Assert.Equal(QrColor.White, history.Entries[0].Design.Foreground);
    }

    [Fact]
    public async Task LogoAssetRepository_StoresAssetsUnderTheAppOwnedLogosDirectory()
    {
        AtomicJsonFileStore store = new(storageDirectory);
        QrLogoAssetRepository repository = new(store);
        byte[] bytes = [1, 2, 3];

        await using MemoryStream source = new(bytes);
        LogoAssetId assetId = await repository.SaveAsync(source, "PNG");
        await using Stream? restored = await repository.OpenReadAsync(assetId);

        Assert.NotNull(restored);
        Assert.StartsWith("logos/", assetId.Value, StringComparison.Ordinal);
        using MemoryStream destination = new();
        await restored.CopyToAsync(destination);
        Assert.Equal(bytes, destination.ToArray());
    }

    [Fact]
    public void StoragePolicies_UseExplicitDraftHistoryAndDesignMatchingRules()
    {
        QrDesign design = CreateDesign();
        QrDocument draft = new() { Design = design };
        QrHistoryEntry matchingHistory = new() { Design = design with { Foreground = QrColor.White } };
        QrSavedDesign savedDesign = new() { Name = "  Personal  ", Design = design };

        Assert.Equal(DraftRecoveryKind.Recover, DraftRecoveryPolicy.Evaluate(draft).Kind);
        Assert.True(HistoryMatchingPolicy.Matches(matchingHistory, design));
        Assert.True(DesignMatchingPolicy.Matches(savedDesign, "personal"));
        Assert.False(DesignMatchingPolicy.Matches(design, design with { QuietZoneModules = 5 }));
    }

    [Theory]
    [InlineData("logos/coffee.png", true)]
    [InlineData("logos\\coffee.png", true)]
    [InlineData("../coffee.png", false)]
    [InlineData("logos/../coffee.png", false)]
    [InlineData("C:\\coffee.png", false)]
    public void LogoAssetId_AcceptsOnlyAppOwnedRelativeLogoPaths(string value, bool isValid)
    {
        bool result = LogoAssetId.TryCreate(value, out LogoAssetId? assetId);

        Assert.Equal(isValid, result);
        Assert.Equal(isValid, assetId is not null);
    }

    public void Dispose()
    {
        if (Directory.Exists(storageDirectory))
            Directory.Delete(storageDirectory, recursive: true);

        string testRootDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".test-artifacts", "SimpleQrCodeMaker");
        if (Directory.Exists(testRootDirectory) && !Directory.EnumerateFileSystemEntries(testRootDirectory).Any())
            Directory.Delete(testRootDirectory);
    }

    private static QrDesign CreateDesign() =>
        new()
        {
            Content = "https://example.com",
            ErrorCorrectionLevel = QrErrorCorrectionLevel.High,
            PixelSize = 2048,
            QuietZoneModules = 4,
            LogoAssetId = LogoAssetId.Create("logos/example.png"),
        };
}
