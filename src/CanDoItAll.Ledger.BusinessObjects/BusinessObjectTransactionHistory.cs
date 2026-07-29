using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.BusinessObjects;

public sealed record BusinessObjectVersionTransaction(
    BusinessObjectId ObjectId,
    LedgerTransactionSummary Transaction,
    LedgerTransactionOutputSummary Output);

/// <summary>
/// Resolves a BusinessObject version chain from canonical output ids and structured
/// previous-version references. Internal V1/V2 lineage is used only as an in-process
/// compatibility fallback and is never required by public read responses.
/// </summary>
public static class BusinessObjectTransactionHistory
{
    public static IReadOnlyList<BusinessObjectVersionTransaction> SelectVersions(
        BusinessObjectId currentObjectId,
        IReadOnlyList<LedgerTransactionSummary> transactions,
        bool transactionsAreLineageFiltered = false)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        if (!currentObjectId.IsCanonicalOutPointId())
        {
            return [];
        }

        BusinessObjectVersionTransaction[] candidates = transactions
            .SelectMany(transaction => transaction.Outputs
                .Where(output => output.HasBusinessObject)
                .Select(output => new BusinessObjectVersionTransaction(
                    BusinessObjectId.FromOutPoint(transaction.TxId, output.OutputIndex),
                    transaction,
                    output)))
            .ToArray();

        if (transactionsAreLineageFiltered)
        {
            BusinessObjectVersionTransaction[] boundVersions = candidates
                .Where(IsBoundToTransactionData)
                .ToArray();
            return boundVersions.Length > 0 ? Order(boundVersions) : Order(candidates);
        }

        BusinessObjectVersionTransaction? current = candidates
            .FirstOrDefault(candidate => candidate.ObjectId == currentObjectId);
        BusinessObjectLineageKey? internalLineage = current?.Output.BusinessObject?.LineageKey;
        if (internalLineage is not null)
        {
            return Order(candidates.Where(candidate =>
                candidate.Output.BusinessObject?.LineageKey == internalLineage.Value));
        }

        Dictionary<BusinessObjectId, BusinessObjectVersionTransaction> byObjectId = candidates
            .GroupBy(candidate => candidate.ObjectId)
            .ToDictionary(group => group.Key, group => group.First());
        var selected = new List<BusinessObjectVersionTransaction>();
        var visited = new HashSet<BusinessObjectId>();
        BusinessObjectId nextObjectId = currentObjectId;
        while (visited.Add(nextObjectId)
            && byObjectId.TryGetValue(nextObjectId, out BusinessObjectVersionTransaction? version))
        {
            selected.Add(version);
            BusinessObjectId? previousObjectId = PreviousVersionObjectId(version.Transaction);
            if (previousObjectId is null)
            {
                break;
            }

            nextObjectId = previousObjectId.Value;
        }

        return Order(selected);
    }

    private static bool IsBoundToTransactionData(BusinessObjectVersionTransaction version)
        => version.Transaction.Data?.DataHash is not { } dataHash
            || version.Output.BusinessObjectDataHash == dataHash;

    private static BusinessObjectId? PreviousVersionObjectId(LedgerTransactionSummary transaction)
    {
        if (!BusinessObjectPayloadEnvelopeReader.TryRead(
                transaction.Data?.InlineJson,
                out BusinessObjectPayloadEnvelope envelope))
        {
            return null;
        }

        return envelope.References
            .Where(reference => string.Equals(
                reference.Relationship,
                BusinessObjectReferenceRelationships.PreviousVersion,
                StringComparison.Ordinal))
            .Select(reference => (BusinessObjectId?)reference.ObjectId)
            .FirstOrDefault();
    }

    private static IReadOnlyList<BusinessObjectVersionTransaction> Order(
        IEnumerable<BusinessObjectVersionTransaction> versions)
        => versions
            .OrderBy(version => version.Output.BusinessObjectVersion ?? int.MaxValue)
            .ThenBy(version => version.Transaction.Timestamp)
            .ThenBy(version => version.Transaction.TxId.Value, StringComparer.Ordinal)
            .ToArray();
}
