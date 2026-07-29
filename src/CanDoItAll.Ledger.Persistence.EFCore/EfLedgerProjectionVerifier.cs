using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Ledger.Persistence.EFCore;

public sealed class EfLedgerProjectionVerifier(IDbContextFactory<LedgerDbContext> dbContextFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashHex EmptyHash = new(new string('0', 64));

    public async Task<EfLedgerProjectionVerificationReport> VerifyAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        await using LedgerDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        ProjectionRows rows = await LoadRowsAsync(db, ledgerId, scenarioId, cancellationToken).ConfigureAwait(false);
        EfLedgerProjectionHashSet hashSet = CalculateHashSet(ledgerId, scenarioId, rows);
        var issues = new List<EfLedgerProjectionVerificationIssue>();

        VerifyLedgerState(rows, issues);
        VerifyAddressProjections(ledgerId, scenarioId, rows, issues);

        return new EfLedgerProjectionVerificationReport(
            issues.Count == 0,
            hashSet,
            issues);
    }

    public async Task<EfLedgerProjectionHashSet> CalculateHashSetAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        await using LedgerDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        ProjectionRows rows = await LoadRowsAsync(db, ledgerId, scenarioId, cancellationToken).ConfigureAwait(false);
        return CalculateHashSet(ledgerId, scenarioId, rows);
    }

    private static void VerifyLedgerState(
        ProjectionRows rows,
        List<EfLedgerProjectionVerificationIssue> issues)
    {
        LedgerTransactionRecord? latest = rows.Transactions
            .OrderByDescending(x => x.LedgerSequence ?? 0)
            .FirstOrDefault();

        if (latest is null)
        {
            return;
        }

        if (rows.LedgerState is null)
        {
            AddIssue(
                issues,
                EfLedgerProjectionVerificationIssueCode.MissingLedgerState,
                "ledger-state",
                "Ledger state projection is missing for confirmed history.");
            return;
        }

        if (rows.LedgerState.CurrentSequence != latest.LedgerSequence)
        {
            AddIssue(
                issues,
                EfLedgerProjectionVerificationIssueCode.LedgerStateMismatch,
                "ledger-state",
                $"Ledger state sequence {rows.LedgerState.CurrentSequence} does not match latest confirmed sequence {latest.LedgerSequence}.");
        }

        if (!string.Equals(rows.LedgerState.CurrentLedgerHash, latest.LedgerEntryHash, StringComparison.Ordinal))
        {
            AddIssue(
                issues,
                EfLedgerProjectionVerificationIssueCode.LedgerStateMismatch,
                "ledger-state",
                $"Ledger state hash {rows.LedgerState.CurrentLedgerHash} does not match latest confirmed ledger hash {latest.LedgerEntryHash}.");
        }
    }

    private static void VerifyAddressProjections(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        ProjectionRows rows,
        List<EfLedgerProjectionVerificationIssue> issues)
    {
        Dictionary<string, long> touchesByAddress = CalculateAddressTouches(rows);
        HashSet<string> expectedAddresses = touchesByAddress.Keys
            .Concat(rows.AddressProjections.Select(x => x.Address))
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, AddressStateProjectionRecord> projectionsByAddress = rows.AddressProjections
            .ToDictionary(x => x.Address, StringComparer.Ordinal);

        foreach (string address in expectedAddresses.Order(StringComparer.Ordinal))
        {
            if (!projectionsByAddress.TryGetValue(address, out AddressStateProjectionRecord? projection))
            {
                AddIssue(
                    issues,
                    EfLedgerProjectionVerificationIssueCode.MissingAddressProjection,
                    address,
                    $"Address projection is missing for {address}.");
                continue;
            }

            UnspentOutputRecord[] addressRows = rows.UnspentOutputs
                .Where(x => string.Equals(x.Address, address, StringComparison.Ordinal))
                .ToArray();
            EfProjectionBalance expectedBalance = EfLedgerProjectionCalculator.CalculateBalance(addressRows);
            long expectedSequence = touchesByAddress.GetValueOrDefault(address);
            string expectedTokenBalancesJson = JsonSerializer.Serialize(expectedBalance.TokenBalances, JsonOptions);
            HashHex expectedStateHash = EfLedgerProjectionCalculator.CalculateAddressProjectionHash(
                ledgerId,
                scenarioId,
                address,
                expectedBalance.MainBalance,
                expectedBalance.TokenBalances,
                expectedBalance.UtxoCount,
                expectedSequence);

            if (projection.MainBalance != expectedBalance.MainBalance)
            {
                AddIssue(
                    issues,
                    EfLedgerProjectionVerificationIssueCode.AddressProjectionBalanceMismatch,
                    address,
                    $"Address {address} main balance {projection.MainBalance} does not match expected {expectedBalance.MainBalance}.");
            }

            if (projection.UtxoCount != expectedBalance.UtxoCount)
            {
                AddIssue(
                    issues,
                    EfLedgerProjectionVerificationIssueCode.AddressProjectionUtxoCountMismatch,
                    address,
                    $"Address {address} UTXO count {projection.UtxoCount} does not match expected {expectedBalance.UtxoCount}.");
            }

            if (!string.Equals(projection.TokenBalancesJson, expectedTokenBalancesJson, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    EfLedgerProjectionVerificationIssueCode.AddressProjectionTokenBalanceMismatch,
                    address,
                    $"Address {address} token balances do not match expected projection.");
            }

            if (projection.LastLedgerSequence != expectedSequence)
            {
                AddIssue(
                    issues,
                    EfLedgerProjectionVerificationIssueCode.AddressProjectionSequenceMismatch,
                    address,
                    $"Address {address} last ledger sequence {projection.LastLedgerSequence} does not match expected {expectedSequence}.");
            }

            if (!string.Equals(projection.StateHash, expectedStateHash.Value, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    EfLedgerProjectionVerificationIssueCode.AddressProjectionStateHashMismatch,
                    address,
                    $"Address {address} state hash {projection.StateHash} does not match expected {expectedStateHash.Value}.");
            }
        }
    }

    private static Dictionary<string, long> CalculateAddressTouches(ProjectionRows rows)
    {
        Dictionary<string, long> sequenceByTxId = rows.Transactions
            .Where(x => x.LedgerSequence.HasValue)
            .ToDictionary(x => x.TxId, x => x.LedgerSequence!.Value, StringComparer.Ordinal);
        var touches = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (LedgerTransactionInputRecord input in rows.Inputs)
        {
            if (sequenceByTxId.TryGetValue(input.TxId, out long sequence))
            {
                touches[input.Address] = Math.Max(touches.GetValueOrDefault(input.Address), sequence);
            }
        }

        foreach (LedgerTransactionOutputRecord output in rows.Outputs)
        {
            if (sequenceByTxId.TryGetValue(output.TxId, out long sequence))
            {
                touches[output.Address] = Math.Max(touches.GetValueOrDefault(output.Address), sequence);
            }
        }

        return touches;
    }

    private static EfLedgerProjectionHashSet CalculateHashSet(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        ProjectionRows rows)
    {
        long currentSequence = rows.LedgerState?.CurrentSequence
            ?? rows.Transactions.Select(x => x.LedgerSequence ?? 0).DefaultIfEmpty(0).Max();
        string currentLedgerHash = rows.LedgerState?.CurrentLedgerHash
            ?? rows.Transactions
                .OrderByDescending(x => x.LedgerSequence ?? 0)
                .Select(x => x.LedgerEntryHash)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? EmptyHash.Value;

        HashHex utxoHash = HashRows(
            "cdia.economy.projection.utxo.v1",
            rows.UnspentOutputs
                .OrderBy(x => x.OutPointKey, StringComparer.Ordinal)
                .Select(x => string.Join('|',
                    x.OutPointKey,
                    x.TxId,
                    x.OutputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.Address,
                    FormatAmount(x.MainAmount),
                    x.TokenId ?? string.Empty,
                    x.TokenAmount.HasValue ? FormatAmount(x.TokenAmount.Value) : string.Empty,
                    x.BusinessObjectId ?? string.Empty,
                    x.BusinessObjectVersion?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    x.BusinessObjectOperation?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    x.BusinessObjectDataHash ?? string.Empty,
                    x.MetadataHash ?? string.Empty)));
        HashHex spentHash = HashRows(
            "cdia.economy.projection.spent.v1",
            rows.SpentOutputs
                .OrderBy(x => x.OutPointKey, StringComparer.Ordinal)
                .Select(x => string.Join('|',
                    x.OutPointKey,
                    x.TxId,
                    x.OutputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.Address,
                    x.SpentInTxId)));
        HashHex addressHash = HashRows(
            "cdia.economy.projection.address.v1",
            rows.AddressProjections
                .OrderBy(x => x.ProjectionKey, StringComparer.Ordinal)
                .Select(x => string.Join('|',
                    x.ProjectionKey,
                    x.Address,
                    FormatAmount(x.MainBalance),
                    x.TokenBalancesJson,
                    x.UtxoCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.LastLedgerSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.StateHash)));
        HashHex tokenTypeHash = HashRows(
            "cdia.economy.projection.token-type.v1",
            rows.TokenTypes
                .OrderBy(x => x.TokenTypeKey, StringComparer.Ordinal)
                .Select(x => string.Join('|',
                    x.TokenTypeKey,
                    x.TokenId,
                    x.CreatedTxId,
                    x.CreatedOutputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        HashHex businessObjectHash = HashRows(
            "cdia.economy.projection.business-object.v1",
            rows.BusinessObjects
                .OrderBy(x => x.BusinessObjectKey, StringComparer.Ordinal)
                .Select(x => string.Join('|',
                    x.BusinessObjectKey,
                    x.ObjectId,
                    x.ObjectType,
                    x.CurrentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.CurrentOwnerAddress,
                    x.CurrentState,
                    x.CurrentHash,
                    x.CurrentOutPointTxId,
                    x.CurrentOutPointIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.CreatedTxId,
                    x.UpdatedTxId)));
        HashHex businessObjectVersionHash = HashRows(
            "cdia.economy.projection.business-object-version.v1",
            rows.BusinessObjectVersions
                .OrderBy(x => x.ObjectId, StringComparer.Ordinal)
                .ThenBy(x => x.Version)
                .Select(x => string.Join('|',
                    x.ObjectId,
                    x.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.TxId,
                    x.OutPointTxId,
                    x.OutPointIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    x.OwnerAddress,
                    x.State,
                    x.SchemaVersion,
                    x.DataHash,
                    x.DataJson ?? string.Empty)));
        HashHex projectionHash = HashRows(
            "cdia.economy.projection.all.v1",
            [
                ledgerId.Value,
                scenarioId?.Value ?? string.Empty,
                currentSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
                currentLedgerHash,
                utxoHash.Value,
                spentHash.Value,
                addressHash.Value,
                tokenTypeHash.Value,
                businessObjectHash.Value,
                businessObjectVersionHash.Value
            ]);

        return new EfLedgerProjectionHashSet(
            ledgerId,
            scenarioId,
            currentSequence,
            new HashHex(currentLedgerHash),
            utxoHash,
            spentHash,
            addressHash,
            tokenTypeHash,
            businessObjectHash,
            businessObjectVersionHash,
            projectionHash);
    }

    private static async Task<ProjectionRows> LoadRowsAsync(
        LedgerDbContext db,
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        string? scenarioValue = scenarioId?.Value;
        string stateKey = StateKey(ledgerId, scenarioId);
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

        return new ProjectionRows(
            transactions,
            await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
                txIds,
                batch => db.TransactionInputs.AsNoTracking().Where(x => batch.Contains(x.TxId)),
                cancellationToken).ConfigureAwait(false),
            await EfLedgerQueryBatcher.ToListByBatchedKeysAsync(
                txIds,
                batch => db.TransactionOutputs.AsNoTracking().Where(x => batch.Contains(x.TxId)),
                cancellationToken).ConfigureAwait(false),
            await db.UnspentOutputs.AsNoTracking().Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue).ToListAsync(cancellationToken).ConfigureAwait(false),
            await db.SpentOutputs.AsNoTracking().Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue).ToListAsync(cancellationToken).ConfigureAwait(false),
            await db.AddressStateProjections.AsNoTracking().Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue).ToListAsync(cancellationToken).ConfigureAwait(false),
            await db.LedgerTokenTypes.AsNoTracking().Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue).ToListAsync(cancellationToken).ConfigureAwait(false),
            await db.BusinessObjects.AsNoTracking().Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue).ToListAsync(cancellationToken).ConfigureAwait(false),
            await db.BusinessObjectVersions.AsNoTracking().Where(x => x.LedgerId == ledgerId.Value && x.ScenarioId == scenarioValue).ToListAsync(cancellationToken).ConfigureAwait(false),
            await db.LedgerStates.AsNoTracking().FirstOrDefaultAsync(x => x.StateKey == stateKey, cancellationToken).ConfigureAwait(false));
    }

    private static HashHex HashRows(string schema, IEnumerable<string> rows)
        => HashText(schema + "\n" + string.Join('\n', rows));

    private static HashHex HashText(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new HashHex(Convert.ToHexString(hash).ToLowerInvariant());
    }

    private static void AddIssue(
        List<EfLedgerProjectionVerificationIssue> issues,
        EfLedgerProjectionVerificationIssueCode code,
        string scope,
        string message)
        => issues.Add(new EfLedgerProjectionVerificationIssue(code, scope, message));

    private static string StateKey(LedgerId ledgerId, ScenarioId? scenarioId)
        => $"{ledgerId.Value}|{scenarioId?.Value ?? string.Empty}";

    private static string FormatAmount(decimal value)
        => new Amount(value).ToString();

    private sealed record ProjectionRows(
        IReadOnlyList<LedgerTransactionRecord> Transactions,
        IReadOnlyList<LedgerTransactionInputRecord> Inputs,
        IReadOnlyList<LedgerTransactionOutputRecord> Outputs,
        IReadOnlyList<UnspentOutputRecord> UnspentOutputs,
        IReadOnlyList<SpentOutputRecord> SpentOutputs,
        IReadOnlyList<AddressStateProjectionRecord> AddressProjections,
        IReadOnlyList<LedgerTokenTypeRecord> TokenTypes,
        IReadOnlyList<BusinessObjectRecord> BusinessObjects,
        IReadOnlyList<BusinessObjectVersionRecord> BusinessObjectVersions,
        LedgerStateRecord? LedgerState);
}

public sealed record EfLedgerProjectionHashSet(
    LedgerId LedgerId,
    ScenarioId? ScenarioId,
    long CurrentSequence,
    HashHex CurrentLedgerHash,
    HashHex UtxoSetHash,
    HashHex SpentSetHash,
    HashHex AddressStateHash,
    HashHex TokenTypeHash,
    HashHex BusinessObjectStateHash,
    HashHex BusinessObjectVersionHash,
    HashHex ProjectionHash);

public sealed record EfLedgerProjectionVerificationReport(
    bool IsValid,
    EfLedgerProjectionHashSet HashSet,
    IReadOnlyList<EfLedgerProjectionVerificationIssue> Issues);

public sealed record EfLedgerProjectionVerificationIssue(
    EfLedgerProjectionVerificationIssueCode Code,
    string Scope,
    string Message);

public enum EfLedgerProjectionVerificationIssueCode
{
    MissingLedgerState,
    LedgerStateMismatch,
    MissingAddressProjection,
    AddressProjectionBalanceMismatch,
    AddressProjectionTokenBalanceMismatch,
    AddressProjectionUtxoCountMismatch,
    AddressProjectionSequenceMismatch,
    AddressProjectionStateHashMismatch
}
