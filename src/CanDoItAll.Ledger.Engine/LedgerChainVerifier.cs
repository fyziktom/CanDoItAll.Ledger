using System.Text;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Engine;

public sealed class LedgerChainVerifier(
    ITransactionHasher hasher,
    ISignaturePayloadBuilder payloadBuilder,
    ISignatureProviderRegistry? signatureProviderRegistry = null,
    IReadOnlyCollection<LedgerAdminVerificationKey>? trustedAdminKeys = null)
{
    private static readonly HashHex GenesisPreviousHash = new(new string('0', 64));
    private readonly IReadOnlyDictionary<string, LedgerAdminVerificationKey> trustedAdminKeysBySignature =
        (trustedAdminKeys ?? Array.Empty<LedgerAdminVerificationKey>())
        .ToDictionary(AdminKeyLookupKey, StringComparer.Ordinal);

    private bool RequiresAdminCryptographicVerification => trustedAdminKeysBySignature.Count > 0;

    public LedgerChainVerificationReport Verify(IEnumerable<ConfirmedTransaction> transactions)
    {
        ConfirmedTransaction[] ordered = transactions
            .OrderBy(x => x.LedgerSequence)
            .ToArray();

        var issues = new List<LedgerChainVerificationIssue>();
        LedgerId? ledgerId = null;
        ScenarioId? scenarioId = null;
        HashHex expectedPreviousHash = GenesisPreviousHash;
        long expectedSequence = 1;

        foreach (ConfirmedTransaction confirmed in ordered)
        {
            LedgerTransaction transaction = confirmed.Transaction;
            TxId? actualTxId = transaction.TxId;
            HashHex? actualTxHash = transaction.TxHash;

            ledgerId ??= transaction.LedgerId;
            scenarioId ??= transaction.ScenarioId;

            if (ledgerId != transaction.LedgerId || scenarioId != transaction.ScenarioId)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.MixedLedgerHistory,
                    confirmed,
                    "Confirmed history contains more than one ledger or scenario.");
            }

            if (confirmed.LedgerSequence != expectedSequence)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.SequenceMismatch,
                    confirmed,
                    $"Expected ledger sequence {expectedSequence}, found {confirmed.LedgerSequence}.");
            }

            if (confirmed.PreviousLedgerHash != expectedPreviousHash)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.PreviousLedgerHashMismatch,
                    confirmed,
                    $"Expected previous ledger hash {expectedPreviousHash.Value}, found {confirmed.PreviousLedgerHash.Value}.");
            }

            TxId expectedTxId = hasher.CalculateTxId(transaction);
            HashHex expectedTxHash = hasher.CalculateTxHash(transaction);

            if (actualTxId != expectedTxId)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.TxIdMismatch,
                    confirmed,
                    $"Expected TxId {expectedTxId.Value}, found {actualTxId?.Value ?? "<null>"}.");
            }

            if (actualTxHash != expectedTxHash)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.TxHashMismatch,
                    confirmed,
                    $"Expected TxHash {expectedTxHash.Value}, found {actualTxHash?.Value ?? "<null>"}.");
            }

            LedgerTransaction canonicalTransaction = transaction with
            {
                TxId = expectedTxId,
                TxHash = expectedTxHash
            };
            ConfirmedTransaction canonicalConfirmed = confirmed with
            {
                Transaction = canonicalTransaction
            };
            HashHex expectedLedgerEntryHash = payloadBuilder.BuildLedgerEntryPayloadHash(canonicalConfirmed);

            if (confirmed.LedgerEntryHash != expectedLedgerEntryHash)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.LedgerEntryHashMismatch,
                    confirmed,
                    $"Expected ledger entry hash {expectedLedgerEntryHash.Value}, found {confirmed.LedgerEntryHash.Value}.");
            }

            VerifyInputPayloads(canonicalTransaction, confirmed, issues);
            VerifyAdminSignaturePayloads(confirmed, expectedLedgerEntryHash, issues);

            expectedPreviousHash = expectedLedgerEntryHash;
            expectedSequence++;
        }

        return new LedgerChainVerificationReport(
            issues.Count == 0,
            issues,
            ordered.Length,
            ledgerId,
            scenarioId,
            ordered.Length == 0 ? 0 : ordered.Max(x => x.LedgerSequence),
            ordered.Length == 0 ? GenesisPreviousHash : expectedPreviousHash);
    }

    private void VerifyInputPayloads(
        LedgerTransaction transaction,
        ConfirmedTransaction confirmed,
        List<LedgerChainVerificationIssue> issues)
    {
        foreach (TransactionInput input in transaction.Inputs.OrderBy(x => x.InputIndex))
        {
            HashHex expectedPayloadHash = transaction.SigningMode == SigningMode.Ordered
                ? payloadBuilder.BuildOrderedInputPayloadHash(transaction, input)
                : payloadBuilder.BuildIndependentInputPayloadHash(transaction, input);

            if (input.SignaturePayloadHash != expectedPayloadHash)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.InputSignaturePayloadHashMismatch,
                    confirmed,
                    $"Input {input.InputIndex} expected signature payload hash {expectedPayloadHash.Value}, found {input.SignaturePayloadHash?.Value ?? "<null>"}.");
            }

            foreach (SignatureEnvelope signature in input.SignatureBundle.Signatures)
            {
                if (signature.PayloadHash == expectedPayloadHash)
                {
                    continue;
                }

                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.InputSignaturePayloadHashMismatch,
                    confirmed,
                    $"Input {input.InputIndex} signature envelope expected payload hash {expectedPayloadHash.Value}, found {signature.PayloadHash.Value}.");
            }
        }
    }

    private void VerifyAdminSignaturePayloads(
        ConfirmedTransaction confirmed,
        HashHex expectedLedgerEntryHash,
        List<LedgerChainVerificationIssue> issues)
    {
        if (confirmed.AdminSignature.Signatures.Count == 0)
        {
            AddIssue(
                issues,
                LedgerChainVerificationIssueCode.MissingAdminSignature,
                confirmed,
                "Confirmed transaction is missing an admin signature.");
            return;
        }

        var verifiedTrustedSignature = false;

        foreach (SignatureEnvelope signature in confirmed.AdminSignature.Signatures)
        {
            if (signature.PayloadHash != expectedLedgerEntryHash)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.AdminSignaturePayloadHashMismatch,
                    confirmed,
                    $"Admin signature expected payload hash {expectedLedgerEntryHash.Value}, found {signature.PayloadHash.Value}.");
                continue;
            }

            if (!RequiresAdminCryptographicVerification)
            {
                continue;
            }

            if (!trustedAdminKeysBySignature.TryGetValue(AdminKeyLookupKey(signature), out LedgerAdminVerificationKey? trustedKey))
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.UntrustedAdminSignature,
                    confirmed,
                    $"Admin signature key '{signature.KeyId}' with algorithm '{signature.Algorithm}' is not trusted for ledger verification.");
                continue;
            }

            if (signature.PublicKeyMaterial is { Length: > 0 } signaturePublicKey
                && !signaturePublicKey.SequenceEqual(trustedKey.PublicKeyMaterial))
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.AdminSignaturePublicKeyMismatch,
                    confirmed,
                    $"Admin signature key '{signature.KeyId}' includes public key material that does not match the trusted key.");
                continue;
            }

            if (signatureProviderRegistry is null)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.AdminSignatureVerificationUnavailable,
                    confirmed,
                    "Admin signature cryptographic verification requires a signature provider registry.");
                continue;
            }

            try
            {
                ISignatureProvider provider = signatureProviderRegistry.Get(signature.Algorithm);
                byte[] payload = Encoding.UTF8.GetBytes(expectedLedgerEntryHash.Value);
                if (!provider.Verify(payload, signature.SignatureBytes, trustedKey.PublicKeyMaterial))
                {
                    AddIssue(
                        issues,
                        LedgerChainVerificationIssueCode.AdminSignatureCryptographicVerificationFailed,
                        confirmed,
                        $"Admin signature key '{signature.KeyId}' failed cryptographic verification.");
                    continue;
                }

                verifiedTrustedSignature = true;
            }
            catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException or InvalidOperationException or ArgumentException)
            {
                AddIssue(
                    issues,
                    LedgerChainVerificationIssueCode.AdminSignatureVerificationUnavailable,
                    confirmed,
                    $"Admin signature key '{signature.KeyId}' could not be verified: {ex.Message}");
            }
        }

        if (RequiresAdminCryptographicVerification && !verifiedTrustedSignature)
        {
            AddIssue(
                issues,
                LedgerChainVerificationIssueCode.AdminSignatureCryptographicVerificationFailed,
                confirmed,
                "Confirmed transaction does not contain a trusted admin signature that verifies cryptographically.");
        }
    }

    private static string AdminKeyLookupKey(SignatureEnvelope signature)
        => AdminKeyLookupKey(signature.KeyId, signature.Algorithm, signature.PublicKeyHash);

    private static string AdminKeyLookupKey(LedgerAdminVerificationKey key)
        => AdminKeyLookupKey(key.KeyId, key.Algorithm, key.PublicKeyHash);

    private static string AdminKeyLookupKey(string keyId, SignatureAlgorithmId algorithm, HashHex publicKeyHash)
        => string.Join('|', keyId, ((int)algorithm).ToString(System.Globalization.CultureInfo.InvariantCulture), publicKeyHash.Value);

    private static void AddIssue(
        List<LedgerChainVerificationIssue> issues,
        LedgerChainVerificationIssueCode code,
        ConfirmedTransaction confirmed,
        string message)
        => issues.Add(new LedgerChainVerificationIssue(
            code,
            confirmed.LedgerSequence,
            confirmed.Transaction.TxId,
            message));
}

public sealed record LedgerChainVerificationReport(
    bool IsValid,
    IReadOnlyList<LedgerChainVerificationIssue> Issues,
    int TransactionCount,
    LedgerId? LedgerId,
    ScenarioId? ScenarioId,
    long HighestSequence,
    HashHex VerifiedLedgerHash);

public sealed record LedgerChainVerificationIssue(
    LedgerChainVerificationIssueCode Code,
    long LedgerSequence,
    TxId? TxId,
    string Message);

public sealed record LedgerAdminVerificationKey(
    string KeyId,
    SignatureAlgorithmId Algorithm,
    HashHex PublicKeyHash,
    byte[] PublicKeyMaterial);

public enum LedgerChainVerificationIssueCode
{
    MixedLedgerHistory,
    SequenceMismatch,
    PreviousLedgerHashMismatch,
    TxIdMismatch,
    TxHashMismatch,
    LedgerEntryHashMismatch,
    InputSignaturePayloadHashMismatch,
    MissingAdminSignature,
    AdminSignaturePayloadHashMismatch,
    UntrustedAdminSignature,
    AdminSignaturePublicKeyMismatch,
    AdminSignatureVerificationUnavailable,
    AdminSignatureCryptographicVerificationFailed
}
