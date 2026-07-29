using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Ledger.Core;

public static class RuleTokenAuthority
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static TokenId RuleTokenId { get; } = new("rule");

    public const string RuleContentType = "application/vnd.candoitall.rule+json";

    public const string CreateRuleTokenAction = "create_rule_token";

    public const string RuleLineageKeyPrefix = "rule:";

    public static PermissionGrant CreateRuleTokenPermissionGrant(string ruleNamespace, bool repeatable)
        => new(CreateRuleTokenAction, PermissionTokenAuthority.NormalizeNamespace(ruleNamespace), repeatable);

    public static bool IsRuleToken(TokenTransfer? token)
        => token?.TokenId == RuleTokenId;

    public static BusinessObjectLineageKey CreateRuleLineageKey(string ruleId)
        => new($"{RuleLineageKeyPrefix}{NormalizeRuleId(ruleId)}");

    [Obsolete("This returns an internal legacy lineage marker, not a public BusinessObject id. Use CreateRuleLineageKey.")]
    public static BusinessObjectId CreateRuleObjectId(string ruleId)
        => new(CreateRuleLineageKey(ruleId).Value);

    public static TransactionData CreateData(AccountRuleDefinition rule)
    {
        AccountRuleDefinition normalized = Normalize(rule);
        string json = JsonSerializer.Serialize(normalized, SerializerOptions);
        return new TransactionData(
            RuleContentType,
            HashJson(json),
            json,
            null,
            TransactionDataItemType.Rule);
    }

    public static bool TryParseData(
        TransactionData? data,
        BusinessObjectRef? businessObject,
        out AccountRuleDefinition rule)
    {
        rule = AccountRuleDefinition.Empty;
        if (data?.InlineJson is null
            || businessObject is null
            || data.ItemType != TransactionDataItemType.Rule
            || !string.Equals(data.ContentType, RuleContentType, StringComparison.Ordinal))
        {
            return false;
        }

        if (data.DataHash is null || data.DataHash != businessObject.DataHash)
        {
            return false;
        }

        try
        {
            AccountRuleDefinition? parsed = JsonSerializer.Deserialize<AccountRuleDefinition>(data.InlineJson, SerializerOptions);
            if (parsed is null)
            {
                return false;
            }

            rule = Normalize(parsed);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static bool TryValidate(AccountRuleDefinition rule, out IReadOnlyList<string> errors)
    {
        var result = new List<string>();
        try
        {
            Normalize(rule);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            result.Add(ex.Message);
        }

        errors = result;
        return result.Count == 0;
    }

    public static AccountRuleDefinition Normalize(AccountRuleDefinition rule)
    {
        string ruleId = NormalizeRuleId(rule.RuleId);
        string ruleNamespace = PermissionTokenAuthority.NormalizeNamespace(rule.RuleNamespace);
        if (rule.Conditions.Count == 0)
        {
            throw new InvalidOperationException("Rule must include at least one condition expression.");
        }

        if (rule.Actions.Count == 0)
        {
            throw new InvalidOperationException("Rule must include at least one action.");
        }

        AccountRuleProcessingOptions processing = rule.Processing ?? AccountRuleProcessingOptions.Default;
        if (processing.MaxActionsPerRefresh <= 0)
        {
            throw new InvalidOperationException("Rule max actions per refresh must be greater than zero.");
        }

        foreach (string condition in rule.Conditions)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new InvalidOperationException("Rule condition expressions cannot be empty.");
            }
        }

        foreach (AccountRuleAction action in rule.Actions)
        {
            ValidateAction(action);
        }

        return rule with
        {
            RuleId = ruleId,
            RuleNamespace = ruleNamespace,
            Trigger = rule.Trigger ?? AccountRuleTrigger.Default,
            Variables = rule.Variables
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .ToDictionary(x => x.Key.Trim(), x => x.Value, StringComparer.Ordinal),
            Conditions = rule.Conditions.Select(x => x.Trim()).ToArray(),
            Actions = rule.Actions.ToArray(),
            Processing = processing
        };
    }

    public static string NormalizeRuleId(string ruleId)
    {
        string normalized = ruleId.Trim();
        if (!LedgerValueValidation.IsValidIdentifier(normalized, LedgerValidationLimits.MaxBusinessObjectIdLength - RuleLineageKeyPrefix.Length))
        {
            throw new ArgumentException("Rule id must be a non-empty safe identifier.", nameof(ruleId));
        }

        return normalized;
    }

    private static void ValidateAction(AccountRuleAction action)
    {
        switch (action.Kind)
        {
            case AccountRuleActionKind.Payment:
            case AccountRuleActionKind.TokenTransfer:
                if (string.IsNullOrWhiteSpace(action.ReceiverAddress))
                {
                    throw new InvalidOperationException($"{action.Kind} action requires a receiver address or receiver variable.");
                }

                if (string.IsNullOrWhiteSpace(action.AmountExpression))
                {
                    throw new InvalidOperationException($"{action.Kind} action requires an amount expression.");
                }

                if (action.Kind == AccountRuleActionKind.TokenTransfer
                    && string.IsNullOrWhiteSpace(action.TokenId))
                {
                    throw new InvalidOperationException("Token transfer action requires a token id.");
                }

                break;
            case AccountRuleActionKind.TransferBusinessObject:
                if (string.IsNullOrWhiteSpace(action.ReceiverAddress))
                {
                    throw new InvalidOperationException("TransferBusinessObject action requires a receiver address or receiver variable.");
                }

                break;
            case AccountRuleActionKind.UpdateBusinessObject:
                if (string.IsNullOrWhiteSpace(action.StateJson))
                {
                    throw new InvalidOperationException("UpdateBusinessObject action requires state JSON.");
                }

                break;
            case AccountRuleActionKind.MarkProcessed:
                break;
            default:
                throw new InvalidOperationException($"Unsupported rule action kind: {action.Kind}.");
        }
    }

    private static HashHex HashJson(string json)
        => new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant());
}

public sealed record AccountRuleDefinition(
    string RuleId,
    string RuleNamespace,
    bool Enabled,
    AccountRuleTrigger? Trigger,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<AccountRuleAction> Actions,
    AccountRuleProcessingOptions? Processing)
{
    public static AccountRuleDefinition Empty { get; } = new(
        "empty",
        "rules",
        false,
        AccountRuleTrigger.Default,
        new Dictionary<string, string>(StringComparer.Ordinal),
        Array.Empty<string>(),
        Array.Empty<AccountRuleAction>(),
        AccountRuleProcessingOptions.Default);
}

public sealed record AccountRuleTrigger(
    AccountRuleTriggerKind Kind,
    string? ObjectType = null,
    string? ProcessedProperty = "processed")
{
    public static AccountRuleTrigger Default { get; } = new(AccountRuleTriggerKind.BusinessObject);
}

public sealed record AccountRuleAction(
    AccountRuleActionKind Kind,
    string? ReceiverAddress = null,
    string? AmountExpression = null,
    string? TokenId = null,
    string? StateJson = null,
    string? ObjectIdExpression = null);

public sealed record AccountRuleProcessingOptions(
    bool MarkTriggerProcessed = true,
    string ProcessedProperty = "processed",
    bool KeepProcessedBusinessObject = true,
    int MaxActionsPerRefresh = 10)
{
    public static AccountRuleProcessingOptions Default { get; } = new();
}

public enum AccountRuleTriggerKind
{
    AccountSnapshot = 0,
    BusinessObject = 1,
    NewTransaction = 2
}

public enum AccountRuleActionKind
{
    Payment = 0,
    TokenTransfer = 1,
    TransferBusinessObject = 2,
    UpdateBusinessObject = 3,
    MarkProcessed = 4
}
