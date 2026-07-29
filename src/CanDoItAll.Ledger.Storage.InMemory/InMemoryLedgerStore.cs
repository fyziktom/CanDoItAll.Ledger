using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Storage.InMemory;

public sealed class InMemoryLedgerStore : ILedgerConfirmationStore
{
    private readonly object gate = new();
    private readonly SemaphoreSlim confirmationGate = new(1, 1);
    private readonly ITransactionHasher? hasher;
    private readonly Dictionary<string, LedgerState> states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LedgerTransaction> transactions = new(StringComparer.Ordinal);
    private readonly Dictionary<OutPointKey, UnspentOutput> unspentOutputs = new();
    private readonly Dictionary<OutPointKey, SpentOutput> spentOutputs = new();
    private readonly Dictionary<string, RegisteredAddress> addresses = new(StringComparer.Ordinal);
    private readonly HashSet<string> tokenIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RegisteredTokenDefinition> tokenDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnspentOutput> currentBusinessObjects = new(StringComparer.Ordinal);
    private readonly HashSet<string> businessObjectVersionKeys = new(StringComparer.Ordinal);

    public InMemoryLedgerStore(ITransactionHasher? hasher = null)
    {
        this.hasher = hasher;
    }

    public Task<LedgerState?> GetLedgerStateAsync(LedgerId ledgerId, ScenarioId? scenarioId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            states.TryGetValue(StateKey(ledgerId, scenarioId), out var state);
            return Task.FromResult(state);
        }
    }

    public Task<RegisteredAddress?> GetRegisteredAddressAsync(LedgerId ledgerId, Address address, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            addresses.TryGetValue($"{ledgerId.Value}|{address.Value}", out var registeredAddress);
            return Task.FromResult(registeredAddress);
        }
    }

    public Task<LedgerTransaction?> GetTransactionAsync(LedgerId ledgerId, ScenarioId? scenarioId, TxId txId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            transactions.TryGetValue(TxKey(ledgerId, scenarioId, txId), out var tx);
            return Task.FromResult(tx);
        }
    }

    public Task<IReadOnlyDictionary<OutPointKey, UnspentOutput>> GetUnspentOutputsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        IReadOnlyList<OutPointKey> outPointKeys,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            IReadOnlyDictionary<OutPointKey, UnspentOutput> result = outPointKeys
                .Where(unspentOutputs.ContainsKey)
                .ToDictionary(x => x, x => unspentOutputs[x]);

            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyDictionary<OutPointKey, SpentOutput>> GetSpentOutputsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        IReadOnlyList<OutPointKey> outPointKeys,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            IReadOnlyDictionary<OutPointKey, SpentOutput> result = outPointKeys
                .Where(spentOutputs.ContainsKey)
                .ToDictionary(x => x, x => spentOutputs[x]);

            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<UnspentOutput>> GetUnspentOutputsByAddressAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        Address address,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            IReadOnlyList<UnspentOutput> result = unspentOutputs.Values
                .Where(x => x.LedgerId == ledgerId && x.ScenarioId == scenarioId && x.Address == address)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.OutPoint.TxId.Value, StringComparer.Ordinal)
                .ThenBy(x => x.OutPoint.OutputIndex)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task<bool> TokenExistsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        TokenId tokenId,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            return Task.FromResult(tokenIds.Contains(TokenKey(ledgerId, scenarioId, tokenId)));
        }
    }

    public async Task<LedgerSubmitResult> ConfirmTransactionAsync(
        LedgerTransaction normalizedTransaction,
        IReadOnlyList<OutPointKey> inputKeys,
        Func<LedgerConfirmationContext, CancellationToken, Task<LedgerConfirmationDecision>> confirm,
        CancellationToken cancellationToken)
    {
        await confirmationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LedgerConfirmationContext context;
            lock (gate)
            {
                TxId txId = normalizedTransaction.TxId
                    ?? throw new InvalidOperationException("Normalized transaction must have a transaction id.");
                states.TryGetValue(
                    StateKey(normalizedTransaction.LedgerId, normalizedTransaction.ScenarioId),
                    out LedgerState? state);
                transactions.TryGetValue(
                    TxKey(normalizedTransaction.LedgerId, normalizedTransaction.ScenarioId, txId),
                    out LedgerTransaction? existing);
                IReadOnlyDictionary<OutPointKey, UnspentOutput> unspent = inputKeys
                    .Where(unspentOutputs.ContainsKey)
                    .ToDictionary(key => key, key => unspentOutputs[key]);
                IReadOnlyDictionary<OutPointKey, SpentOutput> spent = inputKeys
                    .Where(spentOutputs.ContainsKey)
                    .ToDictionary(key => key, key => spentOutputs[key]);
                context = new LedgerConfirmationContext(state, existing, unspent, spent);
            }

            LedgerConfirmationDecision decision = await confirm(context, cancellationToken).ConfigureAwait(false);
            if (!decision.Accepted)
            {
                return LedgerSubmitResult.Rejected(decision.Errors);
            }

            ConfirmedTransaction confirmed = decision.ConfirmedTransaction!;
            try
            {
                AppendConfirmedTransactionCore(confirmed);
            }
            catch (LedgerStoreConflictException ex)
            {
                return LedgerSubmitResult.Rejected(new[] { new ValidationError(ex.Code, ex.Message) });
            }

            return LedgerSubmitResult.Confirmed(
                confirmed.Transaction.TxId!.Value,
                confirmed.LedgerSequence);
        }
        finally
        {
            confirmationGate.Release();
        }
    }

    public Task<RegisteredTokenDefinition?> GetTokenDefinitionAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        TokenId tokenId,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            tokenDefinitions.TryGetValue(TokenKey(ledgerId, scenarioId, tokenId), out RegisteredTokenDefinition? definition);
            return Task.FromResult(definition);
        }
    }

    public async Task AppendConfirmedTransactionAsync(
        ConfirmedTransaction confirmedTransaction,
        CancellationToken cancellationToken)
    {
        await confirmationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AppendConfirmedTransactionCore(confirmedTransaction);
        }
        finally
        {
            confirmationGate.Release();
        }
    }

    private void AppendConfirmedTransactionCore(ConfirmedTransaction confirmedTransaction)
    {
        lock (gate)
        {
            var tx = confirmedTransaction.Transaction;
            TxId txId = tx.TxId ?? throw new InvalidOperationException("Confirmed transaction must have a transaction id.");
            string txKey = TxKey(tx.LedgerId, tx.ScenarioId, txId);
            string stateKey = StateKey(tx.LedgerId, tx.ScenarioId);

            if (transactions.ContainsKey(txKey))
            {
                throw new LedgerStoreConflictException(
                    ValidationErrorCodes.DuplicateTransaction,
                    $"Transaction already exists: {txId.Value}.");
            }

            states.TryGetValue(stateKey, out LedgerState? state);
            ValidateLedgerStateExpectation(state, confirmedTransaction);

            var consumedOutputs = new List<(OutPointKey Key, TransactionInput Input, UnspentOutput Output)>();
            foreach (TransactionInput input in tx.Inputs)
            {
                OutPointKey key = CalculateOutPointKey(tx.LedgerId, tx.ScenarioId, input.PreviousOutPoint, input.Address);
                if (!unspentOutputs.TryGetValue(key, out UnspentOutput? previous))
                {
                    throw new LedgerStoreConflictException(
                        ValidationErrorCodes.UnknownInput,
                        $"Cannot spend unknown output {input.PreviousOutPoint} for {input.Address}.");
                }

                consumedOutputs.Add((key, input, previous));
            }

            ValidateBusinessObjectProjectionState(tx, consumedOutputs.Select(x => x.Output).ToArray());
            HashSet<TokenId> inputTokenIds = consumedOutputs
                .Where(item => item.Output.Token is not null)
                .Select(item => item.Output.Token!.TokenId)
                .ToHashSet();

            foreach ((OutPointKey key, TransactionInput input, UnspentOutput previous) in consumedOutputs)
            {
                unspentOutputs.Remove(key);
                if (previous.BusinessObject is not null)
                {
                    string objectKey = BusinessObjectKey(previous.LedgerId, previous.ScenarioId, previous.BusinessObject.LineageKey);
                    if (currentBusinessObjects.TryGetValue(objectKey, out UnspentOutput? current)
                        && current.OutPoint == previous.OutPoint)
                    {
                        currentBusinessObjects.Remove(objectKey);
                    }
                }

                spentOutputs[key] = new SpentOutput(
                    key,
                    tx.LedgerId,
                    tx.ScenarioId,
                    input.PreviousOutPoint,
                    input.Address,
                    txId,
                    confirmedTransaction.ConfirmedAt,
                    confirmedTransaction.AdminSignature);
            }

            foreach (TransactionOutput output in tx.Outputs)
            {
                var outPoint = new OutPoint(txId, output.OutputIndex);
                OutPointKey key = CalculateOutPointKey(tx.LedgerId, tx.ScenarioId, outPoint, output.Address);
                var createdOutput = new UnspentOutput(
                    key,
                    tx.LedgerId,
                    tx.ScenarioId,
                    outPoint,
                    output.Address,
                    output.MainAmount,
                    output.Token,
                    output.BusinessObject,
                    confirmedTransaction.ConfirmedAt);
                unspentOutputs[key] = createdOutput;

                if (output.Token is not null)
                {
                    string tokenKey = TokenKey(tx.LedgerId, tx.ScenarioId, output.Token.TokenId);
                    tokenIds.Add(tokenKey);
                    if (tx.Kind is TransactionKind.Genesis or TransactionKind.Mint
                        && !inputTokenIds.Contains(output.Token.TokenId)
                        && !tokenDefinitions.ContainsKey(tokenKey))
                    {
                        tokenDefinitions[tokenKey] = CreateRegisteredTokenDefinition(
                            tx,
                            output,
                            confirmedTransaction.ConfirmedAt);
                    }
                }

                if (output.BusinessObject is not null)
                {
                    currentBusinessObjects[BusinessObjectKey(tx.LedgerId, tx.ScenarioId, output.BusinessObject.LineageKey)] = createdOutput;
                    businessObjectVersionKeys.Add(BusinessObjectVersionKey(tx.LedgerId, tx.ScenarioId, output.BusinessObject.LineageKey, output.BusinessObject.Version));
                }
            }

            transactions[txKey] = tx;
            states[stateKey] = new LedgerState(tx.LedgerId, tx.ScenarioId, confirmedTransaction.LedgerSequence, confirmedTransaction.LedgerEntryHash, confirmedTransaction.ConfirmedAt);
        }

    }

    private void ValidateBusinessObjectProjectionState(LedgerTransaction tx, IReadOnlyList<UnspentOutput> consumedOutputs)
    {
        var seenVersionKeys = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<BusinessObjectLineageKey, UnspentOutput> consumedBusinessObjects = consumedOutputs
            .Where(x => x.BusinessObject is not null)
            .GroupBy(x => x.BusinessObject!.LineageKey)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (TransactionOutput output in tx.Outputs.Where(x => x.BusinessObject is not null))
        {
            BusinessObjectRef businessObject = output.BusinessObject!;
            if (!Enum.IsDefined(businessObject.Operation))
            {
                throw new LedgerStoreConflictException(
                    ValidationErrorCodes.InvalidBusinessObjectTransition,
                    $"BusinessObject lineage {businessObject.LineageKey.Value} has an unknown operation value: {(int)businessObject.Operation}.");
            }

            string objectKey = BusinessObjectKey(tx.LedgerId, tx.ScenarioId, businessObject.LineageKey);
            currentBusinessObjects.TryGetValue(objectKey, out UnspentOutput? current);
            consumedBusinessObjects.TryGetValue(businessObject.LineageKey, out UnspentOutput? consumedCurrent);
            switch (businessObject.Operation)
            {
                case BusinessObjectOperation.Create:
                    if (current is not null || consumedCurrent is not null)
                    {
                        throw new LedgerStoreConflictException(
                            ValidationErrorCodes.InvalidBusinessObjectTransition,
                            $"BusinessObject lineage {businessObject.LineageKey.Value} already exists. Create must not overwrite an existing current BusinessObject lineage.");
                    }

                    if (businessObject.Version != 1)
                    {
                        throw new LedgerStoreConflictException(
                            ValidationErrorCodes.InvalidBusinessObjectTransition,
                            $"BusinessObject lineage {businessObject.LineageKey.Value} create must start at version 1.");
                    }

                    break;
                case BusinessObjectOperation.Update:
                case BusinessObjectOperation.Transfer:
                    if (current?.BusinessObject is null || consumedCurrent is null || consumedCurrent.OutPoint != current.OutPoint)
                    {
                        throw new LedgerStoreConflictException(
                            ValidationErrorCodes.InvalidBusinessObjectTransition,
                            $"BusinessObject lineage {businessObject.LineageKey.Value} {businessObject.Operation} must consume the current output for the same lineage key.");
                    }

                    int expectedVersion = current.BusinessObject.Version + 1;
                    if (businessObject.Version != expectedVersion)
                    {
                        throw new LedgerStoreConflictException(
                            ValidationErrorCodes.InvalidBusinessObjectTransition,
                            $"BusinessObject lineage {businessObject.LineageKey.Value} {businessObject.Operation} must advance from version {current.BusinessObject.Version} to {expectedVersion}, not {businessObject.Version}.");
                    }

                    break;
                case BusinessObjectOperation.Close:
                    throw new LedgerStoreConflictException(
                        ValidationErrorCodes.InvalidBusinessObjectTransition,
                        $"BusinessObject lineage {businessObject.LineageKey.Value} close must consume the current output without emitting a next BusinessObject output.");
            }

            string versionKey = BusinessObjectVersionKey(tx.LedgerId, tx.ScenarioId, businessObject.LineageKey, businessObject.Version);
            if (businessObjectVersionKeys.Contains(versionKey) || !seenVersionKeys.Add(versionKey))
            {
                throw new LedgerStoreConflictException(
                    ValidationErrorCodes.InvalidBusinessObjectTransition,
                    $"BusinessObject lineage {businessObject.LineageKey.Value} version {businessObject.Version} already exists.");
            }
        }
    }

    private static void ValidateLedgerStateExpectation(LedgerState? state, ConfirmedTransaction confirmedTransaction)
    {
        long expectedPreviousSequence = confirmedTransaction.LedgerSequence - 1;
        if (expectedPreviousSequence < 0)
        {
            throw new LedgerStoreConflictException(
                ValidationErrorCodes.LedgerStateConflict,
                "Confirmed ledger sequence must be positive.");
        }

        if (state is null)
        {
            if (expectedPreviousSequence != 0)
            {
                throw new LedgerStoreConflictException(
                    ValidationErrorCodes.LedgerStateConflict,
                    "Ledger state changed during confirmation.");
            }

            return;
        }

        if (state.CurrentSequence != expectedPreviousSequence
            || !string.Equals(state.CurrentLedgerHash.Value, confirmedTransaction.PreviousLedgerHash.Value, StringComparison.Ordinal))
        {
            throw new LedgerStoreConflictException(
                ValidationErrorCodes.LedgerStateConflict,
                "Ledger state changed during confirmation.");
        }
    }

    private OutPointKey CalculateOutPointKey(LedgerId ledgerId, ScenarioId? scenarioId, OutPoint outPoint, Address address)
    {
        if (hasher is not null)
        {
            return hasher.CalculateOutPointKey(ledgerId, scenarioId, outPoint, address);
        }

        string value = string.Join('|',
            "cdia.economy.outpoint.v1",
            ledgerId.Value,
            scenarioId?.Value ?? string.Empty,
            outPoint.TxId.Value,
            outPoint.OutputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            address.Value);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new OutPointKey(new HashHex(Convert.ToHexString(hash).ToLowerInvariant()));
    }

    private static string StateKey(LedgerId ledgerId, ScenarioId? scenarioId) => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}";

    private static string TxKey(LedgerId ledgerId, ScenarioId? scenarioId, TxId txId) => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}|{txId.Value}";

    private static string TokenKey(LedgerId ledgerId, ScenarioId? scenarioId, TokenId tokenId) => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}|{tokenId.Value}";

    private static string BusinessObjectKey(LedgerId ledgerId, ScenarioId? scenarioId, BusinessObjectLineageKey lineageKey) => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}|{lineageKey.Value}";

    private static string BusinessObjectVersionKey(LedgerId ledgerId, ScenarioId? scenarioId, BusinessObjectLineageKey lineageKey, int version) => $"{BusinessObjectKey(ledgerId, scenarioId, lineageKey)}|{version.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    private static RegisteredTokenDefinition CreateRegisteredTokenDefinition(
        LedgerTransaction transaction,
        TransactionOutput output,
        DateTimeOffset createdAt)
    {
        TokenTransfer token = output.Token!;
        TokenDefinitionEnvelope? envelope = transaction.TokenDefinition;
        if (envelope is not null && TokenDefinitionIdentity.IsV2TokenId(token.TokenId))
        {
            return new RegisteredTokenDefinition(
                token.TokenId,
                envelope.Definition.ProtocolVersion,
                envelope.Definition.Namespace,
                envelope.Definition.Symbol,
                envelope.Definition.Issuer,
                envelope.Definition.InitialSupply,
                envelope.Definition.DecimalPlaces,
                envelope.Definition.Classification,
                new HashHex(token.TokenId.Value[TokenDefinitionIdentity.V2TokenIdPrefix.Length..]),
                envelope.DisplayMetadata,
                transaction.TxId!.Value,
                output.OutputIndex,
                createdAt);
        }

        return new RegisteredTokenDefinition(
            token.TokenId,
            1,
            null,
            null,
            null,
            null,
            null,
            TokenClassification.LegacyUnspecified,
            null,
            null,
            transaction.TxId!.Value,
            output.OutputIndex,
            createdAt);
    }
}
