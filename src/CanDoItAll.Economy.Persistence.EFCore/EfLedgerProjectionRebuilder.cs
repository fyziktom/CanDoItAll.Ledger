using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Economy.Persistence.EFCore;

public sealed class EfLedgerProjectionRebuilder(
    IDbContextFactory<EconomyDbContext> dbContextFactory,
    ITransactionHasher hasher)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EfLedgerProjectionRebuildResult> RebuildAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        IDbContextTransaction? transaction = null;
        if (!string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            EfLedgerProjectionRebuildResult result = await RebuildCoreAsync(db, ledgerId, scenarioId, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<EfLedgerProjectionRebuildResult> RebuildCoreAsync(
        EconomyDbContext db,
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        string? scenarioValue = scenarioId?.Value;
        List<LedgerTransactionRecord> transactions = await db.Transactions
            .AsNoTracking()
            .Where(x => x.LedgerId == ledgerId.Value
                && x.ScenarioId == scenarioValue
                && x.Status == (int)TransactionStatus.Confirmed
                && x.LedgerSequence.HasValue)
            .OrderBy(x => x.LedgerSequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string[] txIds = transactions.Select(x => x.TxId).ToArray();
        List<LedgerTransactionInputRecord> inputRows = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            txIds,
            batch => db.TransactionInputs.AsNoTracking().Where(x => batch.Contains(x.TxId)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, List<LedgerTransactionInputRecord>> inputsByTxId = inputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(row => row.InputIndex).ToList(),
                StringComparer.Ordinal);

        List<LedgerTransactionOutputRecord> outputRows = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            txIds,
            batch => db.TransactionOutputs.AsNoTracking().Where(x => batch.Contains(x.TxId)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, List<LedgerTransactionOutputRecord>> outputsByTxId = outputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(row => row.OutputIndex).ToList(),
                StringComparer.Ordinal);

        await DeleteDerivedRowsAsync(db, ledgerId, scenarioId, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var unspentOutputs = new Dictionary<string, UnspentOutputRecord>(StringComparer.Ordinal);
        var spentOutputs = new List<SpentOutputRecord>();
        var tokenTypes = new Dictionary<string, LedgerTokenTypeRecord>(StringComparer.Ordinal);
        var addressTouches = new Dictionary<string, AddressTouch>(StringComparer.Ordinal);
        var businessObjects = new Dictionary<string, BusinessObjectRecord>(StringComparer.Ordinal);
        var businessObjectVersions = new List<BusinessObjectVersionRecord>();
        var businessObjectVersionKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (LedgerTransactionRecord tx in transactions)
        {
            DateTimeOffset confirmedAt = tx.ConfirmedAt
                ?? throw new InvalidOperationException($"Confirmed transaction {tx.TxId} does not have a confirmation timestamp.");
            long ledgerSequence = tx.LedgerSequence
                ?? throw new InvalidOperationException($"Confirmed transaction {tx.TxId} does not have a ledger sequence.");
            var txId = new TxId(tx.TxId);
            var currentLedgerId = new LedgerId(tx.LedgerId);
            ScenarioId? currentScenarioId = ToScenarioId(tx.ScenarioId);
            var inputTokenIds = new HashSet<string>(StringComparer.Ordinal);
            var consumedBusinessObjectOutputs = new List<UnspentOutputRecord>();
            var createdOutputs = new List<UnspentOutputRecord>();

            foreach (LedgerTransactionInputRecord input in inputsByTxId.GetValueOrDefault(tx.TxId) ?? [])
            {
                string outPointKey = input.OutPointKey
                    ?? hasher.CalculateOutPointKey(
                        currentLedgerId,
                        currentScenarioId,
                        new OutPoint(new TxId(input.PreviousTxId), input.PreviousOutputIndex),
                        new Address(input.Address)).Value.Value;

                if (!unspentOutputs.TryGetValue(outPointKey, out UnspentOutputRecord? consumed))
                {
                    throw new InvalidOperationException($"Cannot rebuild spent output for unknown outpoint {input.PreviousTxId}:{input.PreviousOutputIndex}.");
                }

                if (!string.IsNullOrWhiteSpace(consumed.TokenId))
                {
                    inputTokenIds.Add(consumed.TokenId);
                }

                if (!string.IsNullOrWhiteSpace(consumed.BusinessObjectId))
                {
                    consumedBusinessObjectOutputs.Add(consumed);
                }

                unspentOutputs.Remove(outPointKey);
                spentOutputs.Add(new SpentOutputRecord
                {
                    OutPointKey = outPointKey,
                    LedgerId = tx.LedgerId,
                    ScenarioId = tx.ScenarioId,
                    TxId = input.PreviousTxId,
                    OutputIndex = input.PreviousOutputIndex,
                    Address = input.Address,
                    SpentInTxId = tx.TxId,
                    SpentAt = confirmedAt,
                    AdminSignatureJson = tx.AdminSignatureJson
                });
                Touch(addressTouches, input.Address, ledgerSequence, confirmedAt, tx.AdminSignatureJson);
            }

            foreach (LedgerTransactionOutputRecord output in outputsByTxId.GetValueOrDefault(tx.TxId) ?? [])
            {
                string outPointKey = hasher.CalculateOutPointKey(
                    currentLedgerId,
                    currentScenarioId,
                    new OutPoint(txId, output.OutputIndex),
                    new Address(output.Address)).Value.Value;

                var createdOutput = new UnspentOutputRecord
                {
                    OutPointKey = outPointKey,
                    LedgerId = tx.LedgerId,
                    ScenarioId = tx.ScenarioId,
                    TxId = tx.TxId,
                    OutputIndex = output.OutputIndex,
                    Address = output.Address,
                    MainAmount = output.MainAmount,
                    TokenId = output.TokenId,
                    TokenAmount = output.TokenAmount,
                    BusinessObjectId = output.BusinessObjectId,
                    BusinessObjectVersion = output.BusinessObjectVersion,
                    BusinessObjectOperation = output.BusinessObjectOperation,
                    BusinessObjectDataHash = output.BusinessObjectDataHash,
                    MetadataHash = output.MetadataHash,
                    CreatedAt = confirmedAt
                };
                unspentOutputs[outPointKey] = createdOutput;
                createdOutputs.Add(createdOutput);
                Touch(addressTouches, output.Address, ledgerSequence, confirmedAt, tx.AdminSignatureJson);
            }

            AddTokenTypeRows(tokenTypes, tx, outputsByTxId.GetValueOrDefault(tx.TxId) ?? [], inputTokenIds, confirmedAt);
            ApplyBusinessObjectProjections(
                businessObjects,
                businessObjectVersions,
                businessObjectVersionKeys,
                tx,
                consumedBusinessObjectOutputs,
                createdOutputs,
                confirmedAt);
        }

        db.UnspentOutputs.AddRange(unspentOutputs.Values);
        db.SpentOutputs.AddRange(spentOutputs);
        db.LedgerTokenTypes.AddRange(tokenTypes.Values);
        db.BusinessObjects.AddRange(businessObjects.Values);
        db.BusinessObjectVersions.AddRange(businessObjectVersions);
        AddLedgerState(db, ledgerId, scenarioId, transactions);
        AddAddressProjections(db, ledgerId, scenarioId, unspentOutputs.Values.ToArray(), addressTouches);

        LedgerTransactionRecord? last = transactions.LastOrDefault();
        return new EfLedgerProjectionRebuildResult(
            transactions.Count,
            unspentOutputs.Count,
            spentOutputs.Count,
            addressTouches.Count,
            tokenTypes.Count,
            businessObjects.Count,
            businessObjectVersions.Count,
            last?.LedgerSequence ?? 0,
            last?.LedgerEntryHash is null ? new HashHex(new string('0', 64)) : new HashHex(last.LedgerEntryHash));
    }

    private static async Task DeleteDerivedRowsAsync(
        EconomyDbContext db,
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        string? scenarioValue = scenarioId?.Value;

        db.UnspentOutputs.RemoveRange(await db.UnspentOutputs
            .Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
        db.SpentOutputs.RemoveRange(await db.SpentOutputs
            .Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
        db.AddressStateProjections.RemoveRange(await db.AddressStateProjections
            .Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
        db.LedgerTokenTypes.RemoveRange(await db.LedgerTokenTypes
            .Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
        db.BusinessObjects.RemoveRange(await db.BusinessObjects
            .Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
        db.BusinessObjectVersions.RemoveRange(await db.BusinessObjectVersions
            .Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
        db.LedgerStates.RemoveRange(await db.LedgerStates
            .Where(x => x.StateKey == StateKey(ledgerId, scenarioId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));
    }

    private static void AddLedgerState(
        EconomyDbContext db,
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        IReadOnlyList<LedgerTransactionRecord> transactions)
    {
        LedgerTransactionRecord? last = transactions.LastOrDefault();
        if (last?.LedgerSequence is null || string.IsNullOrWhiteSpace(last.LedgerEntryHash) || last.ConfirmedAt is null)
        {
            return;
        }

        db.LedgerStates.Add(new LedgerStateRecord
        {
            StateKey = StateKey(ledgerId, scenarioId),
            LedgerId = ledgerId.Value,
            ScenarioId = scenarioId?.Value,
            CurrentSequence = last.LedgerSequence.Value,
            CurrentLedgerHash = last.LedgerEntryHash,
            UpdatedAt = last.ConfirmedAt.Value
        });
    }

    private static void AddAddressProjections(
        EconomyDbContext db,
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        IReadOnlyList<UnspentOutputRecord> unspentOutputs,
        IReadOnlyDictionary<string, AddressTouch> addressTouches)
    {
        foreach ((string address, AddressTouch touch) in addressTouches.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            UnspentOutputRecord[] addressRows = unspentOutputs
                .Where(x => string.Equals(x.Address, address, StringComparison.Ordinal))
                .ToArray();
            EfProjectionBalance balance = EfLedgerProjectionCalculator.CalculateBalance(addressRows);
            string tokenBalancesJson = JsonSerializer.Serialize(balance.TokenBalances, JsonOptions);
            HashHex stateHash = EfLedgerProjectionCalculator.CalculateAddressProjectionHash(
                ledgerId,
                scenarioId,
                address,
                balance.MainBalance,
                balance.TokenBalances,
                balance.UtxoCount,
                touch.LedgerSequence);

            db.AddressStateProjections.Add(new AddressStateProjectionRecord
            {
                ProjectionKey = ProjectionKey(ledgerId, scenarioId, new Address(address)),
                LedgerId = ledgerId.Value,
                ScenarioId = scenarioId?.Value,
                Address = address,
                MainBalance = balance.MainBalance,
                TokenBalancesJson = tokenBalancesJson,
                UtxoCount = balance.UtxoCount,
                LastLedgerSequence = touch.LedgerSequence,
                StateHash = stateHash.Value,
                AdminSignatureJson = touch.AdminSignatureJson,
                UpdatedAt = touch.ConfirmedAt
            });
        }
    }

    private static void AddTokenTypeRows(
        Dictionary<string, LedgerTokenTypeRecord> tokenTypes,
        LedgerTransactionRecord tx,
        IReadOnlyList<LedgerTransactionOutputRecord> outputs,
        IReadOnlySet<string> inputTokenIds,
        DateTimeOffset confirmedAt)
    {
        if ((TransactionKind)tx.Kind is not (TransactionKind.Genesis or TransactionKind.Mint))
        {
            return;
        }

        TokenDefinitionEnvelope? tokenDefinition = string.IsNullOrWhiteSpace(tx.TokenDefinitionJson)
            ? null
            : JsonSerializer.Deserialize<TokenDefinitionEnvelope>(tx.TokenDefinitionJson, JsonOptions);

        foreach (LedgerTransactionOutputRecord output in outputs.Where(x => !string.IsNullOrWhiteSpace(x.TokenId)))
        {
            if (inputTokenIds.Contains(output.TokenId!))
            {
                continue;
            }

            string tokenTypeKey = TokenTypeKey(new LedgerId(tx.LedgerId), ToScenarioId(tx.ScenarioId), new TokenId(output.TokenId!));
            tokenTypes.TryAdd(tokenTypeKey, new LedgerTokenTypeRecord
            {
                TokenTypeKey = tokenTypeKey,
                LedgerId = tx.LedgerId,
                ScenarioId = tx.ScenarioId,
                TokenId = output.TokenId!,
                ProtocolVersion = tokenDefinition?.Definition.ProtocolVersion ?? 1,
                DefinitionHash = tokenDefinition is not null
                    && TokenDefinitionIdentity.IsV2TokenId(new TokenId(output.TokenId!))
                        ? output.TokenId![TokenDefinitionIdentity.V2TokenIdPrefix.Length..]
                        : null,
                TokenNamespace = tokenDefinition?.Definition.Namespace,
                Symbol = tokenDefinition?.Definition.Symbol,
                IssuerAddress = tokenDefinition?.Definition.Issuer.Value,
                InitialSupply = tokenDefinition?.Definition.InitialSupply.Value,
                DecimalPlaces = tokenDefinition?.Definition.DecimalPlaces,
                Classification = (int)(tokenDefinition?.Definition.Classification ?? TokenClassification.LegacyUnspecified),
                DisplayName = tokenDefinition?.DisplayMetadata?.DisplayName,
                Description = tokenDefinition?.DisplayMetadata?.Description,
                DisplayExternalRef = tokenDefinition?.DisplayMetadata?.ExternalRef,
                CreatedTxId = tx.TxId,
                CreatedOutputIndex = output.OutputIndex,
                CreatedAt = confirmedAt
            });
        }
    }

    private static void ApplyBusinessObjectProjections(
        Dictionary<string, BusinessObjectRecord> businessObjects,
        List<BusinessObjectVersionRecord> businessObjectVersions,
        HashSet<string> businessObjectVersionKeys,
        LedgerTransactionRecord tx,
        IReadOnlyList<UnspentOutputRecord> consumedOutputs,
        IReadOnlyList<UnspentOutputRecord> createdOutputs,
        DateTimeOffset confirmedAt)
    {
        UnspentOutputRecord[] createdBusinessObjects = createdOutputs
            .Where(x => !string.IsNullOrWhiteSpace(x.BusinessObjectId))
            .ToArray();
        UnspentOutputRecord[] consumedBusinessObjects = consumedOutputs
            .Where(x => !string.IsNullOrWhiteSpace(x.BusinessObjectId))
            .ToArray();

        if (createdBusinessObjects.Length == 0 && consumedBusinessObjects.Length == 0)
        {
            return;
        }

        HashSet<string> createdObjectIds = createdBusinessObjects
            .Select(x => x.BusinessObjectId!)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, UnspentOutputRecord> consumedCurrentByObjectId = consumedBusinessObjects
            .GroupBy(x => x.BusinessObjectId!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        foreach (UnspentOutputRecord consumed in consumedBusinessObjects)
        {
            string objectId = consumed.BusinessObjectId!;
            if (!createdObjectIds.Contains(objectId))
            {
                businessObjects.Remove(objectId);
            }
        }

        var txId = new TxId(tx.TxId);
        var ledgerId = new LedgerId(tx.LedgerId);
        ScenarioId? scenarioId = ToScenarioId(tx.ScenarioId);
        foreach (UnspentOutputRecord output in createdBusinessObjects)
        {
            string objectId = output.BusinessObjectId!;
            businessObjects.TryGetValue(objectId, out BusinessObjectRecord? previous);
            BusinessObjectOperation operation = (BusinessObjectOperation)(output.BusinessObjectOperation ?? 0);
            int version = output.BusinessObjectVersion ?? 0;
            consumedCurrentByObjectId.TryGetValue(objectId, out UnspentOutputRecord? consumedCurrent);
            ValidateBusinessObjectProjectionOperation(objectId, operation, version, previous, consumedCurrent, tx.TxId);

            BusinessObjectProjectionPayload payload = EfBusinessObjectProjectionMapper.ResolvePayload(
                tx.InlineJson,
                tx.DataHash,
                output,
                previous);
            BusinessObjectRecord next = EfBusinessObjectProjectionMapper.ToCurrentRecord(
                ledgerId,
                scenarioId,
                txId,
                output,
                payload,
                previous);

            if (previous is null)
            {
                businessObjects[objectId] = next;
            }
            else
            {
                EfBusinessObjectProjectionMapper.CopyCurrentRecord(previous, next);
            }

            string versionKey = objectId + "|" + (output.BusinessObjectVersion ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!businessObjectVersionKeys.Add(versionKey))
            {
                throw new InvalidOperationException($"Cannot rebuild transaction {tx.TxId}: BusinessObject {objectId} version {output.BusinessObjectVersion ?? 0} already exists.");
            }

            businessObjectVersions.Add(EfBusinessObjectProjectionMapper.ToVersionRecord(
                ledgerId,
                scenarioId,
                txId,
                output,
                payload,
                confirmedAt));
        }
    }

    private static void ValidateBusinessObjectProjectionOperation(
        string objectId,
        BusinessObjectOperation operation,
        int version,
        BusinessObjectRecord? previous,
        UnspentOutputRecord? consumedCurrent,
        string txId)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new InvalidOperationException($"Cannot rebuild transaction {txId}: BusinessObject {objectId} has an unknown operation value: {(int)operation}.");
        }

        switch (operation)
        {
            case BusinessObjectOperation.Create:
                if (previous is not null || consumedCurrent is not null)
                {
                    throw new InvalidOperationException($"Cannot rebuild transaction {txId}: BusinessObject {objectId} already exists. Create must not overwrite an existing current BusinessObject.");
                }

                if (version != 1)
                {
                    throw new InvalidOperationException($"Cannot rebuild transaction {txId}: BusinessObject {objectId} create must start at version 1.");
                }

                break;
            case BusinessObjectOperation.Update:
            case BusinessObjectOperation.Transfer:
                if (previous is null
                    || consumedCurrent is null
                    || !string.Equals(consumedCurrent.TxId, previous.CurrentOutPointTxId, StringComparison.Ordinal)
                    || consumedCurrent.OutputIndex != previous.CurrentOutPointIndex)
                {
                    throw new InvalidOperationException($"Cannot rebuild transaction {txId}: BusinessObject {objectId} {operation} must consume the current output for the same object id.");
                }

                int expectedVersion = previous.CurrentVersion + 1;
                if (version != expectedVersion)
                {
                    throw new InvalidOperationException($"Cannot rebuild transaction {txId}: BusinessObject {objectId} {operation} must advance from version {previous.CurrentVersion} to {expectedVersion}, not {version}.");
                }

                break;
            case BusinessObjectOperation.Close:
                throw new InvalidOperationException($"Cannot rebuild transaction {txId}: BusinessObject {objectId} close must consume the current output without emitting a next BusinessObject output.");
        }
    }

    private static void Touch(
        Dictionary<string, AddressTouch> addressTouches,
        string address,
        long ledgerSequence,
        DateTimeOffset confirmedAt,
        string? adminSignatureJson)
        => addressTouches[address] = new AddressTouch(ledgerSequence, confirmedAt, adminSignatureJson);

    private static ScenarioId? ToScenarioId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new ScenarioId(value);

    private static string StateKey(LedgerId ledgerId, ScenarioId? scenarioId)
        => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}";

    private static string ProjectionKey(LedgerId ledgerId, ScenarioId? scenarioId, Address address)
        => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}|{address.Value}";

    private static string TokenTypeKey(LedgerId ledgerId, ScenarioId? scenarioId, TokenId tokenId)
        => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}|{tokenId.Value}";

    private sealed record AddressTouch(long LedgerSequence, DateTimeOffset ConfirmedAt, string? AdminSignatureJson);
}

public sealed record EfLedgerProjectionRebuildResult(
    int TransactionCount,
    int UnspentOutputCount,
    int SpentOutputCount,
    int AddressProjectionCount,
    int TokenTypeCount,
    int BusinessObjectCount,
    int BusinessObjectVersionCount,
    long CurrentSequence,
    HashHex CurrentLedgerHash);
