using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

internal sealed class AccountRuleExecutor(IAccountRuleExpressionEvaluator evaluator)
{
    private static readonly Regex TemplatePlaceholderPattern = new(
        "\\{\\{\\s*(?<name>[A-Za-z0-9_.:-]+)\\s*\\}\\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<AccountRuleExecutionResult>> ExecuteAsync(
        BusinessObjectAccount account,
        AccountRefreshResult refresh,
        AccountRuleExecutionOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<AccountRuleExecutionResult>();
        IReadOnlyList<DiscoveredRule> rules = DiscoverRules(refresh.Snapshot);
        if (rules.Count == 0)
        {
            return results;
        }

        HashSet<OutPoint> ruleOutPoints = rules.Select(rule => rule.Holding.CurrentOutPoint).ToHashSet();
        int actionsSubmitted = 0;
        foreach (DiscoveredRule rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!rule.Definition.Enabled)
            {
                results.Add(Skipped(rule.Definition, null, AccountRuleActionKind.MarkProcessed, "Rule is disabled."));
                continue;
            }

            foreach (RuleTriggerContext trigger in SelectTriggers(rule, refresh, ruleOutPoints, options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (actionsSubmitted >= options.MaxActionsPerRefresh)
                {
                    results.Add(Skipped(rule.Definition, trigger, AccountRuleActionKind.MarkProcessed, "Max actions per refresh reached."));
                    return results;
                }

                string ruleExecutionId = CreateRuleExecutionId(rule.Definition, trigger, refresh.Snapshot);
                IReadOnlyDictionary<string, object?> variables = BuildVariables(refresh.Snapshot, rule.Definition, trigger, ruleExecutionId);
                AccountRuleBooleanEvaluation condition = EvaluateConditions(rule.Definition, variables, evaluator);
                if (!condition.Succeeded)
                {
                    results.Add(Failed(rule.Definition, trigger, AccountRuleActionKind.MarkProcessed, condition.Error ?? "Condition evaluation failed."));
                    continue;
                }

                if (!condition.Value)
                {
                    results.Add(Skipped(rule.Definition, trigger, AccountRuleActionKind.MarkProcessed, "Rule conditions evaluated to false."));
                    continue;
                }

                bool explicitProcessedAction = false;
                foreach (AccountRuleAction action in rule.Definition.Actions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (actionsSubmitted >= options.MaxActionsPerRefresh)
                    {
                        results.Add(Skipped(rule.Definition, trigger, action.Kind, "Max actions per refresh reached."));
                        return results;
                    }

                    AccountRuleExecutionResult actionResult = await ExecuteActionAsync(
                            account,
                            refresh.Snapshot,
                            rule.Definition,
                            trigger,
                        action,
                        variables,
                        evaluator,
                        ruleExecutionId,
                            options,
                            cancellationToken)
                        .ConfigureAwait(false);
                    results.Add(actionResult);
                    if (actionResult.Status == AccountRuleExecutionStatus.Executed)
                    {
                        actionsSubmitted++;
                    }

                    explicitProcessedAction |= action.Kind == AccountRuleActionKind.MarkProcessed
                        || (action.Kind is AccountRuleActionKind.UpdateBusinessObject or AccountRuleActionKind.TransferBusinessObject
                            && string.IsNullOrWhiteSpace(action.ObjectIdExpression));
                    if (actionResult.Status == AccountRuleExecutionStatus.Failed)
                    {
                        break;
                    }
                }

                AccountRuleProcessingOptions processing = rule.Definition.Processing ?? AccountRuleProcessingOptions.Default;
                if (trigger.BusinessObject is not null
                    && options.MarkTriggerProcessed
                    && processing.MarkTriggerProcessed
                    && !explicitProcessedAction
                    && actionsSubmitted < options.MaxActionsPerRefresh)
                {
                    AccountRuleExecutionResult processed = await MarkProcessedAsync(
                            account,
                            rule.Definition,
                            trigger.BusinessObject,
                            ruleExecutionId,
                            refresh.Snapshot.RefreshedAtUtc,
                            options,
                            cancellationToken)
                        .ConfigureAwait(false);
                    results.Add(processed);
                    if (processed.Status == AccountRuleExecutionStatus.Executed)
                    {
                        actionsSubmitted++;
                    }
                }
            }
        }

        return results;
    }

    private static IReadOnlyList<DiscoveredRule> DiscoverRules(AccountRefreshSnapshot snapshot)
    {
        HashSet<OutPoint> ruleOutPoints = snapshot.SpendableOutputs
            .Where(output => RuleTokenAuthority.IsRuleToken(output.Token) && output.BusinessObject is not null)
            .Select(output => output.OutPoint)
            .ToHashSet();

        return snapshot.BusinessObjects
            .Where(holding => ruleOutPoints.Contains(holding.CurrentOutPoint))
            .Select(TryDiscoverRule)
            .Where(rule => rule is not null)
            .Select(rule => rule!)
            .ToArray();
    }

    private static DiscoveredRule? TryDiscoverRule(LedgerBusinessObjectHolding holding)
    {
        var data = new TransactionData(
            RuleTokenAuthority.RuleContentType,
            holding.DataHash,
            holding.StateJson,
            holding.ExternalRef,
            holding.ItemType);
        var businessObject = new BusinessObjectRef(
            holding.ObjectId,
            holding.Version,
            holding.Operation,
            holding.DataHash);

        return RuleTokenAuthority.TryParseData(data, businessObject, out AccountRuleDefinition rule)
            ? new DiscoveredRule(rule, holding)
            : null;
    }

    private static IEnumerable<RuleTriggerContext> SelectTriggers(
        DiscoveredRule rule,
        AccountRefreshResult refresh,
        IReadOnlySet<OutPoint> ruleOutPoints,
        AccountRuleExecutionOptions options)
    {
        AccountRefreshSnapshot snapshot = refresh.Snapshot;
        AccountRuleTrigger trigger = rule.Definition.Trigger ?? AccountRuleTrigger.Default;
        if (trigger.Kind == AccountRuleTriggerKind.AccountSnapshot)
        {
            yield return RuleTriggerContext.ForAccountSnapshot();
            yield break;
        }

        if (trigger.Kind == AccountRuleTriggerKind.NewTransaction)
        {
            foreach (LedgerTransactionSummary transaction in refresh.Delta.NewTransactions)
            {
                if (MatchesTransactionTrigger(transaction, trigger))
                {
                    yield return RuleTriggerContext.ForTransaction(transaction);
                }
            }

            yield break;
        }

        string processedProperty = trigger.ProcessedProperty ?? options.ProcessedProperty;
        foreach (LedgerBusinessObjectHolding holding in snapshot.BusinessObjects)
        {
            if (ruleOutPoints.Contains(holding.CurrentOutPoint))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(trigger.ObjectType)
                && !string.Equals(holding.ObjectType, trigger.ObjectType, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsProcessed(holding.StateJson, processedProperty))
            {
                continue;
            }

            yield return RuleTriggerContext.ForBusinessObject(holding);
        }
    }

    private static bool MatchesTransactionTrigger(LedgerTransactionSummary transaction, AccountRuleTrigger trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger.ObjectType))
        {
            return true;
        }

        return string.Equals(transaction.Data?.ContentType, trigger.ObjectType, StringComparison.Ordinal)
            || string.Equals(transaction.Data?.ItemType.ToString(), trigger.ObjectType, StringComparison.OrdinalIgnoreCase);
    }

    private static AccountRuleBooleanEvaluation EvaluateConditions(
        AccountRuleDefinition rule,
        IReadOnlyDictionary<string, object?> variables,
        IAccountRuleExpressionEvaluator evaluator)
    {
        foreach (string condition in rule.Conditions)
        {
            AccountRuleBooleanEvaluation evaluation = evaluator.EvaluateBoolean(condition, variables);
            if (!evaluation.Succeeded || !evaluation.Value)
            {
                return evaluation;
            }
        }

        return AccountRuleBooleanEvaluation.Success(true);
    }

    private static async Task<AccountRuleExecutionResult> ExecuteActionAsync(
        BusinessObjectAccount account,
        AccountRefreshSnapshot snapshot,
        AccountRuleDefinition rule,
        RuleTriggerContext trigger,
        AccountRuleAction action,
        IReadOnlyDictionary<string, object?> variables,
        IAccountRuleExpressionEvaluator evaluator,
        string ruleExecutionId,
        AccountRuleExecutionOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (action.Kind)
            {
                case AccountRuleActionKind.Payment:
                    return await ExecutePaymentAsync(account, rule, trigger, action, variables, evaluator, cancellationToken).ConfigureAwait(false);
                case AccountRuleActionKind.TokenTransfer:
                    return await ExecuteTokenTransferAsync(account, rule, trigger, action, variables, evaluator, cancellationToken).ConfigureAwait(false);
                case AccountRuleActionKind.TransferBusinessObject:
                    return await ExecuteBusinessObjectTransferAsync(account, snapshot, rule, trigger, action, variables, cancellationToken).ConfigureAwait(false);
                case AccountRuleActionKind.UpdateBusinessObject:
                    return await ExecuteBusinessObjectUpdateAsync(account, snapshot, rule, trigger, action, variables, cancellationToken).ConfigureAwait(false);
                case AccountRuleActionKind.MarkProcessed:
                    if (!TryResolveBusinessObjectTarget(snapshot, trigger, action, variables, out LedgerBusinessObjectHolding? processedTarget, out string? processedTargetError))
                    {
                        return Skipped(rule, trigger, action.Kind, processedTargetError ?? "MarkProcessed requires a trigger BusinessObject.");
                    }

                    return await MarkProcessedAsync(account, rule, processedTarget!, ruleExecutionId, snapshot.RefreshedAtUtc, options, cancellationToken).ConfigureAwait(false);
                default:
                    return Failed(rule, trigger, action.Kind, $"Unsupported action kind {action.Kind}.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Failed(rule, trigger, action.Kind, ex.Message);
        }
    }

    private static async Task<AccountRuleExecutionResult> ExecutePaymentAsync(
        BusinessObjectAccount account,
        AccountRuleDefinition rule,
        RuleTriggerContext trigger,
        AccountRuleAction action,
        IReadOnlyDictionary<string, object?> variables,
        IAccountRuleExpressionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        if (!TryResolveAddress(action.ReceiverAddress, variables, out Address receiverAddress, out string? receiverError))
        {
            return Failed(rule, trigger, action.Kind, receiverError ?? "Payment action requires a valid receiver address.");
        }

        AccountRuleNumberEvaluation amount = evaluator.EvaluateNumber(action.AmountExpression ?? string.Empty, variables);
        if (!amount.Succeeded)
        {
            return Failed(rule, trigger, action.Kind, amount.Error ?? "Payment amount evaluation failed.");
        }

        if (amount.Value <= 0m)
        {
            return Skipped(rule, trigger, action.Kind, "Payment amount is not positive.");
        }

        LedgerSubmitResult result = await account.SendPaymentAsync(
                new PaymentRequest(receiverAddress, new Amount(amount.Value)),
                cancellationToken)
            .ConfigureAwait(false);
        return FromSubmitResult(rule, trigger, action.Kind, result, $"Payment {amount.Value} to {receiverAddress.Value}.");
    }

    private static async Task<AccountRuleExecutionResult> ExecuteTokenTransferAsync(
        BusinessObjectAccount account,
        AccountRuleDefinition rule,
        RuleTriggerContext trigger,
        AccountRuleAction action,
        IReadOnlyDictionary<string, object?> variables,
        IAccountRuleExpressionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.TokenId))
        {
            return Failed(rule, trigger, action.Kind, "Token transfer action requires a token id.");
        }

        if (!TryResolveAddress(action.ReceiverAddress, variables, out Address receiverAddress, out string? receiverError))
        {
            return Failed(rule, trigger, action.Kind, receiverError ?? "Token transfer action requires a valid receiver address.");
        }

        AccountRuleNumberEvaluation amount = evaluator.EvaluateNumber(action.AmountExpression ?? string.Empty, variables);
        if (!amount.Succeeded)
        {
            return Failed(rule, trigger, action.Kind, amount.Error ?? "Token amount evaluation failed.");
        }

        if (amount.Value <= 0m)
        {
            return Skipped(rule, trigger, action.Kind, "Token transfer amount is not positive.");
        }

        LedgerSubmitResult result = await account.SendTokenTransferAsync(
                new TokenTransferRequest(
                    new TokenId(action.TokenId),
                    new Amount(amount.Value),
                    receiverAddress),
                cancellationToken)
            .ConfigureAwait(false);
        return FromSubmitResult(rule, trigger, action.Kind, result, $"Token transfer {amount.Value} {action.TokenId}.");
    }

    private static async Task<AccountRuleExecutionResult> ExecuteBusinessObjectTransferAsync(
        BusinessObjectAccount account,
        AccountRefreshSnapshot snapshot,
        AccountRuleDefinition rule,
        RuleTriggerContext trigger,
        AccountRuleAction action,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        if (!TryResolveBusinessObjectTarget(snapshot, trigger, action, variables, out LedgerBusinessObjectHolding? target, out string? targetError))
        {
            return Failed(rule, trigger, action.Kind, targetError ?? "BusinessObject transfer requires a trigger object.");
        }

        if (!TryResolveAddress(action.ReceiverAddress, variables, out Address receiverAddress, out string? receiverError))
        {
            return Failed(rule, trigger, action.Kind, receiverError ?? "BusinessObject transfer requires a valid receiver address.");
        }

        string? stateJson = null;
        if (!string.IsNullOrWhiteSpace(action.StateJson)
            && !TryBuildActionStateJson(action.StateJson, target!.StateJson, variables, out stateJson, out string? stateError))
        {
            return Failed(rule, trigger, action.Kind, stateError ?? "BusinessObject transfer state JSON is invalid.");
        }

        LedgerSubmitResult result = await account.TransferBusinessObjectAsync(
                new TransferBusinessObjectRequest(target!.ObjectId, receiverAddress, StateJson: stateJson),
                cancellationToken)
            .ConfigureAwait(false);
        return FromSubmitResult(rule, trigger, action.Kind, result, $"Transferred {target.ObjectId.Value}.");
    }

    private static async Task<AccountRuleExecutionResult> ExecuteBusinessObjectUpdateAsync(
        BusinessObjectAccount account,
        AccountRefreshSnapshot snapshot,
        AccountRuleDefinition rule,
        RuleTriggerContext trigger,
        AccountRuleAction action,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        if (!TryResolveBusinessObjectTarget(snapshot, trigger, action, variables, out LedgerBusinessObjectHolding? target, out string? targetError))
        {
            return Failed(rule, trigger, action.Kind, targetError ?? "BusinessObject update requires a trigger object.");
        }

        if (string.IsNullOrWhiteSpace(action.StateJson))
        {
            return Failed(rule, trigger, action.Kind, "BusinessObject update requires state JSON.");
        }

        if (!TryBuildActionStateJson(action.StateJson, target!.StateJson, variables, out string stateJson, out string? stateError))
        {
            return Failed(rule, trigger, action.Kind, stateError ?? "BusinessObject update state JSON is invalid.");
        }

        LedgerSubmitResult result = await account.UpdateBusinessObjectAsync(
                new UpdateBusinessObjectRequest(target.ObjectId, stateJson),
                cancellationToken)
            .ConfigureAwait(false);
        return FromSubmitResult(rule, trigger, action.Kind, result, $"Updated {target.ObjectId.Value}.");
    }

    private static async Task<AccountRuleExecutionResult> MarkProcessedAsync(
        BusinessObjectAccount account,
        AccountRuleDefinition rule,
        LedgerBusinessObjectHolding trigger,
        string ruleExecutionId,
        DateTimeOffset processedAtUtc,
        AccountRuleExecutionOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.KeepProcessedBusinessObject)
        {
            string closedStateJson = SetProcessed(trigger.StateJson, options.ProcessedProperty, rule, ruleExecutionId, processedAtUtc);
            LedgerSubmitResult closeResult = await account.CloseBusinessObjectAsync(
                    new CloseBusinessObjectRequest(trigger.ObjectId, closedStateJson),
                    cancellationToken)
                .ConfigureAwait(false);
            return FromSubmitResult(rule, RuleTriggerContext.ForBusinessObject(trigger), AccountRuleActionKind.MarkProcessed, closeResult, $"Closed processed trigger {trigger.ObjectId.Value}.");
        }

        string stateJson = SetProcessed(trigger.StateJson, options.ProcessedProperty, rule, ruleExecutionId, processedAtUtc);
        LedgerSubmitResult result = await account.UpdateBusinessObjectAsync(
                new UpdateBusinessObjectRequest(trigger.ObjectId, stateJson),
                cancellationToken)
            .ConfigureAwait(false);
        return FromSubmitResult(rule, RuleTriggerContext.ForBusinessObject(trigger), AccountRuleActionKind.MarkProcessed, result, $"Marked {trigger.ObjectId.Value} processed.");
    }

    private static IReadOnlyDictionary<string, object?> BuildVariables(
        AccountRefreshSnapshot snapshot,
        AccountRuleDefinition rule,
        RuleTriggerContext trigger,
        string ruleExecutionId)
    {
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["account_address"] = snapshot.Address.Value,
            ["account_main"] = snapshot.Balance.MainBalance.Value,
            ["utxo_count"] = snapshot.Balance.UtxoCount,
            ["business_object_count"] = snapshot.BusinessObjects.Count,
            ["transaction_count"] = snapshot.Transactions.Count,
            ["now_unix"] = snapshot.RefreshedAtUtc.ToUnixTimeSeconds(),
            ["rule_id"] = rule.RuleId,
            ["rule_namespace"] = rule.RuleNamespace,
            ["rule_execution_id"] = ruleExecutionId
        };

        foreach ((TokenId tokenId, Amount amount) in snapshot.Balance.TokenBalances)
        {
            variables[$"token_{NormalizeVariableSegment(tokenId.Value)}"] = amount.Value;
        }

        foreach ((string key, string value) in rule.Variables)
        {
            variables[NormalizeVariableSegment(key)] = ParseVariableValue(value);
        }

        if (trigger.BusinessObject is not null)
        {
            LedgerBusinessObjectHolding businessObject = trigger.BusinessObject;
            variables["trigger_object_id"] = businessObject.ObjectId.Value;
            variables["trigger_object_type"] = businessObject.ObjectType;
            variables["trigger_owner_address"] = businessObject.OwnerAddress.Value;
            variables["trigger_version"] = businessObject.Version;
            variables["trigger_created_unix"] = businessObject.CreatedAt.ToUnixTimeSeconds();
            variables["trigger_age_seconds"] = Math.Max(0, (snapshot.RefreshedAtUtc - businessObject.CreatedAt).TotalSeconds);
            variables["bo_object_id"] = businessObject.ObjectId.Value;
            variables["bo_object_type"] = businessObject.ObjectType;
            variables["bo_owner_address"] = businessObject.OwnerAddress.Value;

            foreach ((string key, object? value) in ReadObjectProperties(businessObject.StateJson))
            {
                string normalized = NormalizeVariableSegment(key);
                variables[$"bo_{normalized}"] = value;
                variables[$"trigger_{normalized}"] = value;
            }
        }

        if (trigger.Transaction is not null)
        {
            LedgerTransactionSummary transaction = trigger.Transaction;
            variables["trigger_tx_id"] = transaction.TxId.Value;
            variables["trigger_tx_kind"] = transaction.Kind.ToString();
            variables["trigger_tx_status"] = transaction.Status.ToString();
            variables["tx_id"] = transaction.TxId.Value;
            variables["tx_kind"] = transaction.Kind.ToString();
            variables["tx_status"] = transaction.Status.ToString();
            variables["tx_unix"] = transaction.Timestamp.ToUnixTimeSeconds();
            variables["tx_input_count"] = transaction.Inputs.Count;
            variables["tx_output_count"] = transaction.Outputs.Count;
            variables["tx_output_main"] = transaction.Outputs.Sum(output => output.MainAmount.Value);
            variables["tx_account_output_main"] = transaction.Outputs
                .Where(output => output.Address == snapshot.Address)
                .Sum(output => output.MainAmount.Value);
            variables["tx_account_input_count"] = transaction.Inputs.Count(input => input.Address == snapshot.Address);
            variables["tx_has_data"] = transaction.Data is not null;

            if (!string.IsNullOrWhiteSpace(transaction.Data?.InlineJson))
            {
                foreach ((string key, object? value) in ReadObjectProperties(transaction.Data.InlineJson))
                {
                    string normalized = NormalizeVariableSegment(key);
                    variables[$"txdata_{normalized}"] = value;
                    variables[$"trigger_{normalized}"] = value;
                }
            }
        }

        return variables;
    }

    private static bool TryResolveAddress(
        string? expression,
        IReadOnlyDictionary<string, object?> variables,
        out Address address,
        out string? error)
    {
        address = default;
        if (!TryResolveTextExpression(expression, variables, out string text, out error))
        {
            return false;
        }

        if (Address.TryParse(text, out address))
        {
            error = null;
            return true;
        }

        error = $"Receiver expression '{expression}' resolved to an invalid address.";
        return false;
    }

    private static bool TryResolveBusinessObjectTarget(
        AccountRefreshSnapshot snapshot,
        RuleTriggerContext trigger,
        AccountRuleAction action,
        IReadOnlyDictionary<string, object?> variables,
        out LedgerBusinessObjectHolding? target,
        out string? error)
    {
        target = null;
        if (string.IsNullOrWhiteSpace(action.ObjectIdExpression))
        {
            if (trigger.BusinessObject is null)
            {
                error = $"{action.Kind} requires a trigger BusinessObject or object id expression.";
                return false;
            }

            target = trigger.BusinessObject;
            error = null;
            return true;
        }

        if (!TryResolveTextExpression(action.ObjectIdExpression, variables, out string objectIdText, out error))
        {
            return false;
        }

        target = snapshot.BusinessObjects.FirstOrDefault(x => string.Equals(x.ObjectId.Value, objectIdText, StringComparison.Ordinal));
        if (target is null)
        {
            error = $"Object id expression '{action.ObjectIdExpression}' resolved to '{objectIdText}', but that BusinessObject is not owned by the account snapshot.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryResolveTextExpression(
        string? expression,
        IReadOnlyDictionary<string, object?> variables,
        out string text,
        out string? error)
    {
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "Text expression cannot be empty.";
            return false;
        }

        string trimmed = expression.Trim();
        Match fullTemplate = TemplatePlaceholderPattern.Match(trimmed);
        if (fullTemplate.Success && fullTemplate.Index == 0 && fullTemplate.Length == trimmed.Length)
        {
            return TryResolveVariableText(fullTemplate.Groups["name"].Value, variables, out text, out error);
        }

        if (TryGetVariableValue(trimmed, variables, out object? value)
            || TryGetVariableValue(NormalizeVariableSegment(trimmed), variables, out value))
        {
            text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            error = null;
            return true;
        }

        if (LooksLikeRuleVariable(trimmed))
        {
            error = $"Variable '{trimmed}' was not available.";
            return false;
        }

        text = trimmed;
        error = null;
        return true;
    }

    private static bool LooksLikeRuleVariable(string value)
        => value.StartsWith("account_", StringComparison.Ordinal)
            || value.StartsWith("bo_", StringComparison.Ordinal)
            || value.StartsWith("trigger_", StringComparison.Ordinal)
            || value.StartsWith("tx_", StringComparison.Ordinal)
            || value.StartsWith("txdata_", StringComparison.Ordinal)
            || value.StartsWith("rule_", StringComparison.Ordinal);

    private static bool TryResolveVariableText(
        string variableName,
        IReadOnlyDictionary<string, object?> variables,
        out string text,
        out string? error)
    {
        text = string.Empty;
        if (!TryGetVariableValue(variableName, variables, out object? value)
            && !TryGetVariableValue(NormalizeVariableSegment(variableName), variables, out value))
        {
            error = $"Variable '{variableName}' was not available.";
            return false;
        }

        text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        error = null;
        return true;
    }

    private static bool TryBuildActionStateJson(
        string templateJson,
        string? existingStateJson,
        IReadOnlyDictionary<string, object?> variables,
        out string stateJson,
        out string? error)
    {
        stateJson = "{}";
        string rendered = RenderStateTemplate(templateJson, variables, out error);
        if (error is not null)
        {
            return false;
        }

        JsonNode? patchNode;
        try
        {
            patchNode = JsonNode.Parse(rendered);
        }
        catch (JsonException ex)
        {
            error = $"Rule action state JSON is invalid after template rendering: {ex.Message}";
            return false;
        }

        if (patchNode is not JsonObject patchObject)
        {
            stateJson = rendered;
            return true;
        }

        JsonObject merged = TryParseJsonObject(existingStateJson) ?? new JsonObject();
        foreach (KeyValuePair<string, JsonNode?> property in patchObject)
        {
            merged[property.Key] = property.Value?.DeepClone();
        }

        stateJson = merged.ToJsonString();
        return true;
    }

    private static string RenderStateTemplate(
        string templateJson,
        IReadOnlyDictionary<string, object?> variables,
        out string? error)
    {
        string? renderError = null;
        string rendered = TemplatePlaceholderPattern.Replace(templateJson, match =>
        {
            string name = match.Groups["name"].Value;
            if (!TryGetVariableValue(name, variables, out object? value)
                && !TryGetVariableValue(NormalizeVariableSegment(name), variables, out value))
            {
                renderError = $"Variable '{name}' was not available for rule state template.";
                return "null";
            }

            return ToJsonLiteral(value);
        });
        error = renderError;
        return rendered;
    }

    private static JsonObject? TryParseJsonObject(string? stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(stateJson) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ToJsonLiteral(object? value)
        => value switch
        {
            null => "null",
            JsonElement element => element.GetRawText(),
            JsonNode node => node.ToJsonString(),
            string text => JsonSerializer.Serialize(text),
            bool boolean => boolean ? "true" : "false",
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            float number => number.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => JsonSerializer.Serialize(value)
        };

    private static bool TryGetVariableValue(
        string name,
        IReadOnlyDictionary<string, object?> variables,
        out object? value)
    {
        if (variables.TryGetValue(name, out value))
        {
            return true;
        }

        string normalized = NormalizeVariableSegment(name);
        return variables.TryGetValue(normalized, out value);
    }

    private static string CreateRuleExecutionId(
        AccountRuleDefinition rule,
        RuleTriggerContext trigger,
        AccountRefreshSnapshot snapshot)
    {
        string triggerId = trigger.BusinessObject?.ObjectId.Value
            ?? trigger.Transaction?.TxId.Value
            ?? "account-snapshot";
        return $"{rule.RuleId}:{triggerId}:{snapshot.RefreshedAtUtc.ToUnixTimeSeconds()}";
    }

    private static object? ParseVariableValue(string value)
    {
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal number))
        {
            return number;
        }

        if (bool.TryParse(value, out bool boolean))
        {
            return boolean;
        }

        return value;
    }

    private static IReadOnlyDictionary<string, object?> ReadObjectProperties(string? stateJson)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return values;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(stateJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = ReadJsonValue(property.Value);
            }
        }
        catch (JsonException)
        {
        }

        return values;
    }

    private static object? ReadJsonValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out decimal number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString(),
            _ => value.GetRawText()
        };

    private static bool IsProcessed(string? stateJson, string processedProperty)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(stateJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(processedProperty, out JsonElement processed))
            {
                return false;
            }

            return processed.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.String => bool.TryParse(processed.GetString(), out bool parsed) && parsed,
                JsonValueKind.Number => processed.TryGetDecimal(out decimal number) && number != 0m,
                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string SetProcessed(
        string? stateJson,
        string processedProperty,
        AccountRuleDefinition rule,
        string ruleExecutionId,
        DateTimeOffset processedAtUtc)
    {
        JsonObject obj;
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            obj = new JsonObject();
        }
        else
        {
            try
            {
                obj = JsonNode.Parse(stateJson) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                obj = new JsonObject();
            }
        }

        obj[processedProperty] = true;
        obj["processedAtUnix"] = processedAtUtc.ToUnixTimeSeconds();
        obj["processedByRuleId"] = rule.RuleId;
        obj["ruleExecutionId"] = ruleExecutionId;
        return obj.ToJsonString();
    }

    private static string NormalizeVariableSegment(string value)
    {
        var builder = new StringBuilder();
        foreach (char character in value.Trim())
        {
            if (char.IsUpper(character))
            {
                if (builder.Length > 0 && builder[^1] != '_')
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (builder.Length == 0 || builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        string normalized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "value" : normalized;
    }

    private static AccountRuleExecutionResult FromSubmitResult(
        AccountRuleDefinition rule,
        RuleTriggerContext trigger,
        AccountRuleActionKind actionKind,
        LedgerSubmitResult result,
        string successMessage)
        => result.Accepted
            ? WithTriggerTransaction(
                new AccountRuleExecutionResult(rule.RuleId, trigger.BusinessObject?.ObjectId, actionKind, AccountRuleExecutionStatus.Executed, result.TxId, successMessage),
                trigger)
            : Failed(rule, trigger, actionKind, string.Join("; ", result.Errors.Select(error => error.ToString())));

    private static AccountRuleExecutionResult Skipped(
        AccountRuleDefinition rule,
        RuleTriggerContext? trigger,
        AccountRuleActionKind actionKind,
        string message)
        => WithTriggerTransaction(
            new AccountRuleExecutionResult(rule.RuleId, trigger?.BusinessObject?.ObjectId, actionKind, AccountRuleExecutionStatus.Skipped, null, message),
            trigger);

    private static AccountRuleExecutionResult Failed(
        AccountRuleDefinition rule,
        RuleTriggerContext? trigger,
        AccountRuleActionKind actionKind,
        string message)
        => WithTriggerTransaction(
            new AccountRuleExecutionResult(rule.RuleId, trigger?.BusinessObject?.ObjectId, actionKind, AccountRuleExecutionStatus.Failed, null, message),
            trigger);

    private static AccountRuleExecutionResult WithTriggerTransaction(
        AccountRuleExecutionResult result,
        RuleTriggerContext? trigger)
        => trigger?.Transaction is null
            ? result
            : result with { TriggerTransactionId = trigger.Transaction.TxId };

    private sealed record DiscoveredRule(
        AccountRuleDefinition Definition,
        LedgerBusinessObjectHolding Holding);

    private sealed record RuleTriggerContext(
        LedgerBusinessObjectHolding? BusinessObject,
        LedgerTransactionSummary? Transaction)
    {
        public static RuleTriggerContext ForAccountSnapshot() => new(null, null);

        public static RuleTriggerContext ForBusinessObject(LedgerBusinessObjectHolding businessObject)
            => new(businessObject, null);

        public static RuleTriggerContext ForTransaction(LedgerTransactionSummary transaction)
            => new(null, transaction);
    }
}
