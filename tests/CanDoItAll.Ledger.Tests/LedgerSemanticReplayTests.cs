using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Storage.InMemory;

namespace CanDoItAll.Economy.Tests;

public sealed class LedgerSemanticReplayTests
{
    private static readonly LedgerId LedgerId = new("semantic-replay");
    private static readonly byte[] AlicePublicKey = Hex.Utf8("semantic-alice-public");
    private static readonly Address Alice = Address.FromPublicKey(AlicePublicKey);
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("semantic-bob-public"));

    [Fact]
    public async Task SemanticReplay_HashValidAssetImbalanceIsRejectedByVersionOneRules()
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        ISignatureProviderRegistry providers = new SignatureProviderRegistry([new AcceptingSignatureProvider()]);
        LedgerTransactionValidationPipeline pipeline = CreatePipeline(payloadBuilder, providers);
        var verifier = new LedgerSemanticReplayVerifier(
            new LedgerSemanticRuleSetRegistry([new LedgerV1SemanticRuleSet(hasher, pipeline)]));

        ConfirmedTransaction genesis = Confirm(
            new LedgerTransaction(
                LedgerId,
                null,
                Version: 1,
                TransactionKind.Genesis,
                SigningMode.Independent,
                DateTimeOffset.Parse("2026-07-10T00:00:00Z"),
                [],
                [new TransactionOutput(0, Alice, Amount.FromInt64(10))],
                null),
            sequence: 1,
            previousHash: new HashHex(new string('0', 64)),
            hasher,
            payloadBuilder);

        LedgerTransaction imbalanced = Sign(
            new LedgerTransaction(
                LedgerId,
                null,
                Version: 1,
                TransactionKind.Transfer,
                SigningMode.Independent,
                DateTimeOffset.Parse("2026-07-10T00:01:00Z"),
                [new TransactionInput(0, new OutPoint(genesis.Transaction.TxId!.Value, 0), Alice, SignatureBundle.Empty)],
                [new TransactionOutput(0, Bob, Amount.FromInt64(11))],
                null),
            hasher,
            payloadBuilder);
        ConfirmedTransaction invalid = Confirm(imbalanced, 2, genesis.LedgerEntryHash, hasher, payloadBuilder);
        ConfirmedTransaction[] history = [genesis, invalid];

        Assert.True(new LedgerChainVerifier(hasher, payloadBuilder).Verify(history).IsValid);
        LedgerSemanticReplayReport report = await verifier.VerifyAsync(
            history,
            new InMemoryLedgerStore(hasher),
            CancellationToken.None);

        Assert.False(report.IsValid);
        LedgerSemanticReplayIssue issue = Assert.Single(report.Issues);
        Assert.Equal(LedgerSemanticReplayIssueCode.SemanticValidationFailed, issue.Code);
        Assert.Equal(ValidationErrorCodes.AssetImbalance, issue.ValidationCode);
        Assert.Equal(1, report.ReplayedTransactionCount);
    }

    [Fact]
    public async Task SemanticReplay_UnsupportedVersionIsNotMappedToCurrentRules()
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        LedgerTransactionValidationPipeline pipeline = CreatePipeline(
            payloadBuilder,
            new SignatureProviderRegistry([new AcceptingSignatureProvider()]));
        var verifier = new LedgerSemanticReplayVerifier(
            new LedgerSemanticRuleSetRegistry([new LedgerV1SemanticRuleSet(hasher, pipeline)]));
        ConfirmedTransaction future = Confirm(
            new LedgerTransaction(
                LedgerId,
                null,
                Version: 99,
                TransactionKind.Genesis,
                SigningMode.Independent,
                DateTimeOffset.Parse("2026-07-10T00:00:00Z"),
                [],
                [new TransactionOutput(0, Alice, Amount.FromInt64(10))],
                null),
            1,
            new HashHex(new string('0', 64)),
            hasher,
            payloadBuilder);

        LedgerSemanticReplayReport report = await verifier.VerifyAsync(
            [future],
            new InMemoryLedgerStore(hasher),
            CancellationToken.None);

        Assert.False(report.IsValid);
        Assert.Equal(LedgerSemanticReplayIssueCode.UnsupportedTransactionVersion, Assert.Single(report.Issues).Code);
        Assert.Equal(0, report.ReplayedTransactionCount);
    }

    [Fact]
    public void SemanticRuleSetRegistry_DuplicateVersionIsRejected()
    {
        var rule = new StubRuleSet(1);

        Assert.Throws<ArgumentException>(() => new LedgerSemanticRuleSetRegistry([rule, rule]));
    }

    private static LedgerTransactionValidationPipeline CreatePipeline(
        ISignaturePayloadBuilder payloadBuilder,
        ISignatureProviderRegistry providers)
        => new(
            new InputReferenceValidator(),
            new BusinessObjectTransitionValidator(),
            new MainCurrencyIssuancePolicy(new MainCurrencyIssuancePolicyOptions()),
            DisabledTokenCreationPolicy.Instance,
            new InputSignatureValidator(payloadBuilder, providers),
            new AssetConservationValidator());

    private static LedgerTransaction Sign(
        LedgerTransaction transaction,
        ITransactionHasher hasher,
        ISignaturePayloadBuilder payloadBuilder)
    {
        LedgerTransaction withId = transaction with { TxId = hasher.CalculateTxId(transaction) };
        TransactionInput[] inputs = withId.Inputs.Select(input =>
        {
            HashHex payload = payloadBuilder.BuildIndependentInputPayloadHash(withId, input);
            return input with
            {
                SignaturePayloadHash = payload,
                SignatureBundle = new SignatureBundle([
                    new SignatureEnvelope(
                        SignatureAlgorithmId.EcdsaP256Sha256,
                        "semantic-alice",
                        Address.CalculatePublicKeyHash(AlicePublicKey),
                        payload,
                        Hex.Utf8("valid-signature"),
                        AlicePublicKey)
                ])
            };
        }).ToArray();
        return withId with { Inputs = inputs };
    }

    private static ConfirmedTransaction Confirm(
        LedgerTransaction transaction,
        long sequence,
        HashHex previousHash,
        ITransactionHasher hasher,
        ISignaturePayloadBuilder payloadBuilder)
    {
        TxId txId = hasher.CalculateTxId(transaction);
        LedgerTransaction normalized = transaction with
        {
            TxId = txId,
            TxHash = hasher.CalculateTxHash(transaction),
            Status = TransactionStatus.Confirmed
        };
        var temporary = new ConfirmedTransaction(
            normalized,
            sequence,
            previousHash,
            new HashHex(new string('0', 64)),
            SignatureBundle.Empty,
            transaction.Timestamp.AddSeconds(1));
        HashHex entryHash = payloadBuilder.BuildLedgerEntryPayloadHash(temporary);
        return temporary with
        {
            LedgerEntryHash = entryHash,
            AdminSignature = new SignatureBundle([
                new SignatureEnvelope(
                    SignatureAlgorithmId.EcdsaP256Sha256,
                    "semantic-admin",
                    new HashHex(new string('a', 64)),
                    entryHash,
                    Hex.Utf8("admin-signature"))
            ])
        };
    }

    private sealed class AcceptingSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;
        public bool IsSupported => true;
        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial) => Hex.Utf8("valid-signature");
        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial) => true;
    }

    private sealed class StubRuleSet(int version) : ILedgerSemanticRuleSet
    {
        public int TransactionVersion { get; } = version;

        public Task<ValidationResult> ValidateAsync(
            ConfirmedTransaction confirmed,
            ILedgerStore replayStore,
            CancellationToken cancellationToken)
            => Task.FromResult(ValidationResult.Success);
    }
}
