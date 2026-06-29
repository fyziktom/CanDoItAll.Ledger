using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.BusinessObjects;

public interface IAccountTransactionSigner
{
    Task<LedgerTransaction> SignAsync(
        LedgerTransaction unsignedTransaction,
        IAccountKeySource keySource,
        CancellationToken cancellationToken);
}

public sealed class AccountTransactionSigner(
    ITransactionHasher transactionHasher,
    ISignaturePayloadBuilder signaturePayloadBuilder,
    IAccountInputSigner inputSigner) : IAccountTransactionSigner
{
    public async Task<LedgerTransaction> SignAsync(
        LedgerTransaction unsignedTransaction,
        IAccountKeySource keySource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unsignedTransaction);
        ArgumentNullException.ThrowIfNull(keySource);

        TxId txId = transactionHasher.CalculateTxId(unsignedTransaction);
        LedgerTransaction withTxId = unsignedTransaction with { TxId = txId, TxHash = null };
        var inputs = withTxId.Inputs.OrderBy(input => input.InputIndex).ToList();

        for (var i = 0; i < inputs.Count; i++)
        {
            TransactionInput input = inputs[i];
            LedgerTransaction payloadTransaction = withTxId with { Inputs = inputs };
            HashHex payloadHash = payloadTransaction.SigningMode == SigningMode.Ordered
                ? signaturePayloadBuilder.BuildOrderedInputPayloadHash(payloadTransaction, input)
                : signaturePayloadBuilder.BuildIndependentInputPayloadHash(payloadTransaction, input);
            SignatureEnvelope signature = await inputSigner
                .SignAsync(keySource, payloadHash, cancellationToken)
                .ConfigureAwait(false);

            inputs[i] = input with
            {
                SignaturePayloadHash = payloadHash,
                SignatureBundle = new SignatureBundle(new[] { signature })
            };
        }

        return withTxId with { Inputs = inputs };
    }
}
