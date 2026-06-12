using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Persistence.EFCore;

public sealed class EfLedgerHistoryReader(IDbContextFactory<EconomyDbContext> dbContextFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<IReadOnlyList<ConfirmedTransaction>> ReadConfirmedTransactionsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        await using EconomyDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
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

        return transactions
            .Select(tx => ToConfirmedTransaction(
                tx,
                inputsByTxId.GetValueOrDefault(tx.TxId) ?? [],
                outputsByTxId.GetValueOrDefault(tx.TxId) ?? []))
            .ToArray();
    }

    private static ConfirmedTransaction ToConfirmedTransaction(
        LedgerTransactionRecord row,
        IReadOnlyList<LedgerTransactionInputRecord> inputs,
        IReadOnlyList<LedgerTransactionOutputRecord> outputs)
    {
        if (row.LedgerSequence is null)
        {
            throw new InvalidOperationException($"Confirmed transaction {row.TxId} is missing ledger sequence.");
        }

        if (string.IsNullOrWhiteSpace(row.PreviousLedgerHash))
        {
            throw new InvalidOperationException($"Confirmed transaction {row.TxId} is missing previous ledger hash.");
        }

        if (string.IsNullOrWhiteSpace(row.LedgerEntryHash))
        {
            throw new InvalidOperationException($"Confirmed transaction {row.TxId} is missing ledger entry hash.");
        }

        if (row.ConfirmedAt is null)
        {
            throw new InvalidOperationException($"Confirmed transaction {row.TxId} is missing confirmation timestamp.");
        }

        return new ConfirmedTransaction(
            MapTransaction(row, inputs, outputs),
            row.LedgerSequence.Value,
            new HashHex(row.PreviousLedgerHash),
            new HashHex(row.LedgerEntryHash),
            DeserializeSignatureBundle(row.AdminSignatureJson),
            row.ConfirmedAt.Value);
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
                    row.DataItemType.HasValue ? (TransactionDataItemType)row.DataItemType.Value : TransactionDataItemType.Generic)
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
            (TransactionStatus)row.Status);
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
            string.IsNullOrWhiteSpace(row.BusinessObjectId) ? null : new BusinessObjectRef(
                new BusinessObjectId(row.BusinessObjectId),
                row.BusinessObjectVersion ?? 0,
                (BusinessObjectOperation)(row.BusinessObjectOperation ?? 0),
                new HashHex(row.BusinessObjectDataHash ?? string.Empty)),
            ToHashHex(row.MetadataHash));

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

    private sealed record SignatureEnvelopeDto(
        int Algorithm,
        string KeyId,
        string PublicKeyHash,
        string PayloadHash,
        string SignatureBase64,
        string? PublicKeyMaterialBase64);
}
