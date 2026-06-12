using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Ledger;

namespace CanDoItAll.Economy.BusinessObjects;

public sealed class InProcessLedgerGateway(
    LedgerExecutor ledgerExecutor,
    ITransactionHasher transactionHasher,
    ITimeProvider timeProvider) : ILedgerGateway, ILedgerReadClient
{
    private readonly object gate = new();
    private readonly Dictionary<OutPointKey, LedgerSpendableOutput> spendableOutputs = new();
    private readonly Dictionary<OutPointKey, ProjectedBusinessObjectHolding> businessObjectHoldings = new();
    private readonly Dictionary<string, LedgerState> ledgerStates = new(StringComparer.Ordinal);
    private readonly List<LedgerTransactionSummary> transactions = new();

    public async Task<LedgerSubmitResult> SubmitTransactionAsync(LedgerTransaction transaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LedgerSubmitResult result = await ledgerExecutor.SubmitAsync(transaction, cancellationToken).ConfigureAwait(false);
        if (!result.Accepted || result.TxId is null)
        {
            return result;
        }

        LedgerTransaction confirmed = transaction with
        {
            TxId = result.TxId,
            TxHash = transactionHasher.CalculateTxHash(transaction),
            Status = TransactionStatus.Confirmed
        };

        lock (gate)
        {
            ApplyConfirmedTransaction(
                confirmed,
                result.LedgerSequence ?? throw new InvalidOperationException("Accepted transaction must have a ledger sequence."));
        }

        return result;
    }

    public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<LedgerSpendableOutput> outputs = spendableOutputs.Values
                .Where(x => Matches(query, x))
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.OutPoint.TxId.Value, StringComparer.Ordinal)
                .ThenBy(x => x.OutPoint.OutputIndex)
                .ToArray();

            return Task.FromResult(outputs);
        }
    }

    public Task<LedgerAccountBalance> GetBalanceAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LedgerSpendableOutput[] outputs;
        lock (gate)
        {
            outputs = spendableOutputs.Values.Where(x => Matches(query, x)).ToArray();
        }

        return Task.FromResult(LedgerBalanceCalculator.Calculate(query, outputs, timeProvider.UtcNow));
    }

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<LedgerBusinessObjectHolding> holdings = businessObjectHoldings.Values
                .Where(x => x.Context == query.Context && x.Address == query.Address)
                .Select(x => x.Holding)
                .OrderBy(x => x.ObjectId.Value, StringComparer.Ordinal)
                .ThenByDescending(x => x.Version)
                .ToArray();

            return Task.FromResult(holdings);
        }
    }

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
        => GetTransactionsAsync(query, includeData: false, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        LedgerAddressQuery query,
        bool includeData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<LedgerTransactionSummary> result = transactions
                .Where(x => x.Context == query.Context && (x.InputAddresses.Contains(query.Address) || x.OutputAddresses.Contains(query.Address)))
                .OrderByDescending(x => x.Timestamp)
                .Select(x => includeData ? x : x with { Data = null })
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task<LedgerState?> GetLedgerStateAsync(
        LedgerAccountContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ledgerStates.TryGetValue(StateKey(context), out LedgerState? state);
            return Task.FromResult(state);
        }
    }

    public Task<LedgerTransactionSummary?> GetTransactionAsync(
        LedgerAccountContext context,
        TxId txId,
        bool includeData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            LedgerTransactionSummary? transaction = transactions
                .FirstOrDefault(x => x.Context == context && x.TxId == txId);

            return Task.FromResult(transaction is null || includeData ? transaction : transaction with { Data = null });
        }
    }

    public async Task<LedgerBusinessObjectHolding?> GetBusinessObjectAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<LedgerBusinessObjectHolding> holdings = await QueryBusinessObjectsAsync(query with { Take = 1 }, cancellationToken)
            .ConfigureAwait(false);
        return holdings.FirstOrDefault();
    }

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> QueryBusinessObjectsAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<LedgerBusinessObjectHolding> holdings = businessObjectHoldings.Values
                .Where(x => x.Context == query.Context)
                .Select(x => x.Holding)
                .Where(x => Matches(query, x))
                .OrderBy(x => x.ObjectType, StringComparer.Ordinal)
                .ThenBy(x => x.ObjectId.Value, StringComparer.Ordinal)
                .ThenByDescending(x => x.Version)
                .Skip(Math.Max(0, query.Skip))
                .Take(NormalizeTake(query.Take))
                .ToArray();

            return Task.FromResult(holdings);
        }
    }

    public Task<IReadOnlyList<LedgerTransactionSummary>> QueryTransactionsAsync(
        LedgerTransactionQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<LedgerTransactionSummary> result = transactions
                .Where(x => x.Context == query.Context)
                .Where(x => query.Address is null || x.InputAddresses.Contains(query.Address.Value) || x.OutputAddresses.Contains(query.Address.Value))
                .Where(x => query.BusinessObjectId is null || x.Outputs.Any(output => output.BusinessObject?.ObjectId == query.BusinessObjectId.Value))
                .Where(x => query.Kind is null || x.Kind == query.Kind.Value)
                .OrderByDescending(x => x.Timestamp)
                .ThenByDescending(x => x.TxId.Value, StringComparer.Ordinal)
                .Skip(Math.Max(0, query.Skip))
                .Take(NormalizeTake(query.Take))
                .Select(x => query.IncludeData ? x : x with { Data = null })
                .ToArray();

            return Task.FromResult(result);
        }
    }

    private void ApplyConfirmedTransaction(LedgerTransaction transaction, long ledgerSequence)
    {
        TxId txId = transaction.TxId ?? throw new InvalidOperationException("Confirmed transaction must have a transaction id.");
        var consumedHoldings = new Dictionary<BusinessObjectId, LedgerBusinessObjectHolding>();

        foreach (TransactionInput input in transaction.Inputs)
        {
            OutPointKey key = transactionHasher.CalculateOutPointKey(transaction.LedgerId, transaction.ScenarioId, input.PreviousOutPoint, input.Address);
            if (spendableOutputs.TryGetValue(key, out LedgerSpendableOutput? previousOutput)
                && previousOutput.BusinessObject is not null
                && businessObjectHoldings.TryGetValue(key, out ProjectedBusinessObjectHolding? previousHolding))
            {
                consumedHoldings[previousOutput.BusinessObject.ObjectId] = previousHolding.Holding;
            }

            spendableOutputs.Remove(key);
            businessObjectHoldings.Remove(key);
        }

        foreach (TransactionOutput output in transaction.Outputs)
        {
            var outPoint = new OutPoint(txId, output.OutputIndex);
            OutPointKey key = transactionHasher.CalculateOutPointKey(transaction.LedgerId, transaction.ScenarioId, outPoint, output.Address);
            spendableOutputs[key] = new LedgerSpendableOutput(
                new LedgerAccountContext(transaction.LedgerId, transaction.ScenarioId),
                key,
                outPoint,
                output.Address,
                output.MainAmount,
                output.Token,
                output.BusinessObject,
                transaction.Timestamp);

            if (output.BusinessObject is not null)
            {
                businessObjectHoldings[key] = new ProjectedBusinessObjectHolding(
                    new LedgerAccountContext(transaction.LedgerId, transaction.ScenarioId),
                    output.Address,
                    CreateBusinessObjectHolding(transaction, output, outPoint, consumedHoldings));
            }
        }

        transactions.Add(new LedgerTransactionSummary(
            new LedgerAccountContext(transaction.LedgerId, transaction.ScenarioId),
            txId,
            transaction.Kind,
            transaction.Status,
            transaction.Timestamp,
            transaction.Inputs.Select(x => x.Address).Distinct().ToArray(),
            transaction.Outputs.Select(x => x.Address).Distinct().ToArray(),
            transaction.Inputs.Select(input => ToInputSummary(transaction, input)).ToArray(),
            transaction.Outputs.Select(ToOutputSummary).ToArray(),
            transaction.Data));

        ledgerStates[StateKey(new LedgerAccountContext(transaction.LedgerId, transaction.ScenarioId))] = new LedgerState(
            transaction.LedgerId,
            transaction.ScenarioId,
            ledgerSequence,
            transaction.TxHash ?? new HashHex(new string('0', 64)),
            transaction.Timestamp);
    }

    private LedgerTransactionInputSummary ToInputSummary(LedgerTransaction transaction, TransactionInput input)
        => new(
            input.InputIndex,
            input.PreviousOutPoint,
            input.Address,
            transactionHasher.CalculateOutPointKey(transaction.LedgerId, transaction.ScenarioId, input.PreviousOutPoint, input.Address),
            input.SignatureBundle,
            input.SignaturePayloadHash);

    private static LedgerTransactionOutputSummary ToOutputSummary(TransactionOutput output)
        => new(
            output.OutputIndex,
            output.Address,
            output.MainAmount,
            output.Token,
            output.BusinessObject,
            output.MetadataHash);

    private static bool Matches(LedgerAddressQuery query, LedgerSpendableOutput output)
        => output.Context == query.Context && output.Address == query.Address;

    private static bool Matches(LedgerBusinessObjectQuery query, LedgerBusinessObjectHolding holding)
    {
        if (query.ObjectId is { } objectId && holding.ObjectId != objectId)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.ObjectType)
            && !string.Equals(holding.ObjectType, query.ObjectType, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.SchemaVersion)
            && !string.Equals(holding.SchemaVersion, query.SchemaVersion, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.OwnerAddress is { } ownerAddress && holding.OwnerAddress != ownerAddress)
        {
            return false;
        }

        if (query.StateProperties is { Count: > 0 }
            && !StatePropertiesMatch(holding.StateJson, query.StateProperties))
        {
            return false;
        }

        return true;
    }

    private static bool StatePropertiesMatch(string? stateJson, IReadOnlyDictionary<string, string> expected)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(stateJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach ((string key, string value) in expected)
            {
                if (!root.TryGetProperty(key, out JsonElement property)
                    || property.ValueKind != JsonValueKind.String
                    || !string.Equals(property.GetString(), value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int NormalizeTake(int take)
        => take <= 0 ? 100 : Math.Min(take, 500);

    private static string StateKey(LedgerAccountContext context)
        => $"{context.LedgerId.Value}|{context.ScenarioId?.Value ?? string.Empty}";

    private static LedgerBusinessObjectHolding CreateBusinessObjectHolding(
        LedgerTransaction transaction,
        TransactionOutput output,
        OutPoint outPoint,
        IReadOnlyDictionary<BusinessObjectId, LedgerBusinessObjectHolding> consumedHoldings)
    {
        BusinessObjectRef businessObject = output.BusinessObject
            ?? throw new InvalidOperationException("BusinessObject output is required.");
        consumedHoldings.TryGetValue(businessObject.ObjectId, out LedgerBusinessObjectHolding? previous);
        BusinessObjectPayloadEnvelope? envelope = TryReadPayloadEnvelope(transaction.Data?.InlineJson);

        return new LedgerBusinessObjectHolding(
            businessObject.ObjectId,
            businessObject.Version,
            output.Address,
            businessObject.Operation,
            businessObject.DataHash,
            outPoint,
            transaction.Timestamp,
            envelope?.ObjectType ?? previous?.ObjectType ?? "business-object",
            envelope?.SchemaVersion ?? previous?.SchemaVersion ?? "1.0",
            envelope?.StateJson ?? transaction.Data?.InlineJson ?? previous?.StateJson,
            transaction.Data?.ExternalRef ?? previous?.ExternalRef,
            transaction.Data?.ItemType ?? previous?.ItemType ?? TransactionDataItemType.BusinessObject);
    }

    private static BusinessObjectPayloadEnvelope? TryReadPayloadEnvelope(string? inlineJson)
    {
        if (string.IsNullOrWhiteSpace(inlineJson))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(inlineJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("objectType", out JsonElement objectType)
                || !root.TryGetProperty("schemaVersion", out JsonElement schemaVersion)
                || !root.TryGetProperty("stateJson", out JsonElement stateJson)
                || objectType.ValueKind != JsonValueKind.String
                || schemaVersion.ValueKind != JsonValueKind.String
                || stateJson.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? objectTypeValue = objectType.GetString();
            string? schemaVersionValue = schemaVersion.GetString();
            string? stateJsonValue = stateJson.GetString();
            if (string.IsNullOrWhiteSpace(objectTypeValue)
                || string.IsNullOrWhiteSpace(schemaVersionValue)
                || stateJsonValue is null)
            {
                return null;
            }

            return new BusinessObjectPayloadEnvelope(objectTypeValue, schemaVersionValue, stateJsonValue);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ProjectedBusinessObjectHolding(
        LedgerAccountContext Context,
        Address Address,
        LedgerBusinessObjectHolding Holding);
}
