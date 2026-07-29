using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Models;

public sealed record BusinessObjectEditorModel(
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    Address OwnerAddress,
    string StateJson,
    DateTimeOffset EffectiveTimeUtc);
