using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Engine;

public interface ILedgerSemanticRuleSet
{
    int TransactionVersion { get; }

    Task<ValidationResult> ValidateAsync(
        ConfirmedTransaction confirmed,
        ILedgerStore replayStore,
        CancellationToken cancellationToken);
}

public sealed class LedgerSemanticRuleSetRegistry
{
    private readonly IReadOnlyDictionary<int, ILedgerSemanticRuleSet> byVersion;

    public LedgerSemanticRuleSetRegistry(IEnumerable<ILedgerSemanticRuleSet> ruleSets)
    {
        ArgumentNullException.ThrowIfNull(ruleSets);
        ILedgerSemanticRuleSet[] materialized = ruleSets.ToArray();
        int[] duplicateVersions = materialized
            .GroupBy(rule => rule.TransactionVersion)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateVersions.Length > 0)
        {
            throw new ArgumentException(
                $"Semantic rule-set versions must be unique. Duplicates: {string.Join(", ", duplicateVersions)}.",
                nameof(ruleSets));
        }

        byVersion = materialized.ToDictionary(rule => rule.TransactionVersion);
    }

    public bool TryGet(int transactionVersion, out ILedgerSemanticRuleSet? ruleSet)
        => byVersion.TryGetValue(transactionVersion, out ruleSet);
}

public sealed class LedgerSemanticReplayVerifier(LedgerSemanticRuleSetRegistry registry)
{
    public async Task<LedgerSemanticReplayReport> VerifyAsync(
        IEnumerable<ConfirmedTransaction> transactions,
        ILedgerStore replayStore,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(replayStore);

        ConfirmedTransaction[] ordered = transactions.OrderBy(item => item.LedgerSequence).ToArray();
        var issues = new List<LedgerSemanticReplayIssue>();
        int replayed = 0;

        foreach (ConfirmedTransaction confirmed in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!registry.TryGet(confirmed.Transaction.Version, out ILedgerSemanticRuleSet? ruleSet))
            {
                issues.Add(new LedgerSemanticReplayIssue(
                    LedgerSemanticReplayIssueCode.UnsupportedTransactionVersion,
                    confirmed.LedgerSequence,
                    confirmed.Transaction.TxId,
                    null,
                    $"No semantic rule set is registered for transaction version {confirmed.Transaction.Version}."));
                break;
            }

            ValidationResult validation = await ruleSet!
                .ValidateAsync(confirmed, replayStore, cancellationToken)
                .ConfigureAwait(false);
            if (!validation.IsValid)
            {
                issues.AddRange(validation.Errors.Select(error => new LedgerSemanticReplayIssue(
                    LedgerSemanticReplayIssueCode.SemanticValidationFailed,
                    confirmed.LedgerSequence,
                    confirmed.Transaction.TxId,
                    error.Code,
                    error.Message)));
                break;
            }

            try
            {
                await replayStore.AppendConfirmedTransactionAsync(confirmed, cancellationToken).ConfigureAwait(false);
                replayed++;
            }
            catch (Exception ex) when (ex is LedgerStoreConflictException or InvalidOperationException)
            {
                issues.Add(new LedgerSemanticReplayIssue(
                    LedgerSemanticReplayIssueCode.ReplayStateConflict,
                    confirmed.LedgerSequence,
                    confirmed.Transaction.TxId,
                    ex is LedgerStoreConflictException conflict ? conflict.Code : null,
                    ex.Message));
                break;
            }
        }

        return new LedgerSemanticReplayReport(
            issues.Count == 0,
            issues,
            ordered.Length,
            replayed,
            replayed == 0 ? 0 : ordered[replayed - 1].LedgerSequence);
    }
}

public sealed record LedgerSemanticReplayReport(
    bool IsValid,
    IReadOnlyList<LedgerSemanticReplayIssue> Issues,
    int TransactionCount,
    int ReplayedTransactionCount,
    long HighestReplayedSequence);

public sealed record LedgerSemanticReplayIssue(
    LedgerSemanticReplayIssueCode Code,
    long LedgerSequence,
    TxId? TxId,
    string? ValidationCode,
    string Message);

public enum LedgerSemanticReplayIssueCode
{
    UnsupportedTransactionVersion,
    SemanticValidationFailed,
    ReplayStateConflict
}
