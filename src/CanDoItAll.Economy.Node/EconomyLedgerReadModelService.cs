using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Node;

public sealed class EconomyLedgerReadModelService(IDbContextFactory<EconomyDbContext> dbContextFactory)
{
    private static readonly LedgerId DefaultLedgerId = new("default");

    public async Task<LedgerState?> GetLedgerStateAsync(
        LedgerAccountContext context,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string? scenarioValue = context.ScenarioId?.Value;
        LedgerStateRecord? row = await db.LedgerStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LedgerId == context.LedgerId.Value && x.ScenarioId == scenarioValue, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new LedgerState(
                new LedgerId(row.LedgerId),
                ToScenarioId(row.ScenarioId),
                row.CurrentSequence,
                new HashHex(row.CurrentLedgerHash),
                row.UpdatedAt);
    }

    public async Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UnspentOutputRecord> rows = await GetUnspentOutputRowsAsync(query, cancellationToken).ConfigureAwait(false);
        return rows.Select(row => EconomyNodeRecordMapper.ToSpendableOutput(query.Context, row)).ToArray();
    }

    public async Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectHoldingsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string? scenarioValue = query.Context.ScenarioId?.Value;
        List<UnspentOutputRecord> rows = await db.UnspentOutputs
            .AsNoTracking()
            .Where(x => x.LedgerId == query.Context.LedgerId.Value
                && x.ScenarioId == scenarioValue
                && x.Address == query.Address.Value
                && x.BusinessObjectId != null
                && x.BusinessObjectId != string.Empty)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.TxId)
            .ThenBy(x => x.OutputIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return Array.Empty<LedgerBusinessObjectHolding>();
        }

        string[] txIds = rows.Select(x => x.TxId).Distinct(StringComparer.Ordinal).ToArray();
        List<LedgerTransactionRecord> transactionRows = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            txIds,
            batch => db.Transactions.AsNoTracking().Where(x => batch.Contains(x.TxId)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, LedgerTransactionRecord> transactions = transactionRows
            .ToDictionary(x => x.TxId, StringComparer.Ordinal);
        string[] objectIds = rows
            .Select(x => x.BusinessObjectId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        List<BusinessObjectPayloadProjection> payloads = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            objectIds,
            batch => db.TransactionOutputs
                .AsNoTracking()
                .Join(
                    db.Transactions.AsNoTracking(),
                    output => output.TxId,
                    transaction => transaction.TxId,
                    (output, transaction) => new { output, transaction })
                .Where(row => row.transaction.LedgerId == query.Context.LedgerId.Value
                    && row.transaction.ScenarioId == scenarioValue
                    && row.output.BusinessObjectId != null
                    && batch.Contains(row.output.BusinessObjectId)
                    && row.transaction.InlineJson != null)
                .Select(row => new BusinessObjectPayloadProjection(
                    row.output.BusinessObjectId!,
                    row.output.BusinessObjectVersion ?? 0,
                    row.transaction.InlineJson!,
                    row.transaction.ExternalRef,
                    row.transaction.DataItemType)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, BusinessObjectPayloadProjection[]> payloadsByObjectId = payloads
            .GroupBy(x => x.ObjectId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(x => x.Version).ToArray(),
                StringComparer.Ordinal);
        List<BusinessObjectRecord> currentRows = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            objectIds,
            batch => db.BusinessObjects.AsNoTracking().Where(x => x.LedgerId == query.Context.LedgerId.Value
                && x.ScenarioId == scenarioValue
                && batch.Contains(x.ObjectId)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, BusinessObjectRecord> currentByObjectId = currentRows
            .ToDictionary(x => x.ObjectId, StringComparer.Ordinal);

        return rows
            .Select(row =>
            {
                transactions.TryGetValue(row.TxId, out LedgerTransactionRecord? transaction);
                BusinessObjectRecord? currentProjection = null;
                if (row.BusinessObjectId is not null)
                {
                    currentByObjectId.TryGetValue(row.BusinessObjectId, out currentProjection);
                }

                BusinessObjectPayloadProjection? fallbackPayload = null;
                if (row.BusinessObjectId is not null
                    && payloadsByObjectId.TryGetValue(row.BusinessObjectId, out BusinessObjectPayloadProjection[]? objectPayloads))
                {
                    fallbackPayload = objectPayloads.FirstOrDefault(x => x.Version <= (row.BusinessObjectVersion ?? 0));
                }

                return EconomyNodeRecordMapper.ToBusinessObjectHolding(row, transaction, fallbackPayload, currentProjection);
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<LedgerTransactionSummary>> GetLedgerTransactionsAsync(
        LedgerAddressQuery query,
        bool includeData,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string? scenarioValue = query.Context.ScenarioId?.Value;
        string ledgerId = query.Context.LedgerId.Value;
        string address = query.Address.Value;

        string[] outputTxIds = await db.TransactionOutputs
            .AsNoTracking()
            .Join(
                db.Transactions.AsNoTracking(),
                output => output.TxId,
                transaction => transaction.TxId,
                (output, transaction) => new { output, transaction })
            .Where(row => row.transaction.LedgerId == ledgerId
                && row.transaction.ScenarioId == scenarioValue
                && row.output.Address == address)
            .Select(row => row.transaction.TxId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        string[] inputTxIds = await db.TransactionInputs
            .AsNoTracking()
            .Join(
                db.Transactions.AsNoTracking(),
                input => input.TxId,
                transaction => transaction.TxId,
                (input, transaction) => new { input, transaction })
            .Where(row => row.transaction.LedgerId == ledgerId
                && row.transaction.ScenarioId == scenarioValue
                && row.input.Address == address)
            .Select(row => row.transaction.TxId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        string[] txIds = outputTxIds.Concat(inputTxIds).Distinct(StringComparer.Ordinal).ToArray();
        if (txIds.Length == 0)
        {
            return Array.Empty<LedgerTransactionSummary>();
        }

        List<LedgerTransactionRecord> transactions = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            txIds,
            batch => db.Transactions.AsNoTracking()
                .Where(x => x.LedgerId == ledgerId && x.ScenarioId == scenarioValue && batch.Contains(x.TxId)),
            cancellationToken).ConfigureAwait(false);
        transactions = transactions
            .OrderByDescending(x => x.LedgerSequence)
            .ThenByDescending(x => x.Timestamp)
            .ToList();

        List<LedgerTransactionInputRecord> inputRows = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            txIds,
            batch => db.TransactionInputs.AsNoTracking().Where(x => batch.Contains(x.TxId)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, Address[]> inputAddresses = inputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => new Address(x.Address)).Distinct().ToArray(),
                StringComparer.Ordinal);
        Dictionary<string, LedgerTransactionInputSummary[]> inputsByTxId = inputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(x => x.InputIndex)
                    .Select(EconomyNodeRecordMapper.ToLedgerTransactionInputSummary)
                    .ToArray(),
                StringComparer.Ordinal);

        List<LedgerTransactionOutputRecord> outputRows = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            txIds,
            batch => db.TransactionOutputs.AsNoTracking().Where(x => batch.Contains(x.TxId)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, Address[]> outputAddresses = outputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => new Address(x.Address)).Distinct().ToArray(),
                StringComparer.Ordinal);
        Dictionary<string, LedgerTransactionOutputSummary[]> outputsByTxId = outputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(x => x.OutputIndex)
                    .Select(EconomyNodeRecordMapper.ToLedgerTransactionOutputSummary)
                    .ToArray(),
                StringComparer.Ordinal);

        return transactions
            .Select(row =>
            {
                inputAddresses.TryGetValue(row.TxId, out Address[]? inputs);
                outputAddresses.TryGetValue(row.TxId, out Address[]? outputs);
                inputsByTxId.TryGetValue(row.TxId, out LedgerTransactionInputSummary[]? inputDetails);
                outputsByTxId.TryGetValue(row.TxId, out LedgerTransactionOutputSummary[]? outputDetails);
                return new LedgerTransactionSummary(
                    query.Context,
                    new TxId(row.TxId),
                    (TransactionKind)row.Kind,
                    (TransactionStatus)row.Status,
                    row.ConfirmedAt ?? row.Timestamp,
                    inputs ?? Array.Empty<Address>(),
                    outputs ?? Array.Empty<Address>(),
                    inputDetails ?? Array.Empty<LedgerTransactionInputSummary>(),
                    outputDetails ?? Array.Empty<LedgerTransactionOutputSummary>(),
                    includeData ? EconomyNodeRecordMapper.ToTransactionData(row) : null);
            })
            .ToArray();
    }

    public async Task<LedgerTransactionSummary?> GetLedgerTransactionAsync(
        LedgerAccountContext context,
        TxId txId,
        bool includeData,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string? scenarioValue = context.ScenarioId?.Value;
        LedgerTransactionRecord? row = await db.Transactions
            .AsNoTracking()
            .Where(x => x.LedgerId == context.LedgerId.Value
                && x.ScenarioId == scenarioValue
                && x.TxId == txId.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        List<LedgerTransactionInputRecord> inputs = await db.TransactionInputs
            .AsNoTracking()
            .Where(x => x.TxId == txId.Value)
            .OrderBy(x => x.InputIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<LedgerTransactionOutputRecord> outputs = await db.TransactionOutputs
            .AsNoTracking()
            .Where(x => x.TxId == txId.Value)
            .OrderBy(x => x.OutputIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new LedgerTransactionSummary(
            context,
            new TxId(row.TxId),
            (TransactionKind)row.Kind,
            (TransactionStatus)row.Status,
            row.ConfirmedAt ?? row.Timestamp,
            inputs.Select(x => new Address(x.Address)).Distinct().ToArray(),
            outputs.Select(x => new Address(x.Address)).Distinct().ToArray(),
            inputs.Select(EconomyNodeRecordMapper.ToLedgerTransactionInputSummary).ToArray(),
            outputs.Select(EconomyNodeRecordMapper.ToLedgerTransactionOutputSummary).ToArray(),
            includeData ? EconomyNodeRecordMapper.ToTransactionData(row) : null);
    }

    public async Task<LedgerBusinessObjectHolding?> GetBusinessObjectHoldingAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerBusinessObjectHolding> holdings = await QueryBusinessObjectHoldingsAsync(
            query with { Take = 1 },
            cancellationToken).ConfigureAwait(false);

        return holdings.FirstOrDefault();
    }

    public async Task<IReadOnlyList<LedgerBusinessObjectHolding>> QueryBusinessObjectHoldingsAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string? scenarioValue = query.Context.ScenarioId?.Value;
        IQueryable<BusinessObjectRecord> current = db.BusinessObjects
            .AsNoTracking()
            .Where(x => x.LedgerId == query.Context.LedgerId.Value && x.ScenarioId == scenarioValue);

        if (query.ObjectId is { } objectId)
        {
            current = current.Where(x => x.ObjectId == objectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ObjectType))
        {
            current = current.Where(x => x.ObjectType == query.ObjectType);
        }

        if (query.OwnerAddress is { } ownerAddress)
        {
            current = current.Where(x => x.CurrentOwnerAddress == ownerAddress.Value);
        }

        List<BusinessObjectRecord> currentRows = await current
            .OrderBy(x => x.ObjectType)
            .ThenBy(x => x.ObjectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (currentRows.Count == 0)
        {
            return Array.Empty<LedgerBusinessObjectHolding>();
        }

        string[] objectIds = currentRows.Select(x => x.ObjectId).ToArray();
        List<BusinessObjectVersionRecord> versionRows = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            objectIds,
            batch => db.BusinessObjectVersions
                .AsNoTracking()
                .Where(x => x.LedgerId == query.Context.LedgerId.Value
                    && x.ScenarioId == scenarioValue
                    && batch.Contains(x.ObjectId)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, BusinessObjectVersionRecord> currentVersions = versionRows
            .GroupBy(x => x.ObjectId, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(row => row.Version).First(),
                StringComparer.Ordinal);

        IEnumerable<LedgerBusinessObjectHolding> mapped = currentRows
            .Select(row => ToBusinessObjectHolding(row, currentVersions.GetValueOrDefault(row.ObjectId)))
            .Where(x => string.IsNullOrWhiteSpace(query.SchemaVersion)
                || string.Equals(x.SchemaVersion, query.SchemaVersion, StringComparison.Ordinal))
            .Where(x => query.StateProperties is not { Count: > 0 }
                || StatePropertiesMatch(x.StateJson, query.StateProperties))
            .Skip(Math.Max(0, query.Skip))
            .Take(NormalizeTake(query.Take));

        return mapped.ToArray();
    }

    public async Task<IReadOnlyList<LedgerTransactionSummary>> QueryLedgerTransactionsAsync(
        LedgerTransactionQuery query,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string? scenarioValue = query.Context.ScenarioId?.Value;
        string ledgerId = query.Context.LedgerId.Value;
        IQueryable<LedgerTransactionRecord> txQuery = db.Transactions
            .AsNoTracking()
            .Where(x => x.LedgerId == ledgerId && x.ScenarioId == scenarioValue);

        if (query.Kind is { } kind)
        {
            int kindValue = (int)kind;
            txQuery = txQuery.Where(x => x.Kind == kindValue);
        }

        if (query.Address is { } address)
        {
            string addressValue = address.Value;
            IQueryable<string> inputTxIds = db.TransactionInputs
                .AsNoTracking()
                .Where(x => x.Address == addressValue)
                .Select(x => x.TxId);
            IQueryable<string> outputTxIds = db.TransactionOutputs
                .AsNoTracking()
                .Where(x => x.Address == addressValue)
                .Select(x => x.TxId);
            txQuery = txQuery.Where(x => inputTxIds.Contains(x.TxId) || outputTxIds.Contains(x.TxId));
        }

        if (query.BusinessObjectId is { } businessObjectId)
        {
            string value = businessObjectId.Value;
            IQueryable<string> objectTxIds = db.TransactionOutputs
                .AsNoTracking()
                .Where(x => x.BusinessObjectId == value)
                .Select(x => x.TxId);
            txQuery = txQuery.Where(x => objectTxIds.Contains(x.TxId));
        }

        List<LedgerTransactionRecord> transactions = await txQuery
            .OrderByDescending(x => x.LedgerSequence)
            .ThenByDescending(x => x.Timestamp)
            .Skip(Math.Max(0, query.Skip))
            .Take(NormalizeTake(query.Take))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (transactions.Count == 0)
        {
            return Array.Empty<LedgerTransactionSummary>();
        }

        string[] txIds = transactions.Select(x => x.TxId).ToArray();
        List<LedgerTransactionInputRecord> inputRows = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            txIds,
            batch => db.TransactionInputs.AsNoTracking().Where(x => batch.Contains(x.TxId)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, Address[]> inputAddresses = inputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => new Address(x.Address)).Distinct().ToArray(),
                StringComparer.Ordinal);
        Dictionary<string, LedgerTransactionInputSummary[]> inputsByTxId = inputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(x => x.InputIndex)
                    .Select(EconomyNodeRecordMapper.ToLedgerTransactionInputSummary)
                    .ToArray(),
                StringComparer.Ordinal);

        List<LedgerTransactionOutputRecord> outputRows = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            txIds,
            batch => db.TransactionOutputs.AsNoTracking().Where(x => batch.Contains(x.TxId)),
            cancellationToken).ConfigureAwait(false);
        Dictionary<string, Address[]> outputAddresses = outputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => new Address(x.Address)).Distinct().ToArray(),
                StringComparer.Ordinal);
        Dictionary<string, LedgerTransactionOutputSummary[]> outputsByTxId = outputRows
            .GroupBy(x => x.TxId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(x => x.OutputIndex)
                    .Select(EconomyNodeRecordMapper.ToLedgerTransactionOutputSummary)
                    .ToArray(),
                StringComparer.Ordinal);

        return transactions
            .Select(row =>
            {
                inputAddresses.TryGetValue(row.TxId, out Address[]? inputs);
                outputAddresses.TryGetValue(row.TxId, out Address[]? outputs);
                inputsByTxId.TryGetValue(row.TxId, out LedgerTransactionInputSummary[]? inputDetails);
                outputsByTxId.TryGetValue(row.TxId, out LedgerTransactionOutputSummary[]? outputDetails);
                return new LedgerTransactionSummary(
                    query.Context,
                    new TxId(row.TxId),
                    (TransactionKind)row.Kind,
                    (TransactionStatus)row.Status,
                    row.ConfirmedAt ?? row.Timestamp,
                    inputs ?? Array.Empty<Address>(),
                    outputs ?? Array.Empty<Address>(),
                    inputDetails ?? Array.Empty<LedgerTransactionInputSummary>(),
                    outputDetails ?? Array.Empty<LedgerTransactionOutputSummary>(),
                    query.IncludeData ? EconomyNodeRecordMapper.ToTransactionData(row) : null);
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        List<AddressStateProjectionRecord> projections = await db.AddressStateProjections
            .AsNoTracking()
            .Where(x => x.LedgerId == DefaultLedgerId.Value && x.ScenarioId == null && x.UtxoCount > 0)
            .OrderBy(x => x.Address)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (projections.Count > 0)
        {
            string[] projectedAddresses = projections.Select(x => x.Address).ToArray();
            List<UnspentOutputRecord> unprojectedRows = await db.UnspentOutputs
                .AsNoTracking()
                .Where(x => x.LedgerId == DefaultLedgerId.Value
                    && x.ScenarioId == null
                    && !projectedAddresses.Contains(x.Address))
                .OrderBy(x => x.Address)
                .ThenBy(x => x.TokenId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return projections
                .Select(EconomyNodeRecordMapper.ToAccountSummary)
                .Concat(EconomyNodeRecordMapper.ToAccountSummaries(unprojectedRows))
                .OrderBy(x => x.Address, StringComparer.Ordinal)
                .ToArray();
        }

        List<UnspentOutputRecord> rows = await db.UnspentOutputs
            .AsNoTracking()
            .Where(x => x.LedgerId == DefaultLedgerId.Value && x.ScenarioId == null)
            .OrderBy(x => x.Address)
            .ThenBy(x => x.TokenId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return EconomyNodeRecordMapper.ToAccountSummaries(rows);
    }

    public async Task<IReadOnlyList<TransactionSummary>> GetAddressTransactionsAsync(string address, CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string[] outputTxIds = await db.TransactionOutputs
            .AsNoTracking()
            .Where(x => x.Address == address)
            .Select(x => x.TxId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        string[] inputTxIds = await db.TransactionInputs
            .AsNoTracking()
            .Where(x => x.Address == address)
            .Select(x => x.TxId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        string[] txIds = outputTxIds.Concat(inputTxIds).Distinct(StringComparer.Ordinal).ToArray();
        List<LedgerTransactionRecord> transactions = await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
            txIds,
            batch => db.Transactions.AsNoTracking().Where(x => batch.Contains(x.TxId)),
            cancellationToken).ConfigureAwait(false);

        return transactions
            .OrderByDescending(x => x.LedgerSequence)
            .Select(EconomyNodeRecordMapper.ToSummary)
            .ToArray();
    }

    public async Task<TransactionDetails?> GetTransactionAsync(
        string txId,
        bool includeData,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        LedgerTransactionRecord? tx = await db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TxId == txId, cancellationToken)
            .ConfigureAwait(false);

        if (tx is null)
        {
            return null;
        }

        List<LedgerTransactionInputRecord> inputs = await db.TransactionInputs.AsNoTracking()
            .Where(x => x.TxId == txId)
            .OrderBy(x => x.InputIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<LedgerTransactionOutputRecord> outputs = await db.TransactionOutputs.AsNoTracking()
            .Where(x => x.TxId == txId)
            .OrderBy(x => x.OutputIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TransactionDetails(
            tx.TxId,
            tx.LedgerSequence ?? 0,
            ((TransactionKind)tx.Kind).ToString(),
            ((TransactionStatus)tx.Status).ToString(),
            tx.Timestamp,
            tx.ConfirmedAt,
            tx.TxHash,
            tx.LedgerEntryHash,
            tx.DataItemType.HasValue ? ((TransactionDataItemType)tx.DataItemType.Value).ToString() : null,
            includeData ? tx.DataContentType : null,
            tx.DataHash,
            includeData ? tx.InlineJson : null,
            includeData ? tx.ExternalRef : null,
            inputs.Select(EconomyNodeRecordMapper.ToTransactionInputDetails).ToArray(),
            outputs.Select(x => new TransactionOutputDetails(
                x.OutputIndex,
                x.Address,
                x.MainAmount,
                x.TokenId,
                x.TokenAmount,
                x.BusinessObjectId,
                x.BusinessObjectVersion,
                x.BusinessObjectOperation.HasValue ? ((BusinessObjectOperation)x.BusinessObjectOperation.Value).ToString() : null,
                x.BusinessObjectDataHash)).ToArray());
    }

    private async Task<IReadOnlyList<UnspentOutputRecord>> GetUnspentOutputRowsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string? scenarioValue = query.Context.ScenarioId?.Value;
        return await db.UnspentOutputs
            .AsNoTracking()
            .Where(x => x.LedgerId == query.Context.LedgerId.Value
                && x.ScenarioId == scenarioValue
                && x.Address == query.Address.Value)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.TxId)
            .ThenBy(x => x.OutputIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static LedgerBusinessObjectHolding ToBusinessObjectHolding(
        BusinessObjectRecord current,
        BusinessObjectVersionRecord? version)
        => new(
            new BusinessObjectId(current.ObjectId),
            current.CurrentVersion,
            new Address(current.CurrentOwnerAddress),
            BusinessObjectOperation.Update,
            new HashHex(current.CurrentHash),
            new OutPoint(new TxId(current.CurrentOutPointTxId), current.CurrentOutPointIndex),
            version?.CreatedAt ?? DateTimeOffset.MinValue,
            current.ObjectType,
            version?.SchemaVersion ?? "1.0",
            current.CurrentState,
            null,
            TransactionDataItemType.BusinessObject);

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

    private static ScenarioId? ToScenarioId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new ScenarioId(value);
}

internal static class EconomyNodeRecordMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static IReadOnlyList<AccountSummary> ToAccountSummaries(IReadOnlyList<UnspentOutputRecord> rows)
    {
        return rows
            .GroupBy(x => x.Address, StringComparer.Ordinal)
            .Select(group =>
            {
                decimal main = group.Aggregate(0m, (sum, row) => sum + row.MainAmount);
                Dictionary<string, decimal> tokens = group
                    .Where(x => !string.IsNullOrWhiteSpace(x.TokenId))
                    .GroupBy(x => x.TokenId!, StringComparer.Ordinal)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Aggregate(0m, (sum, row) => sum + (row.TokenAmount ?? 0m)),
                        StringComparer.Ordinal);

                return new AccountSummary(group.Key, main, FormatDecimal(main), tokens, FormatTokenBalances(tokens), group.Count());
            })
            .OrderBy(x => x.Address, StringComparer.Ordinal)
            .ToArray();
    }

    public static TransactionSummary ToSummary(LedgerTransactionRecord row)
        => new(
            row.TxId,
            row.LedgerSequence ?? 0,
            ((TransactionKind)row.Kind).ToString(),
            ((TransactionStatus)row.Status).ToString(),
            row.ConfirmedAt ?? row.Timestamp,
            row.DataItemType.HasValue ? ((TransactionDataItemType)row.DataItemType.Value).ToString() : null);

    public static LedgerSpendableOutput ToSpendableOutput(
        LedgerAccountContext context,
        UnspentOutputRecord row)
        => new(
            context,
            new OutPointKey(new HashHex(row.OutPointKey)),
            new OutPoint(new TxId(row.TxId), row.OutputIndex),
            new Address(row.Address),
            new Amount(row.MainAmount),
            ToTokenTransfer(row),
            ToBusinessObjectRef(row),
            row.CreatedAt);

    public static LedgerBusinessObjectHolding ToBusinessObjectHolding(
        UnspentOutputRecord row,
        LedgerTransactionRecord? transaction,
        BusinessObjectPayloadProjection? fallbackPayload,
        BusinessObjectRecord? currentProjection = null)
    {
        BusinessObjectPayloadEnvelope? envelope =
            TryReadPayloadEnvelope(transaction?.InlineJson)
            ?? TryReadPayloadEnvelope(fallbackPayload?.InlineJson);
        return new LedgerBusinessObjectHolding(
            new BusinessObjectId(row.BusinessObjectId ?? string.Empty),
            row.BusinessObjectVersion ?? 0,
            new Address(row.Address),
            (BusinessObjectOperation)(row.BusinessObjectOperation ?? (int)BusinessObjectOperation.Create),
            new HashHex(row.BusinessObjectDataHash ?? currentProjection?.CurrentHash ?? transaction?.DataHash ?? string.Empty),
            new OutPoint(new TxId(row.TxId), row.OutputIndex),
            row.CreatedAt,
            currentProjection?.ObjectType ?? envelope?.ObjectType ?? "business-object",
            envelope?.SchemaVersion ?? "1.0",
            currentProjection?.CurrentState ?? envelope?.StateJson ?? transaction?.InlineJson ?? fallbackPayload?.InlineJson,
            transaction?.ExternalRef ?? fallbackPayload?.ExternalRef,
            (transaction?.DataItemType ?? fallbackPayload?.DataItemType) is null
                ? TransactionDataItemType.BusinessObject
                : (TransactionDataItemType)(transaction?.DataItemType ?? fallbackPayload!.DataItemType)!.Value);
    }

    public static TokenTransfer? ToTokenTransfer(UnspentOutputRecord row)
        => string.IsNullOrWhiteSpace(row.TokenId)
            ? null
            : new TokenTransfer(new TokenId(row.TokenId), new Amount(row.TokenAmount ?? 0m));

    public static TokenTransfer? ToTokenTransfer(LedgerTransactionOutputRecord row)
        => string.IsNullOrWhiteSpace(row.TokenId)
            ? null
            : new TokenTransfer(new TokenId(row.TokenId), new Amount(row.TokenAmount ?? 0m));

    public static BusinessObjectRef? ToBusinessObjectRef(UnspentOutputRecord row)
        => string.IsNullOrWhiteSpace(row.BusinessObjectId)
            ? null
            : new BusinessObjectRef(
                new BusinessObjectId(row.BusinessObjectId),
                row.BusinessObjectVersion ?? 0,
                (BusinessObjectOperation)(row.BusinessObjectOperation ?? (int)BusinessObjectOperation.Create),
                new HashHex(row.BusinessObjectDataHash ?? string.Empty));

    public static BusinessObjectRef? ToBusinessObjectRef(LedgerTransactionOutputRecord row)
        => string.IsNullOrWhiteSpace(row.BusinessObjectId)
            ? null
            : new BusinessObjectRef(
                new BusinessObjectId(row.BusinessObjectId),
                row.BusinessObjectVersion ?? 0,
                (BusinessObjectOperation)(row.BusinessObjectOperation ?? (int)BusinessObjectOperation.Create),
                new HashHex(row.BusinessObjectDataHash ?? string.Empty));

    public static LedgerTransactionInputSummary ToLedgerTransactionInputSummary(LedgerTransactionInputRecord row)
        => new(
            row.InputIndex,
            new OutPoint(new TxId(row.PreviousTxId), row.PreviousOutputIndex),
            new Address(row.Address),
            string.IsNullOrWhiteSpace(row.OutPointKey) ? null : new OutPointKey(new HashHex(row.OutPointKey)),
            DeserializeSignatureBundle(row.SignatureBundleJson),
            string.IsNullOrWhiteSpace(row.SignaturePayloadHash) ? null : new HashHex(row.SignaturePayloadHash));

    public static TransactionInputDetails ToTransactionInputDetails(LedgerTransactionInputRecord row)
        => new(
            row.InputIndex,
            row.PreviousTxId,
            row.PreviousOutputIndex,
            row.Address,
            row.OutPointKey,
            row.SignaturePayloadHash,
            DeserializeSignatureBundle(row.SignatureBundleJson));

    public static LedgerTransactionOutputSummary ToLedgerTransactionOutputSummary(LedgerTransactionOutputRecord row)
        => new(
            row.OutputIndex,
            new Address(row.Address),
            new Amount(row.MainAmount),
            ToTokenTransfer(row),
            ToBusinessObjectRef(row),
            string.IsNullOrWhiteSpace(row.MetadataHash) ? null : new HashHex(row.MetadataHash));

    public static TransactionData? ToTransactionData(LedgerTransactionRecord row)
    {
        if (row.DataItemType is null
            && string.IsNullOrWhiteSpace(row.DataContentType)
            && string.IsNullOrWhiteSpace(row.DataHash)
            && string.IsNullOrWhiteSpace(row.InlineJson)
            && string.IsNullOrWhiteSpace(row.ExternalRef))
        {
            return null;
        }

        return new TransactionData(
            row.DataContentType,
            string.IsNullOrWhiteSpace(row.DataHash) ? null : new HashHex(row.DataHash),
            row.InlineJson,
            row.ExternalRef,
            row.DataItemType.HasValue ? (TransactionDataItemType)row.DataItemType.Value : TransactionDataItemType.Generic);
    }

    public static AccountSummary ToAccountSummary(AddressStateProjectionRecord row)
    {
        IReadOnlyDictionary<string, decimal> tokens = DeserializeTokenBalances(row.TokenBalancesJson);
        return new(row.Address, row.MainBalance, FormatDecimal(row.MainBalance), tokens, FormatTokenBalances(tokens), row.UtxoCount);
    }

    private static SignatureBundle DeserializeSignatureBundle(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return SignatureBundle.Empty;
        }

        SignatureEnvelopeDto[]? dto = JsonSerializer.Deserialize<SignatureEnvelopeDto[]>(json, JsonOptions);
        if (dto is null || dto.Length == 0)
        {
            return SignatureBundle.Empty;
        }

        return new SignatureBundle(dto
            .Select(x => new SignatureEnvelope(
                (SignatureAlgorithmId)x.Algorithm,
                x.KeyId,
                new HashHex(x.PublicKeyHash),
                new HashHex(x.PayloadHash),
                Convert.FromBase64String(x.SignatureBase64),
                string.IsNullOrWhiteSpace(x.PublicKeyMaterialBase64) ? null : Convert.FromBase64String(x.PublicKeyMaterialBase64)))
            .ToArray());
    }

    private static BusinessObjectPayloadEnvelope? TryReadPayloadEnvelope(string? inlineJson)
    {
        if (string.IsNullOrWhiteSpace(inlineJson))
        {
            return null;
        }

        try
        {
            BusinessObjectPayloadEnvelope? envelope = JsonSerializer.Deserialize<BusinessObjectPayloadEnvelope>(inlineJson, JsonOptions);
            if (string.IsNullOrWhiteSpace(envelope?.ObjectType)
                || string.IsNullOrWhiteSpace(envelope.SchemaVersion)
                || envelope.StateJson is null)
            {
                return null;
            }

            return envelope;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> FormatTokenBalances(IReadOnlyDictionary<string, decimal> tokenBalances)
        => tokenBalances.ToDictionary(
            item => item.Key,
            item => FormatDecimal(item.Value),
            StringComparer.Ordinal);

    private static string FormatDecimal(decimal value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyDictionary<string, decimal> DeserializeTokenBalances(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        Dictionary<string, decimal>? balances = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, JsonOptions);
        return balances is null
            ? new Dictionary<string, decimal>(StringComparer.Ordinal)
            : new SortedDictionary<string, decimal>(balances, StringComparer.Ordinal);
    }

    private sealed record SignatureEnvelopeDto(
        int Algorithm,
        string KeyId,
        string PublicKeyHash,
        string PayloadHash,
        string SignatureBase64,
        string? PublicKeyMaterialBase64 = null);
}

internal sealed record BusinessObjectPayloadProjection(
    string ObjectId,
    int Version,
    string InlineJson,
    string? ExternalRef,
    int? DataItemType);
