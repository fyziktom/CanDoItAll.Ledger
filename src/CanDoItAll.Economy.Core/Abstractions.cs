namespace CanDoItAll.Economy.Core;

public interface ITransactionCanonicalizer
{
    byte[] GetTransactionCoreBytes(LedgerTransaction transaction);

    byte[] GetSubmittedTransactionBytes(LedgerTransaction transaction);
}

public interface ITransactionHasher
{
    TxId CalculateTxId(LedgerTransaction transaction);

    HashHex CalculateTxHash(LedgerTransaction transaction);

    OutPointKey CalculateOutPointKey(LedgerId ledgerId, ScenarioId? scenarioId, OutPoint outPoint, Address address);
}

public interface ISignaturePayloadBuilder
{
    HashHex BuildIndependentInputPayloadHash(LedgerTransaction transaction, TransactionInput input);

    HashHex BuildOrderedInputPayloadHash(LedgerTransaction transaction, TransactionInput input);

    HashHex BuildLedgerEntryPayloadHash(ConfirmedTransaction confirmedTransaction);
}

public interface ILedgerStore
{
    Task<LedgerState?> GetLedgerStateAsync(LedgerId ledgerId, ScenarioId? scenarioId, CancellationToken cancellationToken);

    Task<RegisteredAddress?> GetRegisteredAddressAsync(LedgerId ledgerId, Address address, CancellationToken cancellationToken);

    Task<LedgerTransaction?> GetTransactionAsync(LedgerId ledgerId, ScenarioId? scenarioId, TxId txId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<OutPointKey, UnspentOutput>> GetUnspentOutputsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        IReadOnlyList<OutPointKey> outPointKeys,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<OutPointKey, SpentOutput>> GetSpentOutputsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        IReadOnlyList<OutPointKey> outPointKeys,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UnspentOutput>> GetUnspentOutputsByAddressAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        Address address,
        CancellationToken cancellationToken);

    Task<bool> TokenExistsAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        TokenId tokenId,
        CancellationToken cancellationToken);

    Task AppendConfirmedTransactionAsync(ConfirmedTransaction confirmedTransaction, CancellationToken cancellationToken);
}

public sealed record LedgerConfirmationContext(
    LedgerState? State,
    LedgerTransaction? ExistingTransaction,
    IReadOnlyDictionary<OutPointKey, UnspentOutput> UnspentOutputs,
    IReadOnlyDictionary<OutPointKey, SpentOutput> SpentOutputs);

public sealed record LedgerConfirmationDecision(
    ConfirmedTransaction? ConfirmedTransaction,
    IReadOnlyList<ValidationError> Errors)
{
    public bool Accepted => ConfirmedTransaction is not null && Errors.Count == 0;

    public static LedgerConfirmationDecision Confirmed(ConfirmedTransaction confirmedTransaction)
        => new(confirmedTransaction, Array.Empty<ValidationError>());

    public static LedgerConfirmationDecision Rejected(IReadOnlyList<ValidationError> errors)
        => new(null, errors);
}

public interface ILedgerConfirmationStore : ILedgerStore
{
    Task<LedgerSubmitResult> ConfirmTransactionAsync(
        LedgerTransaction normalizedTransaction,
        IReadOnlyList<OutPointKey> inputKeys,
        Func<LedgerConfirmationContext, CancellationToken, Task<LedgerConfirmationDecision>> confirm,
        CancellationToken cancellationToken);
}

public interface ILedgerAdminSigner
{
    SignatureBundle Sign(HashHex payloadHash);
}

public interface ITimeProvider
{
    DateTimeOffset UtcNow { get; }
}
