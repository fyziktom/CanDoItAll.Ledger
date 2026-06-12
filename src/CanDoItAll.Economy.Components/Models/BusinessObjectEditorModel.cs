using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Models;

public sealed record BusinessObjectEditorModel(
    BusinessObjectId ObjectId,
    string ObjectType,
    string SchemaVersion,
    Address OwnerAddress,
    string StateJson,
    DateTimeOffset EffectiveTimeUtc);
