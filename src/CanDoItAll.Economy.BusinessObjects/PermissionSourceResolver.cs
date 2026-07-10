using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

internal static class PermissionSourceResolver
{
    public static async Task<IReadOnlyList<LedgerPermissionSource>> GetPermissionSourcesAsync(
        ILedgerReadClient ledgerReadClient,
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await ledgerReadClient
            .GetSpendableOutputsAsync(query, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await ledgerReadClient
            .GetTransactionsAsync(query, includeData: true, cancellationToken)
            .ConfigureAwait(false);

        return Resolve(spendableOutputs, transactions);
    }

    public static IReadOnlyList<LedgerPermissionSource> Resolve(
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs,
        IReadOnlyList<LedgerTransactionSummary> transactions)
    {
        Dictionary<HashHex, TransactionData> dataByHash = transactions
            .Where(transaction => transaction.Data?.DataHash is not null)
            .Select(transaction => transaction.Data!)
            .GroupBy(data => data.DataHash!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        return spendableOutputs
            .Where(output => PermissionTokenAuthority.IsPermissionToken(output.Token)
                && output.BusinessObject is not null)
            .Select(output => TryResolve(output, dataByHash))
            .Where(source => source is not null)
            .Select(source => source!)
            .OrderBy(source => source.Grant.Namespace, StringComparer.Ordinal)
            .ThenBy(source => source.Output.CreatedAt)
            .ThenBy(source => source.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(source => source.OutPoint.OutputIndex)
            .ToArray();
    }

    public static LedgerPermissionSource SelectMintPermissionSource(
        IReadOnlyList<LedgerPermissionSource> permissionSources,
        TokenId tokenId,
        OutPoint? sourceOutPoint)
    {
        IEnumerable<LedgerPermissionSource> candidates = permissionSources.Where(source => source.AllowsMint(tokenId));
        if (sourceOutPoint is not null)
        {
            return candidates.FirstOrDefault(source => source.OutPoint == sourceOutPoint)
                ?? throw new InvalidOperationException(
                    $"Permission source UTXO {sourceOutPoint} is not spendable by this account or does not authorize {tokenId.Value}.");
        }

        return candidates
            .OrderBy(source => source.Grant.Namespace.Length)
            .ThenBy(source => source.Output.CreatedAt)
            .ThenBy(source => source.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(source => source.OutPoint.OutputIndex)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Account does not have a permission source for minting token {tokenId.Value}.");
    }

    public static LedgerPermissionSource SelectMintPermissionSource(
        IReadOnlyList<LedgerPermissionSource> permissionSources,
        string tokenNamespace,
        string tokenLabel,
        OutPoint? sourceOutPoint)
    {
        string normalizedNamespace = PermissionTokenAuthority.NormalizeNamespace(tokenNamespace);
        IEnumerable<LedgerPermissionSource> candidates = permissionSources
            .Where(source => source.AllowsMintNamespace(normalizedNamespace));
        if (sourceOutPoint is not null)
        {
            return candidates.FirstOrDefault(source => source.OutPoint == sourceOutPoint)
                ?? throw new InvalidOperationException(
                    $"Permission source UTXO {sourceOutPoint} is not spendable by this account or does not authorize {tokenLabel} in namespace {normalizedNamespace}.");
        }

        return candidates
            .OrderBy(source => source.Grant.Namespace.Length)
            .ThenBy(source => source.Output.CreatedAt)
            .ThenBy(source => source.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(source => source.OutPoint.OutputIndex)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Account does not have a permission source for minting {tokenLabel} in namespace {normalizedNamespace}.");
    }

    public static LedgerPermissionSource SelectRuleCreationPermissionSource(
        IReadOnlyList<LedgerPermissionSource> permissionSources,
        string ruleNamespace,
        OutPoint? sourceOutPoint)
    {
        string normalizedNamespace = PermissionTokenAuthority.NormalizeNamespace(ruleNamespace);
        IEnumerable<LedgerPermissionSource> candidates = permissionSources
            .Where(source => source.AllowsRuleCreation(normalizedNamespace) && source.Amount.Value >= 1m);
        if (sourceOutPoint is not null)
        {
            return candidates.FirstOrDefault(source => source.OutPoint == sourceOutPoint)
                ?? throw new InvalidOperationException(
                    $"Permission source UTXO {sourceOutPoint} is not spendable by this account or does not authorize rule creation for {normalizedNamespace}.");
        }

        return candidates
            .OrderBy(source => source.Grant.Namespace.Length)
            .ThenBy(source => source.Output.CreatedAt)
            .ThenBy(source => source.OutPoint.TxId.Value, StringComparer.Ordinal)
            .ThenBy(source => source.OutPoint.OutputIndex)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Account does not have a permission source for creating rules in namespace {normalizedNamespace}.");
    }

    private static LedgerPermissionSource? TryResolve(
        LedgerSpendableOutput output,
        IReadOnlyDictionary<HashHex, TransactionData> dataByHash)
    {
        BusinessObjectRef? businessObject = output.BusinessObject;
        if (businessObject is null
            || !dataByHash.TryGetValue(businessObject.DataHash, out TransactionData? data)
            || !PermissionTokenAuthority.TryParseData(data, businessObject, out PermissionGrant grant))
        {
            return null;
        }

        return new LedgerPermissionSource(output, grant);
    }
}
