using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Models;

public sealed record BusinessObjectSubAccountSummary(
    string Name,
    Address Address,
    Amount MainBalance,
    int UtxoCount,
    int BusinessObjectCount,
    DateTimeOffset LastActivityUtc);
