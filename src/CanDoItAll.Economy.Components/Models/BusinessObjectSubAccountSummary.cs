using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Models;

public sealed record BusinessObjectSubAccountSummary(
    string Name,
    Address Address,
    Amount MainBalance,
    int UtxoCount,
    int BusinessObjectCount,
    DateTimeOffset LastActivityUtc);
