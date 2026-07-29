using System.Text.Json;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.BusinessObjects;

public sealed record BusinessObjectCastSource(
    LedgerAccountContext Context,
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    int Version,
    Address OwnerAddress,
    BusinessObjectOperation Operation,
    HashHex DataHash,
    OutPoint CurrentOutPoint,
    string? StateJson,
    string? ExternalRef,
    TransactionDataItemType ItemType,
    DateTimeOffset CreatedAt);

public interface IBusinessObjectView
{
    LedgerAccountContext Context { get; }

    BusinessObjectId ObjectId { get; }

    string ObjectType { get; }

    string SchemaVersion { get; }

    int Version { get; }

    Address OwnerAddress { get; }

    BusinessObjectOperation Operation { get; }

    HashHex DataHash { get; }

    OutPoint CurrentOutPoint { get; }

    string? StateJson { get; }

    DateTimeOffset CreatedAt { get; }

    DateTimeOffset ViewedAt { get; }
}

public sealed record GenericBusinessObjectView(
    LedgerAccountContext Context,
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    int Version,
    Address OwnerAddress,
    BusinessObjectOperation Operation,
    HashHex DataHash,
    OutPoint CurrentOutPoint,
    string? StateJson,
    string? ExternalRef,
    TransactionDataItemType ItemType,
    DateTimeOffset CreatedAt,
    DateTimeOffset ViewedAt) : IBusinessObjectView;

public sealed record AccountSettingsBusinessObjectView(
    LedgerAccountContext Context,
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    int Version,
    Address OwnerAddress,
    BusinessObjectOperation Operation,
    HashHex DataHash,
    OutPoint CurrentOutPoint,
    string? StateJson,
    string? ExternalRef,
    IReadOnlyDictionary<string, string> Settings,
    DateTimeOffset CreatedAt,
    DateTimeOffset ViewedAt) : IBusinessObjectView;

public interface IBusinessObjectCaster
{
    int Priority { get; }

    bool CanCast(BusinessObjectCastSource source);

    ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken);
}

public interface IBusinessObjectCastRegistry
{
    ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken);
}

public sealed class BusinessObjectCastRegistry(IEnumerable<IBusinessObjectCaster> casters) : IBusinessObjectCastRegistry
{
    private readonly IReadOnlyList<IBusinessObjectCaster> casters = casters
        .OrderByDescending(x => x.Priority)
        .ToArray();

    public ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken)
    {
        IBusinessObjectCaster? caster = casters.FirstOrDefault(x => x.CanCast(source));
        if (caster is null)
        {
            throw new InvalidOperationException($"No BusinessObject caster is registered for type '{source.ObjectType}'.");
        }

        return caster.CastAsync(source, cancellationToken);
    }
}

public sealed class GenericBusinessObjectCaster(ITimeProvider timeProvider) : IBusinessObjectCaster
{
    public int Priority => int.MinValue;

    public bool CanCast(BusinessObjectCastSource source) => true;

    public ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken)
        => ValueTask.FromResult<IBusinessObjectView>(new GenericBusinessObjectView(
            source.Context,
            source.ObjectId,
            source.ObjectType,
            source.SchemaVersion,
            source.Version,
            source.OwnerAddress,
            source.Operation,
            source.DataHash,
            source.CurrentOutPoint,
            source.StateJson,
            source.ExternalRef,
            source.ItemType,
            source.CreatedAt,
            timeProvider.UtcNow));
}

public sealed class AccountSettingsBusinessObjectCaster(ITimeProvider timeProvider) : IBusinessObjectCaster
{
    public int Priority => 100;

    public bool CanCast(BusinessObjectCastSource source)
        => source.ItemType == TransactionDataItemType.AccountSettings
            || string.Equals(source.ObjectType, "settings", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source.ObjectType, "account-settings", StringComparison.OrdinalIgnoreCase);

    public ValueTask<IBusinessObjectView> CastAsync(BusinessObjectCastSource source, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> settings = ParseSettings(source.StateJson);
        return ValueTask.FromResult<IBusinessObjectView>(new AccountSettingsBusinessObjectView(
            source.Context,
            source.ObjectId,
            source.ObjectType,
            source.SchemaVersion,
            source.Version,
            source.OwnerAddress,
            source.Operation,
            source.DataHash,
            source.CurrentOutPoint,
            source.StateJson,
            source.ExternalRef,
            settings,
            source.CreatedAt,
            timeProvider.UtcNow));
    }

    private static IReadOnlyDictionary<string, string> ParseSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        Dictionary<string, string>? parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return parsed is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new SortedDictionary<string, string>(parsed, StringComparer.Ordinal);
    }
}
