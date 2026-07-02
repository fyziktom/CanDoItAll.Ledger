using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.Tests;

public sealed class AccountWorkspaceLocalServicesTests
{
    [Fact]
    public async Task TagOccurrence_TracksGeneralAndUniqueCountsSeparately()
    {
        var cache = new InMemoryAccountWorkspaceCache();
        var service = new TagOccurrenceService(cache);

        await service.RecordBusinessObjectViewAsync("alice", "bo:1", ["ticket", "vip"]);
        await service.RecordBusinessObjectViewAsync("alice", "bo:1", ["ticket", "vip"]);
        await service.RecordBusinessObjectViewAsync("alice", "bo:2", ["ticket"]);

        TagUsageSummary ticket = (await service.GetSummariesAsync("alice", ["ticket"])).Single();

        Assert.Equal(3, ticket.GeneralCount);
        Assert.Equal(2, ticket.UniqueCount);
    }

    [Fact]
    public async Task TagOccurrence_PersistsColorAndReadableContrast()
    {
        var cache = new InMemoryAccountWorkspaceCache();
        var service = new TagOccurrenceService(cache);

        TagColorMetadata first = await service.GetOrCreateColorAsync("alice", "vip");
        TagColorMetadata second = await service.GetOrCreateColorAsync("alice", "vip");

        Assert.Equal(first, second);
        Assert.True(TagPalette.HasReadableContrast(first));
    }

    [Fact]
    public async Task AddressDirectory_MergesSeedWithPersistedFavoriteAndTags()
    {
        var cache = new InMemoryAccountWorkspaceCache();
        var service = new AddressDirectoryService(cache);
        var address = Address.FromPublicKey(Hex.Utf8("bookmark"));
        var seed = AddressDirectorySeed.FromBookmarks(
            "alice",
            [new EconomyAddressBookmark("Recent receiver", address)]);

        await service.SaveEntryAsync(
            "alice",
            new AddressDirectoryEntry(address.Value, "Receiver", AddressDirectoryEntryKind.External, Favorite: true, UserTags: ["vip"]));

        AddressDirectoryEntry entry = (await service.GetEntriesAsync(seed)).Single(item => item.Address == address.Value);

        Assert.True(entry.Favorite);
        Assert.Contains("vip", entry.TagsOrEmpty);
        Assert.Contains("bookmark", entry.TagsOrEmpty);
    }

    [Fact]
    public async Task SettingsBackup_RoundTripsSelectedSectionsAndRejectsInvalidJson()
    {
        var sourceCache = new InMemoryAccountWorkspaceCache();
        var sourceTags = new TagOccurrenceService(sourceCache);
        var sourceAddresses = new AddressDirectoryService(sourceCache);
        var sourceBackup = new AccountAppSettingsBackupService(sourceTags, sourceAddresses);
        Address address = Address.FromPublicKey(Hex.Utf8("backup-bookmark"));

        await sourceTags.RecordBusinessObjectViewAsync("alice", "bo:1", ["ticket"]);
        await sourceAddresses.SaveEntryAsync(
            "alice",
            new AddressDirectoryEntry(address.Value, "Backup receiver", AddressDirectoryEntryKind.External, Favorite: true));

        string json = await sourceBackup.ExportAsync("alice");

        var targetCache = new InMemoryAccountWorkspaceCache();
        var targetTags = new TagOccurrenceService(targetCache);
        var targetAddresses = new AddressDirectoryService(targetCache);
        var targetBackup = new AccountAppSettingsBackupService(targetTags, targetAddresses);

        SettingsBackupPreview invalid = await targetBackup.PreviewImportAsync("{broken");
        SettingsBackupImportResult imported = await targetBackup.ImportAsync(
            "alice",
            json,
            new HashSet<SettingsBackupSection>
            {
                SettingsBackupSection.TagUsage,
                SettingsBackupSection.AddressDirectory
            },
            SettingsImportConflictStrategy.Merge);

        Assert.False(invalid.IsValid);
        Assert.True(imported.Success);
        Assert.Contains(await targetTags.GetSuggestionsAsync("alice", TagSuggestionSource.General), tag => tag == "ticket");
        Assert.Contains(await targetAddresses.GetEntriesAsync(AddressDirectorySeed.FromBookmarks("alice", [])), entry => entry.Address == address.Value);
    }

    [Fact]
    public void BusinessObjectVerification_DetectsPositiveAndNegativeOwnershipPayloads()
    {
        var service = new BusinessObjectVerificationService();
        GenericBusinessObjectView target = CreateBusinessObject("bo:ticket:1", Address.FromPublicKey(Hex.Utf8("owner")));
        GenericBusinessObjectView other = target with
        {
            OwnerAddress = Address.FromPublicKey(Hex.Utf8("other-owner"))
        };

        string payload = service.CreateOwnershipPayload(target);

        VerificationFlowResult positive = service.VerifyOwnershipPayload(payload, target);
        VerificationFlowResult negative = service.VerifyOwnershipPayload(payload, other);

        Assert.True(positive.Success);
        Assert.False(negative.Success);
        Assert.Contains(negative.Steps, step => step.StepId == "owner" && step.Status == VerificationStepStatus.Failed);
    }

    private static GenericBusinessObjectView CreateBusinessObject(string id, Address owner)
        => new(
            new LedgerAccountContext(new LedgerId("test-ledger")),
            new BusinessObjectId(id),
            "ticket",
            "1.0",
            1,
            owner,
            BusinessObjectOperation.Create,
            Hex.Sha256Hex(Hex.Utf8(id)),
            new OutPoint(new TxId("tx-" + id.Replace(':', '-')), 0),
            "{\"tags\":\"#ticket\"}",
            null,
            TransactionDataItemType.BusinessObject,
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"));
}
