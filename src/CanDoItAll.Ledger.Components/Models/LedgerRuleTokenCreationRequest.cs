using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Models;

public sealed record LedgerRuleTokenCreationRequest(
    AccountRuleDefinition Rule,
    OutPoint? PermissionSourceOutPoint = null);
