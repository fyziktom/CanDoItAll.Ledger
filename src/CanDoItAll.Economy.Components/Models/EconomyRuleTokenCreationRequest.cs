using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Models;

public sealed record EconomyRuleTokenCreationRequest(
    AccountRuleDefinition Rule,
    OutPoint? PermissionSourceOutPoint = null);
