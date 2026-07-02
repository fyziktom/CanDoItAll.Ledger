using System.Text.Json;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using static CanDoItAll.Economy.Components.Services.AccountWorkspaceLocalServiceHelpers;

namespace CanDoItAll.Economy.Components.Services;

public enum TagSuggestionSource
{
    General = 0,
    Unique = 1
}

public enum AddressDirectoryEntryKind
{
    External = 0,
    Own = 1
}

public enum AddressDirectoryDialogMode
{
    Manage = 0,
    Select = 1
}

public enum SettingsBackupSection
{
    TagUsage = 0,
    AddressDirectory = 1,
    UiPreferences = 2
}

public enum SettingsImportConflictStrategy
{
    Merge = 0,
    ReplaceSection = 1,
    SkipExisting = 2
}

public enum VerificationStepStatus
{
    Passed = 0,
    Failed = 1,
    Warning = 2
}

public sealed record TagColorMetadata(
    string BackgroundColor,
    string BorderColor,
    string ForegroundColor,
    int PaletteIndex)
{
    public string ToCssStyle()
        => $"background:{BackgroundColor};border-color:{BorderColor};color:{ForegroundColor};";
}

public sealed record TagUsageSummary(
    string Tag,
    int GeneralCount,
    int UniqueCount,
    TagColorMetadata Color);

public sealed record AddressDirectoryEntry(
    string Address,
    string DisplayName,
    AddressDirectoryEntryKind Kind,
    bool Favorite = false,
    IReadOnlyList<string>? UserTags = null,
    string? ProfileObjectId = null,
    string? ProfileDisplayName = null,
    string? ProfileImageLink = null)
{
    public IReadOnlyList<string> TagsOrEmpty => UserTags ?? [];
}

public sealed record AddressDirectorySeed(
    string AccountScope,
    IReadOnlyList<AddressDirectoryEntry> Entries)
{
    public static AddressDirectorySeed FromWorkspace(BusinessObjectWorkspaceModel workspace)
    {
        IBusinessObjectView? profile = FindProfile(workspace.BusinessObjects);
        var entries = new List<AddressDirectoryEntry>
        {
            new(
                workspace.Address.Value,
                workspace.AccountName,
                AddressDirectoryEntryKind.Own,
                Favorite: true,
                UserTags: ["current"],
                ProfileObjectId: profile?.ObjectId.Value,
                ProfileDisplayName: workspace.AccountName,
                ProfileImageLink: ProfileImageLink(profile))
        };

        entries.AddRange(workspace.SubAccounts.Select(item => new AddressDirectoryEntry(
            item.Address.Value,
            item.Name,
            AddressDirectoryEntryKind.Own,
            UserTags: ["subaccount"])));

        entries.AddRange(workspace.AddressBookmarks.Select(item => new AddressDirectoryEntry(
            item.Address.Value,
            item.Name,
            AddressDirectoryEntryKind.External,
            UserTags: ["bookmark"])));

        return new AddressDirectorySeed(workspace.Address.Value, Deduplicate(entries));
    }

    public static AddressDirectorySeed FromBookmarks(string accountScope, IReadOnlyList<EconomyAddressBookmark> bookmarks)
        => new(accountScope, Deduplicate(bookmarks.Select(item => new AddressDirectoryEntry(
            item.Address.Value,
            item.Name,
            AddressDirectoryEntryKind.External,
            UserTags: ["bookmark"]))));

    private static IBusinessObjectView? FindProfile(IReadOnlyList<IBusinessObjectView> objects)
        => objects.FirstOrDefault(item => item.ObjectType.Equals("profile", StringComparison.OrdinalIgnoreCase));

    private static string? ProfileImageLink(IBusinessObjectView? profile)
    {
        if (profile is IMediaBusinessObjectView media && !string.IsNullOrWhiteSpace(media.ImageLink))
        {
            return media.ImageLink;
        }

        return BusinessObjectPayloadBuilder.TryParseMediaPayload(profile?.StateJson, out BusinessObjectMediaPayload? payload)
            ? payload?.ImageLink
            : null;
    }

    private static IReadOnlyList<AddressDirectoryEntry> Deduplicate(IEnumerable<AddressDirectoryEntry> entries)
        => entries
            .Where(item => !string.IsNullOrWhiteSpace(item.Address))
            .GroupBy(item => item.Address.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                AddressDirectoryEntry first = group.First();
                IReadOnlyList<string> tags = group
                    .SelectMany(item => item.TagsOrEmpty)
                    .Select(NormalizeTag)
                    .Where(tag => tag is not null)
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(tag => tag, StringComparer.Ordinal)
                    .ToArray();
                return first with
                {
                    Address = first.Address.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(first.DisplayName)
                        ? ShortAddress(first.Address)
                        : first.DisplayName.Trim(),
                    UserTags = tags
                };
            })
            .OrderByDescending(item => item.Favorite)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed record SettingsBackupPreview(
    bool IsValid,
    string Summary,
    IReadOnlyList<SettingsBackupSection> Sections,
    string? Error = null);

public sealed record SettingsBackupImportResult(
    bool Success,
    string Summary,
    IReadOnlyList<SettingsBackupSection> ImportedSections);

public sealed record VerificationStepResult(
    string StepId,
    string Label,
    VerificationStepStatus Status,
    string Summary);

public sealed record VerificationFlowResult(
    bool Success,
    string Summary,
    IReadOnlyList<VerificationStepResult> Steps,
    string? Payload = null);

public interface ITagOccurrenceService
{
    Task RecordBusinessObjectViewAsync(
        string accountScope,
        string businessObjectId,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSuggestionsAsync(
        string accountScope,
        TagSuggestionSource source,
        int maxCount = 8,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagUsageSummary>> GetSummariesAsync(
        string accountScope,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default);

    Task<TagColorMetadata> GetOrCreateColorAsync(
        string accountScope,
        string tag,
        CancellationToken cancellationToken = default);

    Task<TagSuggestionSource> GetSuggestionSourceAsync(
        string accountScope,
        CancellationToken cancellationToken = default);

    Task SetSuggestionSourceAsync(
        string accountScope,
        TagSuggestionSource source,
        CancellationToken cancellationToken = default);

    Task<TagOccurrenceExportState> ExportAsync(
        string accountScope,
        CancellationToken cancellationToken = default);

    Task ImportAsync(
        string accountScope,
        TagOccurrenceExportState state,
        SettingsImportConflictStrategy strategy,
        CancellationToken cancellationToken = default);
}

public interface IAddressDirectoryService
{
    Task<IReadOnlyList<AddressDirectoryEntry>> GetEntriesAsync(
        AddressDirectorySeed seed,
        CancellationToken cancellationToken = default);

    Task SaveEntryAsync(
        string accountScope,
        AddressDirectoryEntry entry,
        CancellationToken cancellationToken = default);

    Task RemoveEntryAsync(
        string accountScope,
        string address,
        CancellationToken cancellationToken = default);

    Task<AddressDirectoryExportState> ExportAsync(
        string accountScope,
        CancellationToken cancellationToken = default);

    Task ImportAsync(
        string accountScope,
        AddressDirectoryExportState state,
        SettingsImportConflictStrategy strategy,
        CancellationToken cancellationToken = default);
}

public interface IAccountAppSettingsBackupService
{
    Task<string> ExportAsync(
        string accountScope,
        CancellationToken cancellationToken = default);

    Task<SettingsBackupPreview> PreviewImportAsync(
        string json,
        CancellationToken cancellationToken = default);

    Task<SettingsBackupImportResult> ImportAsync(
        string accountScope,
        string json,
        IReadOnlySet<SettingsBackupSection> sections,
        SettingsImportConflictStrategy strategy,
        CancellationToken cancellationToken = default);
}

public interface IBusinessObjectVerificationService
{
    string CreateOwnershipPayload(IBusinessObjectView businessObject);

    VerificationFlowResult VerifyOwnershipPayload(string payload, IBusinessObjectView businessObject);

    VerificationFlowResult VerifyTicketState(IBusinessObjectView businessObject);
}

public sealed class TagOccurrenceService(IAccountWorkspaceCache cache) : ITagOccurrenceService
{
    private const string SchemaVersion = "candoitall-economy-tags-v1";

    public async Task RecordBusinessObjectViewAsync(
        string accountScope,
        string businessObjectId,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        TagOccurrenceExportState state = await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);
        string objectKey = string.IsNullOrWhiteSpace(businessObjectId) ? "__unknown" : businessObjectId.Trim();

        foreach (string tag in NormalizeTags(tags))
        {
            TagOccurrenceRecord record = GetOrCreateRecord(state, tag);
            record.GeneralCount++;
            record.UniqueObjectKeys.Add(objectKey);
        }

        await SaveAsync(accountScope, state, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(
        string accountScope,
        TagSuggestionSource source,
        int maxCount = 8,
        CancellationToken cancellationToken = default)
    {
        TagOccurrenceExportState state = await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);
        return state.Tags.Values
            .OrderByDescending(record => source == TagSuggestionSource.Unique ? record.UniqueObjectKeys.Count : record.GeneralCount)
            .ThenBy(record => record.Tag, StringComparer.Ordinal)
            .Take(Math.Max(1, maxCount))
            .Select(record => record.Tag)
            .ToArray();
    }

    public async Task<IReadOnlyList<TagUsageSummary>> GetSummariesAsync(
        string accountScope,
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        TagOccurrenceExportState state = await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);
        bool changed = false;
        var summaries = new List<TagUsageSummary>();
        foreach (string tag in NormalizeTags(tags))
        {
            TagOccurrenceRecord record = GetOrCreateRecord(state, tag);
            changed = true;
            summaries.Add(ToSummary(record));
        }

        if (changed)
        {
            await SaveAsync(accountScope, state, cancellationToken).ConfigureAwait(false);
        }

        return summaries;
    }

    public async Task<TagColorMetadata> GetOrCreateColorAsync(
        string accountScope,
        string tag,
        CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeTag(tag) ?? "tag";
        TagOccurrenceExportState state = await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);
        TagOccurrenceRecord record = GetOrCreateRecord(state, normalized);
        await SaveAsync(accountScope, state, cancellationToken).ConfigureAwait(false);
        return record.Color;
    }

    public async Task<TagSuggestionSource> GetSuggestionSourceAsync(
        string accountScope,
        CancellationToken cancellationToken = default)
    {
        TagOccurrenceExportState state = await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);
        return state.SuggestionSource;
    }

    public async Task SetSuggestionSourceAsync(
        string accountScope,
        TagSuggestionSource source,
        CancellationToken cancellationToken = default)
    {
        TagOccurrenceExportState state = await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);
        state.SuggestionSource = source;
        await SaveAsync(accountScope, state, cancellationToken).ConfigureAwait(false);
    }

    public Task<TagOccurrenceExportState> ExportAsync(
        string accountScope,
        CancellationToken cancellationToken = default)
        => LoadAsync(accountScope, cancellationToken);

    public async Task ImportAsync(
        string accountScope,
        TagOccurrenceExportState imported,
        SettingsImportConflictStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        TagOccurrenceExportState current = strategy == SettingsImportConflictStrategy.ReplaceSection
            ? new TagOccurrenceExportState()
            : await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);

        current.SuggestionSource = imported.SuggestionSource;
        foreach (TagOccurrenceRecord importedRecord in imported.Tags.Values)
        {
            string? normalized = NormalizeTag(importedRecord.Tag);
            if (normalized is null)
            {
                continue;
            }

            if (strategy == SettingsImportConflictStrategy.SkipExisting && current.Tags.ContainsKey(normalized))
            {
                continue;
            }

            if (!current.Tags.TryGetValue(normalized, out TagOccurrenceRecord? currentRecord))
            {
                current.Tags[normalized] = importedRecord.Clone(normalized);
                continue;
            }

            currentRecord.GeneralCount = Math.Max(currentRecord.GeneralCount, importedRecord.GeneralCount);
            foreach (string key in importedRecord.UniqueObjectKeys)
            {
                currentRecord.UniqueObjectKeys.Add(key);
            }
        }

        await SaveAsync(accountScope, current, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TagOccurrenceExportState> LoadAsync(string accountScope, CancellationToken cancellationToken)
        => await cache.GetJsonAsync<TagOccurrenceExportState>(CacheKey(accountScope), cancellationToken).ConfigureAwait(false)
            ?? new TagOccurrenceExportState();

    private Task SaveAsync(string accountScope, TagOccurrenceExportState state, CancellationToken cancellationToken)
    {
        state.SchemaVersion = SchemaVersion;
        return cache.SetJsonAsync(CacheKey(accountScope), state, cancellationToken);
    }

    private static AccountWorkspaceCacheKey CacheKey(string accountScope)
        => new("tags", SafeScope(accountScope), "usage");

    private static TagUsageSummary ToSummary(TagOccurrenceRecord record)
        => new(record.Tag, record.GeneralCount, record.UniqueObjectKeys.Count, record.Color);

    private static TagOccurrenceRecord GetOrCreateRecord(TagOccurrenceExportState state, string tag)
    {
        if (state.Tags.TryGetValue(tag, out TagOccurrenceRecord? record))
        {
            return record;
        }

        int paletteIndex = AllocatePaletteIndex(state);
        record = new TagOccurrenceRecord
        {
            Tag = tag,
            Color = TagPalette.Resolve(paletteIndex),
            GeneralCount = 0
        };
        state.Tags[tag] = record;
        return record;
    }

    private static int AllocatePaletteIndex(TagOccurrenceExportState state)
    {
        HashSet<int> used = state.Tags.Values.Select(record => record.Color.PaletteIndex).ToHashSet();
        for (int index = 0; index < TagPalette.Count; index++)
        {
            if (!used.Contains(index))
            {
                return index;
            }
        }

        return state.Tags.Count % TagPalette.Count;
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
        => tags
            .Select(NormalizeTag)
            .Where(tag => tag is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}

public sealed class AddressDirectoryService(IAccountWorkspaceCache cache) : IAddressDirectoryService
{
    private const string SchemaVersion = "candoitall-economy-address-directory-v1";

    public async Task<IReadOnlyList<AddressDirectoryEntry>> GetEntriesAsync(
        AddressDirectorySeed seed,
        CancellationToken cancellationToken = default)
    {
        AddressDirectoryExportState state = await LoadAsync(seed.AccountScope, cancellationToken).ConfigureAwait(false);
        var entries = seed.Entries.ToDictionary(item => item.Address, StringComparer.OrdinalIgnoreCase);

        foreach (AddressDirectoryEntry stored in state.Entries.Values)
        {
            if (entries.TryGetValue(stored.Address, out AddressDirectoryEntry? seeded))
            {
                entries[stored.Address] = Merge(seeded, stored);
            }
            else
            {
                entries[stored.Address] = stored;
            }
        }

        return entries.Values
            .OrderByDescending(item => item.Favorite)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task SaveEntryAsync(
        string accountScope,
        AddressDirectoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entry.Address))
        {
            return;
        }

        AddressDirectoryExportState state = await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);
        AddressDirectoryEntry normalized = NormalizeEntry(entry);
        state.Entries[normalized.Address] = normalized;
        await SaveAsync(accountScope, state, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveEntryAsync(
        string accountScope,
        string address,
        CancellationToken cancellationToken = default)
    {
        AddressDirectoryExportState state = await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);
        state.Entries.Remove(address.Trim());
        await SaveAsync(accountScope, state, cancellationToken).ConfigureAwait(false);
    }

    public Task<AddressDirectoryExportState> ExportAsync(
        string accountScope,
        CancellationToken cancellationToken = default)
        => LoadAsync(accountScope, cancellationToken);

    public async Task ImportAsync(
        string accountScope,
        AddressDirectoryExportState imported,
        SettingsImportConflictStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        AddressDirectoryExportState current = strategy == SettingsImportConflictStrategy.ReplaceSection
            ? new AddressDirectoryExportState()
            : await LoadAsync(accountScope, cancellationToken).ConfigureAwait(false);

        foreach (AddressDirectoryEntry importedEntry in imported.Entries.Values)
        {
            AddressDirectoryEntry normalized = NormalizeEntry(importedEntry);
            if (strategy == SettingsImportConflictStrategy.SkipExisting && current.Entries.ContainsKey(normalized.Address))
            {
                continue;
            }

            current.Entries[normalized.Address] = current.Entries.TryGetValue(normalized.Address, out AddressDirectoryEntry? existing)
                ? Merge(existing, normalized)
                : normalized;
        }

        await SaveAsync(accountScope, current, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AddressDirectoryExportState> LoadAsync(string accountScope, CancellationToken cancellationToken)
        => await cache.GetJsonAsync<AddressDirectoryExportState>(CacheKey(accountScope), cancellationToken).ConfigureAwait(false)
            ?? new AddressDirectoryExportState();

    private Task SaveAsync(string accountScope, AddressDirectoryExportState state, CancellationToken cancellationToken)
    {
        state.SchemaVersion = SchemaVersion;
        return cache.SetJsonAsync(CacheKey(accountScope), state, cancellationToken);
    }

    private static AccountWorkspaceCacheKey CacheKey(string accountScope)
        => new("address-directory", SafeScope(accountScope), "entries");

    private static AddressDirectoryEntry NormalizeEntry(AddressDirectoryEntry entry)
        => entry with
        {
            Address = entry.Address.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? ShortAddress(entry.Address)
                : entry.DisplayName.Trim(),
            UserTags = entry.TagsOrEmpty
                .Select(NormalizeTag)
                .Where(tag => tag is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };

    private static AddressDirectoryEntry Merge(AddressDirectoryEntry seeded, AddressDirectoryEntry stored)
        => stored with
        {
            DisplayName = string.IsNullOrWhiteSpace(stored.DisplayName) ? seeded.DisplayName : stored.DisplayName,
            Kind = seeded.Kind == AddressDirectoryEntryKind.Own ? AddressDirectoryEntryKind.Own : stored.Kind,
            ProfileObjectId = stored.ProfileObjectId ?? seeded.ProfileObjectId,
            ProfileDisplayName = stored.ProfileDisplayName ?? seeded.ProfileDisplayName,
            ProfileImageLink = stored.ProfileImageLink ?? seeded.ProfileImageLink,
            UserTags = seeded.TagsOrEmpty
                .Concat(stored.TagsOrEmpty)
                .Select(NormalizeTag)
                .Where(tag => tag is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
}

public sealed class AccountAppSettingsBackupService(
    ITagOccurrenceService tagOccurrenceService,
    IAddressDirectoryService addressDirectoryService) : IAccountAppSettingsBackupService
{
    private const string Format = "candoitall-economy-accountapp-settings-backup-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<string> ExportAsync(
        string accountScope,
        CancellationToken cancellationToken = default)
    {
        TagOccurrenceExportState tags = await tagOccurrenceService.ExportAsync(accountScope, cancellationToken).ConfigureAwait(false);
        AddressDirectoryExportState addresses = await addressDirectoryService.ExportAsync(accountScope, cancellationToken).ConfigureAwait(false);
        var document = new SettingsBackupDocument
        {
            AccountScope = accountScope,
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Tags = tags,
            AddressDirectory = addresses,
            UiPreferences = new SettingsUiPreferences
            {
                TagSuggestionSource = tags.SuggestionSource
            }
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public Task<SettingsBackupPreview> PreviewImportAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SettingsBackupDocument? document = TryRead(json, out string? error);
        if (document is null)
        {
            return Task.FromResult(new SettingsBackupPreview(false, "Backup could not be read.", [], error));
        }

        SettingsBackupSection[] sections = GetSections(document);
        return Task.FromResult(new SettingsBackupPreview(
            true,
            $"{sections.Length} section(s) ready from {document.ExportedAtUtc:u}.",
            sections));
    }

    public async Task<SettingsBackupImportResult> ImportAsync(
        string accountScope,
        string json,
        IReadOnlySet<SettingsBackupSection> sections,
        SettingsImportConflictStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        SettingsBackupDocument? document = TryRead(json, out string? error);
        if (document is null)
        {
            return new SettingsBackupImportResult(false, error ?? "Backup is invalid.", []);
        }

        var imported = new List<SettingsBackupSection>();
        if (sections.Contains(SettingsBackupSection.TagUsage) && document.Tags is not null)
        {
            await tagOccurrenceService.ImportAsync(accountScope, document.Tags, strategy, cancellationToken).ConfigureAwait(false);
            imported.Add(SettingsBackupSection.TagUsage);
        }

        if (sections.Contains(SettingsBackupSection.AddressDirectory) && document.AddressDirectory is not null)
        {
            await addressDirectoryService.ImportAsync(accountScope, document.AddressDirectory, strategy, cancellationToken).ConfigureAwait(false);
            imported.Add(SettingsBackupSection.AddressDirectory);
        }

        if (sections.Contains(SettingsBackupSection.UiPreferences) && document.UiPreferences is not null)
        {
            await tagOccurrenceService
                .SetSuggestionSourceAsync(accountScope, document.UiPreferences.TagSuggestionSource, cancellationToken)
                .ConfigureAwait(false);
            imported.Add(SettingsBackupSection.UiPreferences);
        }

        return new SettingsBackupImportResult(
            true,
            $"{imported.Count} setting section(s) imported.",
            imported);
    }

    private static SettingsBackupDocument? TryRead(string json, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Backup JSON is empty.";
            return null;
        }

        try
        {
            SettingsBackupDocument? document = JsonSerializer.Deserialize<SettingsBackupDocument>(json, JsonOptions);
            if (!string.Equals(document?.Format, Format, StringComparison.Ordinal))
            {
                error = "Backup format is unsupported.";
                return null;
            }

            return document;
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return null;
        }
    }

    private static SettingsBackupSection[] GetSections(SettingsBackupDocument document)
    {
        var sections = new List<SettingsBackupSection>();
        if (document.Tags is not null)
        {
            sections.Add(SettingsBackupSection.TagUsage);
        }

        if (document.AddressDirectory is not null)
        {
            sections.Add(SettingsBackupSection.AddressDirectory);
        }

        if (document.UiPreferences is not null)
        {
            sections.Add(SettingsBackupSection.UiPreferences);
        }

        return sections.ToArray();
    }

    private sealed class SettingsBackupDocument
    {
        public string Format { get; set; } = AccountAppSettingsBackupService.Format;

        public DateTimeOffset ExportedAtUtc { get; set; }

        public string AccountScope { get; set; } = string.Empty;

        public TagOccurrenceExportState? Tags { get; set; }

        public AddressDirectoryExportState? AddressDirectory { get; set; }

        public SettingsUiPreferences? UiPreferences { get; set; }
    }

    private sealed class SettingsUiPreferences
    {
        public TagSuggestionSource TagSuggestionSource { get; set; }
    }
}

public sealed class BusinessObjectVerificationService : IBusinessObjectVerificationService
{
    private const string OwnershipFormat = "candoitall-bo-ownership-v1";

    public string CreateOwnershipPayload(IBusinessObjectView businessObject)
    {
        var payload = new BusinessObjectOwnershipPayload
        {
            ObjectId = businessObject.ObjectId.Value,
            ObjectType = businessObject.ObjectType,
            OwnerAddress = businessObject.OwnerAddress.Value,
            OutPoint = businessObject.CurrentOutPoint.ToString(),
            DataHash = businessObject.DataHash.Value,
            Version = businessObject.Version
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public VerificationFlowResult VerifyOwnershipPayload(string payload, IBusinessObjectView businessObject)
    {
        var steps = new List<VerificationStepResult>();
        BusinessObjectOwnershipPayload? parsed = TryParseOwnershipPayload(payload);
        if (parsed is null)
        {
            return Failed("Ownership payload is not valid JSON for the BO ownership verifier.");
        }

        AddStep(steps, "format", "Payload format", parsed.Format == OwnershipFormat, parsed.Format);
        AddStep(steps, "object", "Object identity", parsed.ObjectId == businessObject.ObjectId.Value, parsed.ObjectId);
        AddStep(steps, "owner", "Owner address", parsed.OwnerAddress == businessObject.OwnerAddress.Value, parsed.OwnerAddress);
        AddStep(steps, "outpoint", "Current outpoint", parsed.OutPoint == businessObject.CurrentOutPoint.ToString(), parsed.OutPoint);
        AddStep(steps, "hash", "Data hash", parsed.DataHash == businessObject.DataHash.Value, parsed.DataHash);

        bool success = steps.All(step => step.Status == VerificationStepStatus.Passed);
        return new VerificationFlowResult(
            success,
            success ? "BusinessObject ownership payload matches the selected BO." : "BusinessObject ownership payload does not match.",
            steps,
            payload);
    }

    public VerificationFlowResult VerifyTicketState(IBusinessObjectView businessObject)
    {
        var steps = new List<VerificationStepResult>();
        AddStep(steps, "type", "Ticket type", BusinessObjectTicketVerification.IsTicketObject(businessObject.ObjectType), businessObject.ObjectType);
        AddStep(steps, "used", "Used flag", !BusinessObjectTicketVerification.StateHasUsedFlag(businessObject.StateJson), "unused required");

        bool success = steps.All(step => step.Status == VerificationStepStatus.Passed);
        return new VerificationFlowResult(
            success,
            success ? "Ticket can be verified and used." : "Ticket verification requires attention.",
            steps);
    }

    private static void AddStep(List<VerificationStepResult> steps, string id, string label, bool passed, string? summary)
        => steps.Add(new VerificationStepResult(
            id,
            label,
            passed ? VerificationStepStatus.Passed : VerificationStepStatus.Failed,
            string.IsNullOrWhiteSpace(summary) ? (passed ? "Passed" : "Failed") : summary));

    private static VerificationFlowResult Failed(string summary)
        => new(
            false,
            summary,
            [new VerificationStepResult("payload", "Payload", VerificationStepStatus.Failed, summary)]);

    private static BusinessObjectOwnershipPayload? TryParseOwnershipPayload(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<BusinessObjectOwnershipPayload>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class BusinessObjectOwnershipPayload
    {
        public string Format { get; set; } = OwnershipFormat;

        public string ObjectId { get; set; } = string.Empty;

        public string ObjectType { get; set; } = string.Empty;

        public string OwnerAddress { get; set; } = string.Empty;

        public string OutPoint { get; set; } = string.Empty;

        public string DataHash { get; set; } = string.Empty;

        public int Version { get; set; }
    }
}

public sealed class TagOccurrenceExportState
{
    public string SchemaVersion { get; set; } = "candoitall-economy-tags-v1";

    public TagSuggestionSource SuggestionSource { get; set; } = TagSuggestionSource.Unique;

    public Dictionary<string, TagOccurrenceRecord> Tags { get; set; } = new(StringComparer.Ordinal);
}

public sealed class TagOccurrenceRecord
{
    public string Tag { get; set; } = string.Empty;

    public int GeneralCount { get; set; }

    public HashSet<string> UniqueObjectKeys { get; set; } = new(StringComparer.Ordinal);

    public TagColorMetadata Color { get; set; } = TagPalette.Resolve(0);

    public TagOccurrenceRecord Clone(string tag)
        => new()
        {
            Tag = tag,
            GeneralCount = GeneralCount,
            UniqueObjectKeys = new HashSet<string>(UniqueObjectKeys, StringComparer.Ordinal),
            Color = Color
        };
}

public sealed class AddressDirectoryExportState
{
    public string SchemaVersion { get; set; } = "candoitall-economy-address-directory-v1";

    public Dictionary<string, AddressDirectoryEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class TagPalette
{
    private static readonly TagColorMetadata[] Colors =
    [
        new("#e0f2fe", "#7dd3fc", "#0c4a6e", 0),
        new("#fce7f3", "#f9a8d4", "#831843", 1),
        new("#d1fae5", "#6ee7b7", "#064e3b", 2),
        new("#fef3c7", "#fcd34d", "#78350f", 3),
        new("#ede9fe", "#c4b5fd", "#4c1d95", 4),
        new("#ffedd5", "#fdba74", "#7c2d12", 5),
        new("#e0e7ff", "#a5b4fc", "#312e81", 6),
        new("#ccfbf1", "#5eead4", "#134e4a", 7),
        new("#fce4ec", "#f48fb1", "#880e4f", 8),
        new("#e8f5e9", "#81c784", "#1b5e20", 9)
    ];

    public static int Count => Colors.Length;

    public static TagColorMetadata Resolve(int index)
        => Colors[Math.Abs(index) % Colors.Length];

    public static bool HasReadableContrast(TagColorMetadata color)
        => ContrastRatio(color.BackgroundColor, color.ForegroundColor) >= 4.5;

    private static double ContrastRatio(string background, string foreground)
    {
        double l1 = RelativeLuminance(background);
        double l2 = RelativeLuminance(foreground);
        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double RelativeLuminance(string hex)
    {
        string value = hex.TrimStart('#');
        int r = Convert.ToInt32(value[..2], 16);
        int g = Convert.ToInt32(value.Substring(2, 2), 16);
        int b = Convert.ToInt32(value.Substring(4, 2), 16);
        return 0.2126d * Channel(r) + 0.7152d * Channel(g) + 0.0722d * Channel(b);
    }

    private static double Channel(int value)
    {
        double normalized = value / 255d;
        return normalized <= 0.03928d
            ? normalized / 12.92d
            : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
    }
}

internal static class AccountWorkspaceLocalServiceHelpers
{
    public static string? NormalizeTag(string? tag)
        => TagTextValueNormalizer.Normalize(tag?.TrimStart('#'), 48);

    public static string ShortAddress(string address)
        => string.IsNullOrWhiteSpace(address)
            ? "unknown"
            : address.Length > 16
                ? $"{address[..8]}...{address[^5..]}"
                : address;

    public static string SafeScope(string accountScope)
        => string.IsNullOrWhiteSpace(accountScope) ? "default" : accountScope.Trim();
}
