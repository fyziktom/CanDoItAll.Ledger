using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Economy.Persistence.EFCore;

public sealed class EfLedgerStore : ILedgerConfirmationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly IDbContextFactory<EconomyDbContext> dbContextFactory;
    private readonly ITransactionHasher? hasher;

    public EfLedgerStore(IDbContextFactory<EconomyDbContext> dbContextFactory, ITransactionHasher? hasher = null)
    {
        this.dbContextFactory = dbContextFactory;
        this.hasher = hasher;
    }

    public async Task<LedgerState?> GetLedgerStateAsync(LedgerId ledgerId, ScenarioId? scenarioId, CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        string? scenarioValue = scenarioId?.Value;
        LedgerStateRecord? row = await db.LedgerStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue, cancellationToken);

        return row is null
            ? null
            : new LedgerState(new LedgerId(row.LedgerId), ToScenarioId(row.ScenarioId), row.CurrentSequence, new HashHex(row.CurrentLedgerHash), row.UpdatedAt);
    }

    public async Task<RegisteredAddress?> GetRegisteredAddressAsync(LedgerId ledgerId, Address address, CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        RegisteredAddressRecord? row = await db.RegisteredAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LedgerId == ledgerId.Value && x.Address == address.Value, cancellationToken);

        if (row is null)
        {
            return null;
        }

        SignaturePolicy policy = new(
            new HashSet<SignatureAlgorithmId> { (SignatureAlgorithmId)row.KeyAlgorithm },
            new HashSet<SignatureAlgorithmId> { (SignatureAlgorithmId)row.KeyAlgorithm },
            RequireAllSignatures: true);

        return new RegisteredAddress(
            new LedgerId(row.LedgerId),
            new Address(row.Address),
            (SignatureAlgorithmId)row.KeyAlgorithm,
            row.PublicKeyMaterial,
            new HashHex(row.PublicKeyHash),
            row.DisplayName,
            (AddressStatus)row.Status,
            policy,
            SignatureBundle.Empty,
            row.CreatedAt,
            row.UpdatedAt);
    }

    public async Task<LedgerTransaction?> GetTransactionAsync(LedgerId ledgerId, ScenarioId? scenarioId, TxId txId, CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        string? scenarioValue = scenarioId?.Value;

        LedgerTransactionRecord? row = await db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue && x.TxId == txId.Value,
                cancellationToken);

        if (row is null)
        {
            return null;
        }

        List<LedgerTransactionInputRecord> inputs = await db.TransactionInputs
            .AsNoTracking()
            .Where(x => x.TxId == txId.Value)
            .OrderBy(x => x.InputIndex)
            .ToListAsync(cancellationToken);

        List<LedgerTransactionOutputRecord> outputs = await db.TransactionOutputs
            .AsNoTracking()
            .Where(x => x.TxId == txId.Value)
            .OrderBy(x => x.OutputIndex)
            .ToListAsync(cancellationToken);

        return MapTransaction(row, inputs, outputs);
    }

    public async Task<IReadOnlyDictionary<OutPointKey, UnspentOutput>> GetUnspentOutputsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        IReadOnlyList<OutPointKey> outPointKeys,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (outPointKeys.Count == 0)
        {
            return new Dictionary<OutPointKey, UnspentOutput>();
        }

        string? scenarioValue = scenarioId?.Value;
        string[] keys = outPointKeys.Select(x => x.Value.Value).Distinct(StringComparer.Ordinal).ToArray();

        IQueryable<UnspentOutputRecord> query = db.UnspentOutputs
            .Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue && keys.Contains(x.OutPointKey));

        List<UnspentOutputRecord> rows = await query.AsNoTracking().ToListAsync(cancellationToken);

        return rows.ToDictionary(x => new OutPointKey(new HashHex(x.OutPointKey)), MapUnspentOutput);
    }

    public async Task<IReadOnlyDictionary<OutPointKey, SpentOutput>> GetSpentOutputsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        IReadOnlyList<OutPointKey> outPointKeys,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (outPointKeys.Count == 0)
        {
            return new Dictionary<OutPointKey, SpentOutput>();
        }

        string? scenarioValue = scenarioId?.Value;
        string[] keys = outPointKeys.Select(x => x.Value.Value).Distinct(StringComparer.Ordinal).ToArray();

        List<SpentOutputRecord> rows = await db.SpentOutputs
            .AsNoTracking()
            .Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue && keys.Contains(x.OutPointKey))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => new OutPointKey(new HashHex(x.OutPointKey)), MapSpentOutput);
    }

    public async Task<IReadOnlyList<UnspentOutput>> GetUnspentOutputsByAddressAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        Address address,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        string? scenarioValue = scenarioId?.Value;
        List<UnspentOutputRecord> rows = await db.UnspentOutputs
            .AsNoTracking()
            .Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue && x.Address == address.Value)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.TxId)
            .ThenBy(x => x.OutputIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapUnspentOutput).ToArray();
    }

    public async Task<bool> TokenExistsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        TokenId tokenId,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        string? scenarioValue = scenarioId?.Value;
        bool existsInRegistry = await db.LedgerTokenTypes
            .AsNoTracking()
            .AnyAsync(
                row => row.LedgerId == ledgerId.Value
                    && row.ScenarioId == scenarioValue
                    && row.TokenId == tokenId.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (existsInRegistry)
        {
            return true;
        }

        return await db.TransactionOutputs
            .AsNoTracking()
            .Join(
                db.Transactions.AsNoTracking(),
                output => output.TxId,
                transaction => transaction.TxId,
                (output, transaction) => new { output, transaction })
            .AnyAsync(
                row => row.transaction.LedgerId == ledgerId.Value
                    && row.transaction.ScenarioId == scenarioValue
                    && row.output.TokenId == tokenId.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<RegisteredTokenDefinition?> GetTokenDefinitionAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        TokenId tokenId,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string? scenarioValue = scenarioId?.Value;
        LedgerTokenTypeRecord? row = await db.LedgerTokenTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.LedgerId == ledgerId.Value
                    && item.ScenarioId == scenarioValue
                    && item.TokenId == tokenId.Value,
                cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : MapRegisteredTokenDefinition(row);
    }

    public async Task<LedgerSubmitResult> ConfirmTransactionAsync(
        LedgerTransaction normalizedTransaction,
        IReadOnlyList<OutPointKey> inputKeys,
        Func<LedgerConfirmationContext, CancellationToken, Task<LedgerConfirmationDecision>> confirm,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext strategyDb = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IExecutionStrategy strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await ConfirmTransactionCoreAsync(db, normalizedTransaction, inputKeys, confirm, cancellationToken)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private async Task<LedgerSubmitResult> ConfirmTransactionCoreAsync(
        EconomyDbContext db,
        LedgerTransaction normalizedTransaction,
        IReadOnlyList<OutPointKey> inputKeys,
        Func<LedgerConfirmationContext, CancellationToken, Task<LedgerConfirmationDecision>> confirm,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? databaseTransaction = null;

        try
        {
            if (db.Database.IsRelational())
            {
                databaseTransaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            }

            TxId txId = normalizedTransaction.TxId ?? throw new InvalidOperationException("Normalized transaction must have a transaction id.");
            string stateKey = StateKey(normalizedTransaction.LedgerId, normalizedTransaction.ScenarioId);

            LedgerStateRecord? state = await LoadLedgerStateForUpdateAsync(db, stateKey, cancellationToken).ConfigureAwait(false);
            LedgerTransaction? existing = await LoadTransactionAsync(db, normalizedTransaction.LedgerId, normalizedTransaction.ScenarioId, txId, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyDictionary<OutPointKey, UnspentOutput> unspent = await LoadUnspentOutputsForConfirmationAsync(
                db,
                inputKeys,
                cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<OutPointKey, SpentOutput> spent = await LoadSpentOutputsForConfirmationAsync(
                db,
                inputKeys,
                cancellationToken).ConfigureAwait(false);

            LedgerState? mappedState = state is null
                ? null
                : new LedgerState(
                    new LedgerId(state.LedgerId),
                    ToScenarioId(state.ScenarioId),
                    state.CurrentSequence,
                    new HashHex(state.CurrentLedgerHash),
                    state.UpdatedAt);
            LedgerConfirmationDecision decision = await confirm(
                new LedgerConfirmationContext(mappedState, existing, unspent, spent),
                cancellationToken).ConfigureAwait(false);
            if (!decision.Accepted || decision.ConfirmedTransaction is null)
            {
                if (databaseTransaction is not null)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }

                return LedgerSubmitResult.Rejected(decision.Errors);
            }

            await AppendConfirmedTransactionCoreAsync(db, state, decision.ConfirmedTransaction, cancellationToken)
                .ConfigureAwait(false);
            await SaveChangesWithConflictMappingAsync(db, cancellationToken).ConfigureAwait(false);

            if (databaseTransaction is not null)
            {
                await databaseTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return LedgerSubmitResult.Confirmed(
                decision.ConfirmedTransaction.Transaction.TxId ?? txId,
                decision.ConfirmedTransaction.LedgerSequence);
        }
        catch (LedgerStoreConflictException ex)
        {
            if (databaseTransaction is not null)
            {
                await databaseTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            return LedgerSubmitResult.Rejected(new[] { new ValidationError(ex.Code, ex.Message) });
        }
        catch
        {
            if (databaseTransaction is not null)
            {
                await databaseTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (databaseTransaction is not null)
            {
                await databaseTransaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task AppendConfirmedTransactionAsync(ConfirmedTransaction confirmedTransaction, CancellationToken cancellationToken)
    {
        await using EconomyDbContext strategyDb = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IExecutionStrategy strategy = strategyDb.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await AppendConfirmedTransactionWithContextAsync(db, confirmedTransaction, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private async Task AppendConfirmedTransactionWithContextAsync(
        EconomyDbContext db,
        ConfirmedTransaction confirmedTransaction,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? databaseTransaction = null;

        try
        {
            if (db.Database.IsRelational())
            {
                databaseTransaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            }

            LedgerTransaction tx = confirmedTransaction.Transaction;
            string stateKey = StateKey(tx.LedgerId, tx.ScenarioId);
            LedgerStateRecord? state = await LoadLedgerStateForUpdateAsync(db, stateKey, cancellationToken).ConfigureAwait(false);
            await AppendConfirmedTransactionCoreAsync(db, state, confirmedTransaction, cancellationToken).ConfigureAwait(false);
            await SaveChangesWithConflictMappingAsync(db, cancellationToken).ConfigureAwait(false);

            if (databaseTransaction is not null)
            {
                await databaseTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            if (databaseTransaction is not null)
            {
                await databaseTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (databaseTransaction is not null)
            {
                await databaseTransaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task AppendConfirmedTransactionCoreAsync(
        EconomyDbContext db,
        LedgerStateRecord? state,
        ConfirmedTransaction confirmedTransaction,
        CancellationToken cancellationToken)
    {
        LedgerTransaction tx = confirmedTransaction.Transaction;
        TxId txId = tx.TxId ?? throw new InvalidOperationException("Confirmed transaction must have a transaction id.");
        HashHex txHash = tx.TxHash ?? throw new InvalidOperationException("Confirmed transaction must have a transaction hash.");
        string stateKey = StateKey(tx.LedgerId, tx.ScenarioId);

        ValidateLedgerStateExpectation(state, confirmedTransaction);

        if (await db.Transactions.AsNoTracking().AnyAsync(x => x.TxId == txId.Value, cancellationToken).ConfigureAwait(false))
        {
            throw new LedgerStoreConflictException(
                ValidationErrorCodes.DuplicateTransaction,
                $"Transaction already exists: {txId.Value}.");
        }

        string? scenarioValue = tx.ScenarioId?.Value;
        if (await db.Transactions
            .AsNoTracking()
            .AnyAsync(
                x => x.LedgerId == tx.LedgerId.Value
                    && x.ScenarioId == scenarioValue
                    && x.LedgerSequence == confirmedTransaction.LedgerSequence,
                cancellationToken)
            .ConfigureAwait(false))
        {
            throw new LedgerStoreConflictException(
                ValidationErrorCodes.LedgerStateConflict,
                $"Ledger sequence already exists: {confirmedTransaction.LedgerSequence}.");
        }

        db.Transactions.Add(ToTransactionRecord(confirmedTransaction, txId, txHash));

        TransactionInput[] orderedInputs = tx.Inputs.OrderBy(x => x.InputIndex).ToArray();
        Dictionary<int, string> inputOutPointKeys = new();
        var touchedAddresses = new HashSet<string>(StringComparer.Ordinal);
        var consumedOutPointKeys = new HashSet<string>(StringComparer.Ordinal);
        var inputTokenIds = new HashSet<TokenId>();
        var consumedBusinessObjectOutputs = new List<UnspentOutputRecord>();
        foreach (TransactionInput input in orderedInputs)
        {
            inputOutPointKeys[input.InputIndex] = await ResolveInputOutPointKeyAsync(db, tx, input, cancellationToken).ConfigureAwait(false);
            touchedAddresses.Add(input.Address.Value);
        }

        await LockInputUtxosForUpdateAsync(db, inputOutPointKeys.Values.ToArray(), cancellationToken).ConfigureAwait(false);

        var spentRows = new List<SpentOutputRecord>();
        foreach (TransactionInput input in orderedInputs)
        {
            string outPointKey = inputOutPointKeys[input.InputIndex];
            consumedOutPointKeys.Add(outPointKey);
            db.TransactionInputs.Add(ToInputRecord(txId, input, outPointKey));

            UnspentOutputRecord? existing = await db.UnspentOutputs
                .FirstOrDefaultAsync(x => x.OutPointKey == outPointKey, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                throw new LedgerStoreConflictException(
                    ValidationErrorCodes.UnknownInput,
                    $"Cannot spend unknown output {input.PreviousOutPoint} for {input.Address}.");
            }

            db.UnspentOutputs.Remove(existing);
            if (!string.IsNullOrWhiteSpace(existing.TokenId))
            {
                inputTokenIds.Add(new TokenId(existing.TokenId));
            }

            if (!string.IsNullOrWhiteSpace(existing.BusinessObjectId))
            {
                consumedBusinessObjectOutputs.Add(existing);
            }

            spentRows.Add(new SpentOutputRecord
            {
                OutPointKey = outPointKey,
                LedgerId = tx.LedgerId.Value,
                ScenarioId = tx.ScenarioId?.Value,
                TxId = input.PreviousOutPoint.TxId.Value,
                OutputIndex = input.PreviousOutPoint.OutputIndex,
                Address = input.Address.Value,
                SpentInTxId = txId.Value,
                SpentAt = confirmedTransaction.ConfirmedAt,
                AdminSignatureJson = SerializeSignatureBundle(confirmedTransaction.AdminSignature)
            });
        }

        db.SpentOutputs.AddRange(spentRows);

        var createdOutputs = new List<UnspentOutputRecord>();
        foreach (TransactionOutput output in tx.Outputs.OrderBy(x => x.OutputIndex))
        {
            db.TransactionOutputs.Add(ToOutputRecord(txId, output));
            UnspentOutputRecord createdOutput = ToUnspentOutputRecord(tx, txId, output, confirmedTransaction.ConfirmedAt);
            db.UnspentOutputs.Add(createdOutput);
            createdOutputs.Add(createdOutput);
            touchedAddresses.Add(output.Address.Value);
        }

        await AddTokenTypeRowsAsync(db, confirmedTransaction, txId, inputTokenIds, cancellationToken).ConfigureAwait(false);
        await ApplyBusinessObjectProjectionsAsync(
            db,
            tx,
            confirmedTransaction,
            consumedBusinessObjectOutputs,
            createdOutputs,
            cancellationToken).ConfigureAwait(false);

        if (state is null)
        {
            db.LedgerStates.Add(new LedgerStateRecord
            {
                StateKey = stateKey,
                LedgerId = tx.LedgerId.Value,
                ScenarioId = tx.ScenarioId?.Value,
                CurrentSequence = confirmedTransaction.LedgerSequence,
                CurrentLedgerHash = confirmedTransaction.LedgerEntryHash.Value,
                UpdatedAt = confirmedTransaction.ConfirmedAt
            });
        }
        else
        {
            state.CurrentSequence = confirmedTransaction.LedgerSequence;
            state.CurrentLedgerHash = confirmedTransaction.LedgerEntryHash.Value;
            state.UpdatedAt = confirmedTransaction.ConfirmedAt;
        }

        await RebuildTouchedAddressProjectionsAsync(
            db,
            tx,
            confirmedTransaction,
            touchedAddresses,
            consumedOutPointKeys,
            createdOutputs,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<LedgerStateRecord?> LoadLedgerStateForUpdateAsync(EconomyDbContext db, string stateKey, CancellationToken cancellationToken)
    {
        if (string.Equals(db.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            return await db.LedgerStates
                .FromSqlInterpolated($@"SELECT * FROM ""Economy_LedgerStates"" WHERE ""StateKey"" = {stateKey} FOR UPDATE")
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return await db.LedgerStates.FindAsync([stateKey], cancellationToken).ConfigureAwait(false);
    }

    private static async Task<LedgerTransaction?> LoadTransactionAsync(
        EconomyDbContext db,
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        TxId txId,
        CancellationToken cancellationToken)
    {
        string? scenarioValue = scenarioId?.Value;
        LedgerTransactionRecord? row = await db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue && x.TxId == txId.Value,
                cancellationToken)
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

        return MapTransaction(row, inputs, outputs);
    }

    private static async Task<IReadOnlyDictionary<OutPointKey, UnspentOutput>> LoadUnspentOutputsForConfirmationAsync(
        EconomyDbContext db,
        IReadOnlyList<OutPointKey> inputKeys,
        CancellationToken cancellationToken)
    {
        if (inputKeys.Count == 0)
        {
            return new Dictionary<OutPointKey, UnspentOutput>();
        }

        string[] keys = inputKeys.Select(x => x.Value.Value).Distinct(StringComparer.Ordinal).ToArray();
        await LockInputUtxosForUpdateAsync(db, keys, cancellationToken).ConfigureAwait(false);

        List<UnspentOutputRecord> rows = await db.UnspentOutputs
            .AsNoTracking()
            .Where(x => keys.Contains(x.OutPointKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(x => new OutPointKey(new HashHex(x.OutPointKey)), MapUnspentOutput);
    }

    private static async Task<IReadOnlyDictionary<OutPointKey, SpentOutput>> LoadSpentOutputsForConfirmationAsync(
        EconomyDbContext db,
        IReadOnlyList<OutPointKey> inputKeys,
        CancellationToken cancellationToken)
    {
        if (inputKeys.Count == 0)
        {
            return new Dictionary<OutPointKey, SpentOutput>();
        }

        string[] keys = inputKeys.Select(x => x.Value.Value).Distinct(StringComparer.Ordinal).ToArray();
        List<SpentOutputRecord> rows = await db.SpentOutputs
            .AsNoTracking()
            .Where(x => keys.Contains(x.OutPointKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(x => new OutPointKey(new HashHex(x.OutPointKey)), MapSpentOutput);
    }

    private static async Task SaveChangesWithConflictMappingAsync(EconomyDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (TryMapStoreConflict(ex, out string? code, out string? message))
        {
            throw new LedgerStoreConflictException(
                code,
                message);
        }
    }

    private static bool TryMapStoreConflict(Exception exception, out string code, out string message)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            string currentMessage = current.Message;
            if (ContainsAny(currentMessage, "Economy_LedgerTokenTypes", "LedgerTokenTypes", "TokenTypeKey"))
            {
                code = ValidationErrorCodes.DuplicateTokenType;
                message = "Token type already exists for this ledger and scenario.";
                return true;
            }

            if (ContainsAny(currentMessage, "LedgerSequence", "IX_Economy_LedgerTransactions_LedgerId_ScenarioId_LedgerSequence"))
            {
                code = ValidationErrorCodes.LedgerStateConflict;
                message = "Ledger sequence changed during confirmation.";
                return true;
            }

            if (ContainsAny(currentMessage, "Economy_LedgerTransactions", "LedgerTransactions", "PK_Economy_LedgerTransactions", "TxId"))
            {
                code = ValidationErrorCodes.DuplicateTransaction;
                message = "Transaction already exists for this ledger.";
                return true;
            }

            if (ContainsAny(currentMessage, "Economy_LedgerStates", "LedgerStates", "StateKey"))
            {
                code = ValidationErrorCodes.LedgerStateConflict;
                message = "Ledger state changed during confirmation.";
                return true;
            }

            if (ContainsAny(currentMessage, "Economy_UnspentOutputs", "UnspentOutputs", "Economy_SpentOutputs", "SpentOutputs", "OutPointKey"))
            {
                code = ValidationErrorCodes.SpentInput;
                message = "Input state changed during confirmation.";
                return true;
            }

            if (ContainsAny(currentMessage, "Economy_BusinessObjects", "BusinessObjectKey", "Economy_BusinessObjectVersions"))
            {
                code = ValidationErrorCodes.InvalidBusinessObjectTransition;
                message = "BusinessObject state changed during confirmation.";
                return true;
            }

            if (ContainsAny(currentMessage, "Economy_AddressStateProjections", "ProjectionKey"))
            {
                code = ValidationErrorCodes.LedgerStateConflict;
                message = "Address projection state changed during confirmation.";
                return true;
            }
        }

        code = string.Empty;
        message = string.Empty;
        return false;
    }

    private static bool ContainsAny(string value, params string[] needles)
        => needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static void ValidateLedgerStateExpectation(LedgerStateRecord? state, ConfirmedTransaction confirmedTransaction)
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
            || !string.Equals(state.CurrentLedgerHash, confirmedTransaction.PreviousLedgerHash.Value, StringComparison.Ordinal))
        {
            throw new LedgerStoreConflictException(
                ValidationErrorCodes.LedgerStateConflict,
                "Ledger state changed during confirmation.");
        }
    }

    private static async Task RebuildTouchedAddressProjectionsAsync(
        EconomyDbContext db,
        LedgerTransaction tx,
        ConfirmedTransaction confirmedTransaction,
        IReadOnlyCollection<string> touchedAddresses,
        IReadOnlySet<string> consumedOutPointKeys,
        IReadOnlyList<UnspentOutputRecord> createdOutputs,
        CancellationToken cancellationToken)
    {
        if (touchedAddresses.Count == 0)
        {
            return;
        }

        string? scenarioValue = tx.ScenarioId?.Value;

        foreach (string address in touchedAddresses.Order(StringComparer.Ordinal))
        {
            List<UnspentOutputRecord> rows = await db.UnspentOutputs
                .AsNoTracking()
                .Where(x => x.LedgerId == tx.LedgerId.Value && x.ScenarioId == scenarioValue && x.Address == address)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            rows.RemoveAll(x => consumedOutPointKeys.Contains(x.OutPointKey));
            rows.AddRange(createdOutputs.Where(x => x.Address == address));

            string projectionKey = ProjectionKey(tx.LedgerId, tx.ScenarioId, new Address(address));
            AddressStateProjectionRecord? projection = await db.AddressStateProjections.FindAsync([projectionKey], cancellationToken).ConfigureAwait(false);
            EfProjectionBalance balance = EfLedgerProjectionCalculator.CalculateBalance(rows);
            string tokenBalancesJson = JsonSerializer.Serialize(balance.TokenBalances, JsonOptions);
            HashHex stateHash = EfLedgerProjectionCalculator.CalculateAddressProjectionHash(
                tx.LedgerId,
                tx.ScenarioId,
                address,
                balance.MainBalance,
                balance.TokenBalances,
                balance.UtxoCount,
                confirmedTransaction.LedgerSequence);

            if (projection is null)
            {
                db.AddressStateProjections.Add(new AddressStateProjectionRecord
                {
                    ProjectionKey = projectionKey,
                    LedgerId = tx.LedgerId.Value,
                    ScenarioId = scenarioValue,
                    Address = address,
                    MainBalance = balance.MainBalance,
                    TokenBalancesJson = tokenBalancesJson,
                    UtxoCount = balance.UtxoCount,
                    LastLedgerSequence = confirmedTransaction.LedgerSequence,
                    StateHash = stateHash.Value,
                    AdminSignatureJson = SerializeSignatureBundle(confirmedTransaction.AdminSignature),
                    UpdatedAt = confirmedTransaction.ConfirmedAt
                });
            }
            else
            {
                projection.MainBalance = balance.MainBalance;
                projection.TokenBalancesJson = tokenBalancesJson;
                projection.UtxoCount = balance.UtxoCount;
                projection.LastLedgerSequence = confirmedTransaction.LedgerSequence;
                projection.StateHash = stateHash.Value;
                projection.AdminSignatureJson = SerializeSignatureBundle(confirmedTransaction.AdminSignature);
                projection.UpdatedAt = confirmedTransaction.ConfirmedAt;
            }
        }
    }

    private static async Task AddTokenTypeRowsAsync(
        EconomyDbContext db,
        ConfirmedTransaction confirmedTransaction,
        TxId txId,
        IReadOnlySet<TokenId> inputTokenIds,
        CancellationToken cancellationToken)
    {
        LedgerTransaction tx = confirmedTransaction.Transaction;
        if (tx.Kind is not (TransactionKind.Genesis or TransactionKind.Mint))
        {
            return;
        }

        foreach (TokenId tokenId in tx.Outputs
            .Where(x => x.Token is not null)
            .Select(x => x.Token!.TokenId)
            .Distinct())
        {
            if (inputTokenIds.Contains(tokenId))
            {
                continue;
            }

            string tokenTypeKey = TokenTypeKey(tx.LedgerId, tx.ScenarioId, tokenId);
            bool tokenTypeExists = await db.LedgerTokenTypes
                .AsNoTracking()
                .AnyAsync(x => x.TokenTypeKey == tokenTypeKey, cancellationToken)
                .ConfigureAwait(false);
            if (tokenTypeExists)
            {
                throw new LedgerStoreConflictException(
                    ValidationErrorCodes.DuplicateTokenType,
                    $"Token type already exists: {tokenId.Value}.");
            }

            db.LedgerTokenTypes.Add(new LedgerTokenTypeRecord
            {
                TokenTypeKey = tokenTypeKey,
                LedgerId = tx.LedgerId.Value,
                ScenarioId = tx.ScenarioId?.Value,
                TokenId = tokenId.Value,
                ProtocolVersion = tx.TokenDefinition?.Definition.ProtocolVersion ?? 1,
                DefinitionHash = tx.TokenDefinition is not null && TokenDefinitionIdentity.IsV2TokenId(tokenId)
                    ? tokenId.Value[TokenDefinitionIdentity.V2TokenIdPrefix.Length..]
                    : null,
                TokenNamespace = tx.TokenDefinition?.Definition.Namespace,
                Symbol = tx.TokenDefinition?.Definition.Symbol,
                IssuerAddress = tx.TokenDefinition?.Definition.Issuer.Value,
                InitialSupply = tx.TokenDefinition?.Definition.InitialSupply.Value,
                DecimalPlaces = tx.TokenDefinition?.Definition.DecimalPlaces,
                Classification = (int)(tx.TokenDefinition?.Definition.Classification ?? TokenClassification.LegacyUnspecified),
                DisplayName = tx.TokenDefinition?.DisplayMetadata?.DisplayName,
                Description = tx.TokenDefinition?.DisplayMetadata?.Description,
                DisplayExternalRef = tx.TokenDefinition?.DisplayMetadata?.ExternalRef,
                CreatedTxId = txId.Value,
                CreatedOutputIndex = tx.Outputs.First(x => x.Token?.TokenId == tokenId).OutputIndex,
                CreatedAt = confirmedTransaction.ConfirmedAt
            });
        }
    }

    private static async Task ApplyBusinessObjectProjectionsAsync(
        EconomyDbContext db,
        LedgerTransaction tx,
        ConfirmedTransaction confirmedTransaction,
        IReadOnlyList<UnspentOutputRecord> consumedOutputs,
        IReadOnlyList<UnspentOutputRecord> createdOutputs,
        CancellationToken cancellationToken)
    {
        UnspentOutputRecord[] createdBusinessObjects = createdOutputs
            .Where(x => !string.IsNullOrWhiteSpace(x.BusinessObjectId))
            .ToArray();
        UnspentOutputRecord[] consumedBusinessObjects = consumedOutputs
            .Where(x => !string.IsNullOrWhiteSpace(x.BusinessObjectId))
            .ToArray();
        string[] objectIds = consumedBusinessObjects
            .Concat(createdBusinessObjects)
            .Select(x => x.BusinessObjectId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (objectIds.Length == 0)
        {
            return;
        }

        string? scenarioValue = tx.ScenarioId?.Value;
        Dictionary<string, BusinessObjectRecord> currentByObjectId = await db.BusinessObjects
            .Where(x => x.LedgerId == tx.LedgerId.Value
                && x.ScenarioId == scenarioValue
                && objectIds.Contains(x.ObjectId))
            .ToDictionaryAsync(x => x.ObjectId, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        HashSet<string> createdObjectIds = createdBusinessObjects
            .Select(x => x.BusinessObjectId!)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, UnspentOutputRecord> consumedCurrentByObjectId = consumedBusinessObjects
            .GroupBy(x => x.BusinessObjectId!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        foreach (UnspentOutputRecord consumed in consumedBusinessObjects)
        {
            string objectId = consumed.BusinessObjectId!;
            if (!createdObjectIds.Contains(objectId)
                && currentByObjectId.TryGetValue(objectId, out BusinessObjectRecord? current))
            {
                db.BusinessObjects.Remove(current);
            }
        }

        List<string> existingVersionRows = await db.BusinessObjectVersions
            .Where(x => x.LedgerId == tx.LedgerId.Value
                && x.ScenarioId == scenarioValue
                && objectIds.Contains(x.ObjectId))
            .Select(x => x.ObjectId + "|" + x.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingVersions = existingVersionRows.ToHashSet(StringComparer.Ordinal);

        var txId = confirmedTransaction.Transaction.TxId
            ?? throw new InvalidOperationException("Confirmed BusinessObject transaction must have a transaction id.");
        foreach (UnspentOutputRecord output in createdBusinessObjects)
        {
            string objectId = output.BusinessObjectId!;
            currentByObjectId.TryGetValue(objectId, out BusinessObjectRecord? previous);
            BusinessObjectOperation operation = (BusinessObjectOperation)(output.BusinessObjectOperation ?? 0);
            int version = output.BusinessObjectVersion ?? 0;
            consumedCurrentByObjectId.TryGetValue(objectId, out UnspentOutputRecord? consumedCurrent);
            ValidateBusinessObjectProjectionOperation(objectId, operation, version, previous, consumedCurrent);

            BusinessObjectProjectionPayload payload = EfBusinessObjectProjectionMapper.ResolvePayload(
                tx.Data?.InlineJson,
                tx.Data?.DataHash?.Value,
                output,
                previous);
            BusinessObjectRecord next = EfBusinessObjectProjectionMapper.ToCurrentRecord(
                tx.LedgerId,
                tx.ScenarioId,
                txId,
                output,
                payload,
                previous);

            if (previous is null)
            {
                db.BusinessObjects.Add(next);
                currentByObjectId[objectId] = next;
            }
            else
            {
                EfBusinessObjectProjectionMapper.CopyCurrentRecord(previous, next);
            }

            string versionKey = objectId + "|" + (output.BusinessObjectVersion ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!existingVersions.Add(versionKey))
            {
                throw new LedgerStoreConflictException(
                    ValidationErrorCodes.InvalidBusinessObjectTransition,
                    $"BusinessObject {objectId} version {output.BusinessObjectVersion ?? 0} already exists.");
            }

            db.BusinessObjectVersions.Add(EfBusinessObjectProjectionMapper.ToVersionRecord(
                tx.LedgerId,
                tx.ScenarioId,
                txId,
                output,
                payload,
                confirmedTransaction.ConfirmedAt));
        }
    }

    private static void ValidateBusinessObjectProjectionOperation(
        string objectId,
        BusinessObjectOperation operation,
        int version,
        BusinessObjectRecord? previous,
        UnspentOutputRecord? consumedCurrent)
    {
        if (!Enum.IsDefined(operation))
        {
            throw new LedgerStoreConflictException(
                ValidationErrorCodes.InvalidBusinessObjectTransition,
                $"BusinessObject {objectId} has an unknown operation value: {(int)operation}.");
        }

        switch (operation)
        {
            case BusinessObjectOperation.Create:
                if (previous is not null || consumedCurrent is not null)
                {
                    throw new LedgerStoreConflictException(
                        ValidationErrorCodes.InvalidBusinessObjectTransition,
                        $"BusinessObject {objectId} already exists. Create must not overwrite an existing current BusinessObject.");
                }

                if (version != 1)
                {
                    throw new LedgerStoreConflictException(
                        ValidationErrorCodes.InvalidBusinessObjectTransition,
                        $"BusinessObject {objectId} create must start at version 1.");
                }

                break;
            case BusinessObjectOperation.Update:
            case BusinessObjectOperation.Transfer:
                if (previous is null
                    || consumedCurrent is null
                    || !string.Equals(consumedCurrent.TxId, previous.CurrentOutPointTxId, StringComparison.Ordinal)
                    || consumedCurrent.OutputIndex != previous.CurrentOutPointIndex)
                {
                    throw new LedgerStoreConflictException(
                        ValidationErrorCodes.InvalidBusinessObjectTransition,
                        $"BusinessObject {objectId} {operation} must consume the current output for the same object id.");
                }

                int expectedVersion = previous.CurrentVersion + 1;
                if (version != expectedVersion)
                {
                    throw new LedgerStoreConflictException(
                        ValidationErrorCodes.InvalidBusinessObjectTransition,
                        $"BusinessObject {objectId} {operation} must advance from version {previous.CurrentVersion} to {expectedVersion}, not {version}.");
                }

                break;
            case BusinessObjectOperation.Close:
                throw new LedgerStoreConflictException(
                    ValidationErrorCodes.InvalidBusinessObjectTransition,
                    $"BusinessObject {objectId} close must consume the current output without emitting a next BusinessObject output.");
        }
    }

    private static async Task LockInputUtxosForUpdateAsync(EconomyDbContext db, IReadOnlyCollection<string> outPointKeys, CancellationToken cancellationToken)
    {
        if (outPointKeys.Count == 0 || !string.Equals(db.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            return;
        }

        string[] keys = outPointKeys.ToArray();
        await db.UnspentOutputs
            .FromSqlInterpolated($@"SELECT * FROM ""Economy_UnspentOutputs"" WHERE ""OutPointKey"" = ANY({keys}) FOR UPDATE")
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static LedgerTransaction MapTransaction(
        LedgerTransactionRecord row,
        IReadOnlyList<LedgerTransactionInputRecord> inputs,
        IReadOnlyList<LedgerTransactionOutputRecord> outputs)
    {
        TransactionData? data = row.DataItemType.HasValue
            || row.DataContentType is not null
            || row.DataHash is not null
            || row.InlineJson is not null
            || row.ExternalRef is not null
                ? new TransactionData(
                    row.DataContentType,
                    ToHashHex(row.DataHash),
                    row.InlineJson,
                    row.ExternalRef,
                    (TransactionDataItemType)(row.DataItemType ?? 0))
                : null;

        return new LedgerTransaction(
            new LedgerId(row.LedgerId),
            ToScenarioId(row.ScenarioId),
            row.Version,
            (TransactionKind)row.Kind,
            (SigningMode)row.SigningMode,
            row.Timestamp,
            inputs.Select(MapInput).ToArray(),
            outputs.Select(MapOutput).ToArray(),
            data,
            new TxId(row.TxId),
            ToHashHex(row.TxHash),
            (TransactionStatus)row.Status,
            DeserializeTokenDefinition(row.TokenDefinitionJson));
    }

    private static LedgerTransactionInputRecord ToInputRecord(TxId txId, TransactionInput input, string outPointKey)
        => new()
        {
            TxId = txId.Value,
            InputIndex = input.InputIndex,
            PreviousTxId = input.PreviousOutPoint.TxId.Value,
            PreviousOutputIndex = input.PreviousOutPoint.OutputIndex,
            Address = input.Address.Value,
            OutPointKey = outPointKey,
            SignaturePayloadHash = input.SignaturePayloadHash?.Value,
            SignatureBundleJson = SerializeSignatureBundle(input.SignatureBundle)
        };

    private LedgerTransactionOutputRecord ToOutputRecord(TxId txId, TransactionOutput output)
        => new()
        {
            TxId = txId.Value,
            OutputIndex = output.OutputIndex,
            Address = output.Address.Value,
            MainAmount = output.MainAmount.Value,
            TokenId = output.Token?.TokenId.Value,
            TokenAmount = output.Token?.Amount.Value,
            BusinessObjectId = output.BusinessObject?.ObjectId.Value,
            BusinessObjectVersion = output.BusinessObject?.Version,
            BusinessObjectOperation = output.BusinessObject is null ? null : (int)output.BusinessObject.Operation,
            BusinessObjectDataHash = output.BusinessObject?.DataHash.Value,
            MetadataHash = output.MetadataHash?.Value
        };

    private UnspentOutputRecord ToUnspentOutputRecord(LedgerTransaction tx, TxId txId, TransactionOutput output, DateTimeOffset createdAt)
        => new()
        {
            OutPointKey = CalculateOutPointKey(tx.LedgerId, tx.ScenarioId, new OutPoint(txId, output.OutputIndex), output.Address).Value.Value,
            LedgerId = tx.LedgerId.Value,
            ScenarioId = tx.ScenarioId?.Value,
            TxId = txId.Value,
            OutputIndex = output.OutputIndex,
            Address = output.Address.Value,
            MainAmount = output.MainAmount.Value,
            TokenId = output.Token?.TokenId.Value,
            TokenAmount = output.Token?.Amount.Value,
            BusinessObjectId = output.BusinessObject?.ObjectId.Value,
            BusinessObjectVersion = output.BusinessObject?.Version,
            BusinessObjectOperation = output.BusinessObject is null ? null : (int)output.BusinessObject.Operation,
            BusinessObjectDataHash = output.BusinessObject?.DataHash.Value,
            MetadataHash = output.MetadataHash?.Value,
            CreatedAt = createdAt
        };

    private static LedgerTransactionRecord ToTransactionRecord(ConfirmedTransaction confirmedTransaction, TxId txId, HashHex txHash)
    {
        LedgerTransaction tx = confirmedTransaction.Transaction;

        return new LedgerTransactionRecord
        {
            TxId = txId.Value,
            LedgerId = tx.LedgerId.Value,
            ScenarioId = tx.ScenarioId?.Value,
            LedgerSequence = confirmedTransaction.LedgerSequence,
            Version = tx.Version,
            Kind = (int)tx.Kind,
            SigningMode = (int)tx.SigningMode,
            Status = (int)TransactionStatus.Confirmed,
            Timestamp = tx.Timestamp,
            ConfirmedAt = confirmedTransaction.ConfirmedAt,
            PreviousLedgerHash = confirmedTransaction.PreviousLedgerHash.Value,
            LedgerEntryHash = confirmedTransaction.LedgerEntryHash.Value,
            TxHash = txHash.Value,
            DataItemType = tx.Data is null ? null : (int)tx.Data.ItemType,
            DataContentType = tx.Data?.ContentType,
            DataHash = tx.Data?.DataHash?.Value,
            InlineJson = tx.Data?.InlineJson,
            ExternalRef = tx.Data?.ExternalRef,
            TokenDefinitionJson = tx.TokenDefinition is null
                ? null
                : JsonSerializer.Serialize(tx.TokenDefinition, JsonOptions),
            AdminSignatureJson = SerializeSignatureBundle(confirmedTransaction.AdminSignature)
        };
    }

    private static RegisteredTokenDefinition MapRegisteredTokenDefinition(LedgerTokenTypeRecord row)
        => new(
            new TokenId(row.TokenId),
            row.ProtocolVersion,
            row.TokenNamespace,
            row.Symbol,
            string.IsNullOrWhiteSpace(row.IssuerAddress) ? null : new Address(row.IssuerAddress),
            row.InitialSupply.HasValue ? new Amount(row.InitialSupply.Value) : null,
            row.DecimalPlaces,
            (TokenClassification)row.Classification,
            string.IsNullOrWhiteSpace(row.DefinitionHash) ? null : new HashHex(row.DefinitionHash),
            row.DisplayName is null && row.Description is null && row.DisplayExternalRef is null
                ? null
                : new TokenDisplayMetadata(row.DisplayName, row.Description, row.DisplayExternalRef),
            new TxId(row.CreatedTxId),
            row.CreatedOutputIndex,
            row.CreatedAt);

    private static TokenDefinitionEnvelope? DeserializeTokenDefinition(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<TokenDefinitionEnvelope>(json, JsonOptions);

    private async Task<string> ResolveInputOutPointKeyAsync(
        EconomyDbContext db,
        LedgerTransaction tx,
        TransactionInput input,
        CancellationToken cancellationToken)
    {
        string? scenarioValue = tx.ScenarioId?.Value;
        UnspentOutputRecord? existing = await db.UnspentOutputs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.LedgerId == tx.LedgerId.Value
                    && x.ScenarioId == scenarioValue
                    && x.TxId == input.PreviousOutPoint.TxId.Value
                    && x.OutputIndex == input.PreviousOutPoint.OutputIndex
                    && x.Address == input.Address.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return existing?.OutPointKey
            ?? CalculateOutPointKey(tx.LedgerId, tx.ScenarioId, input.PreviousOutPoint, input.Address).Value.Value;
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

    private static TransactionInput MapInput(LedgerTransactionInputRecord row)
        => new(
            row.InputIndex,
            new OutPoint(new TxId(row.PreviousTxId), row.PreviousOutputIndex),
            new Address(row.Address),
            DeserializeSignatureBundle(row.SignatureBundleJson),
            ToHashHex(row.SignaturePayloadHash));

    private static TransactionOutput MapOutput(LedgerTransactionOutputRecord row)
        => new(
            row.OutputIndex,
            new Address(row.Address),
            ParseAmount(row.MainAmount),
            string.IsNullOrWhiteSpace(row.TokenId) ? null : new TokenTransfer(new TokenId(row.TokenId), ParseAmount(row.TokenAmount ?? 0m)),
            string.IsNullOrWhiteSpace(row.BusinessObjectId) ? null : new BusinessObjectRef(new BusinessObjectId(row.BusinessObjectId), row.BusinessObjectVersion ?? 0, (BusinessObjectOperation)(row.BusinessObjectOperation ?? 0), new HashHex(row.BusinessObjectDataHash ?? string.Empty)),
            ToHashHex(row.MetadataHash));

    private static UnspentOutput MapUnspentOutput(UnspentOutputRecord row)
        => new(
            new OutPointKey(new HashHex(row.OutPointKey)),
            new LedgerId(row.LedgerId),
            ToScenarioId(row.ScenarioId),
            new OutPoint(new TxId(row.TxId), row.OutputIndex),
            new Address(row.Address),
            ParseAmount(row.MainAmount),
            string.IsNullOrWhiteSpace(row.TokenId) ? null : new TokenTransfer(new TokenId(row.TokenId), ParseAmount(row.TokenAmount ?? 0m)),
            string.IsNullOrWhiteSpace(row.BusinessObjectId) ? null : new BusinessObjectRef(new BusinessObjectId(row.BusinessObjectId), row.BusinessObjectVersion ?? 0, (BusinessObjectOperation)(row.BusinessObjectOperation ?? 0), new HashHex(row.BusinessObjectDataHash ?? string.Empty)),
            row.CreatedAt);

    private static SpentOutput MapSpentOutput(SpentOutputRecord row)
        => new(
            new OutPointKey(new HashHex(row.OutPointKey)),
            new LedgerId(row.LedgerId),
            ToScenarioId(row.ScenarioId),
            new OutPoint(new TxId(row.TxId), row.OutputIndex),
            new Address(row.Address),
            new TxId(row.SpentInTxId),
            row.SpentAt,
            DeserializeSignatureBundle(row.AdminSignatureJson));

    private static string SerializeSignatureBundle(SignatureBundle bundle)
    {
        SignatureEnvelopeDto[] dto = bundle.Signatures
            .Select(x => new SignatureEnvelopeDto(
                (int)x.Algorithm,
                x.KeyId,
                x.PublicKeyHash.Value,
                x.PayloadHash.Value,
                Convert.ToBase64String(x.SignatureBytes),
                x.PublicKeyMaterial is null ? null : Convert.ToBase64String(x.PublicKeyMaterial)))
            .ToArray();

        return JsonSerializer.Serialize(dto, JsonOptions);
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

    private static ScenarioId? ToScenarioId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new ScenarioId(value);

    private static HashHex? ToHashHex(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new HashHex(value);

    private static Amount ParseAmount(decimal value)
        => new(value);

    private static string StateKey(LedgerId ledgerId, ScenarioId? scenarioId)
        => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}";

    private static string ProjectionKey(LedgerId ledgerId, ScenarioId? scenarioId, Address address)
        => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}|{address.Value}";

    private static string TokenTypeKey(LedgerId ledgerId, ScenarioId? scenarioId, TokenId tokenId)
        => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}|{tokenId.Value}";

    private sealed record SignatureEnvelopeDto(
        int Algorithm,
        string KeyId,
        string PublicKeyHash,
        string PayloadHash,
        string SignatureBase64,
        string? PublicKeyMaterialBase64 = null);
}
