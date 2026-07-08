using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Storage.InMemory;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectTransitionValidationTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
    private static readonly BusinessObjectId ObjectId = new("settings");
    private static readonly TokenId CarrierToken = new("bo.carrier");

    [Fact]
    public async Task LedgerExecutor_RejectsMalformedBusinessObjectTransitions()
    {
        TestRig rig = CreateRig();
        HashHex dataHash = Hex.Sha256Hex(Hex.Utf8("{\"theme\":\"dark\"}"));
        LedgerSubmitResult genesis = await rig.Executor.SubmitAsync(CreateGenesis(rig.Time.UtcNow, dataHash), CancellationToken.None);
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));

        var businessObjectOutPoint = new OutPoint(genesis.TxId!.Value, 0);
        var plainOutPoint = new OutPoint(genesis.TxId.Value, 1);

        await AssertRejectedAsync(
            rig,
            CreateSignedBusinessObjectTransaction(
                rig,
                plainOutPoint,
                Amount.FromInt64(5),
                new BusinessObjectRef(ObjectId, 2, BusinessObjectOperation.Update, dataHash)),
            "Update must consume the current output");

        await AssertRejectedAsync(
            rig,
            CreateSignedBusinessObjectTransaction(
                rig,
                plainOutPoint,
                Amount.FromInt64(5),
                new BusinessObjectRef(ObjectId, 2, BusinessObjectOperation.Transfer, dataHash),
                Bob),
            "Transfer must consume the current output");

        await AssertRejectedAsync(
            rig,
            CreateSignedBusinessObjectTransaction(
                rig,
                businessObjectOutPoint,
                Amount.FromInt64(10),
                new BusinessObjectRef(ObjectId, 99, BusinessObjectOperation.Update, dataHash)),
            "must advance from version 1 to 2");

        await AssertRejectedAsync(
            rig,
            CreateSignedBusinessObjectTransaction(
                rig,
                businessObjectOutPoint,
                Amount.FromInt64(10),
                new BusinessObjectRef(ObjectId, 1, BusinessObjectOperation.Create, dataHash)),
            "create cannot consume an existing output");

        await AssertRejectedAsync(
            rig,
            CreateSignedBusinessObjectTransaction(
                rig,
                businessObjectOutPoint,
                Amount.FromInt64(10),
                new BusinessObjectRef(ObjectId, 2, BusinessObjectOperation.Close, dataHash)),
            "close must consume the current output without emitting");

        LedgerSubmitResult validClose = await rig.Executor.SubmitAsync(
            CreateSignedBusinessObjectTransaction(rig, businessObjectOutPoint, Amount.FromInt64(10), businessObject: null),
            CancellationToken.None);

        Assert.True(validClose.Accepted, string.Join("; ", validClose.Errors));
    }

    private static async Task AssertRejectedAsync(TestRig rig, LedgerTransaction transaction, string expectedMessage)
    {
        LedgerSubmitResult result = await rig.Executor.SubmitAsync(transaction, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.InvalidBusinessObjectTransition
                && error.Message.Contains(expectedMessage, StringComparison.Ordinal));
    }

    private static LedgerTransaction CreateGenesis(DateTimeOffset timestamp, HashHex dataHash)
        => new(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            timestamp,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(
                    0,
                    Alice,
                    Amount.Zero,
                    new TokenTransfer(CarrierToken, Amount.FromInt64(1)),
                    new BusinessObjectRef(ObjectId, 1, BusinessObjectOperation.Create, dataHash)),
                new TransactionOutput(1, Alice, Amount.FromInt64(5))
            },
            new TransactionData("application/json", dataHash, "{\"theme\":\"dark\"}", null, TransactionDataItemType.BusinessObject));

    private static LedgerTransaction CreateSignedBusinessObjectTransaction(
        TestRig rig,
        OutPoint source,
        Amount amount,
        BusinessObjectRef? businessObject,
        Address? receiver = null)
    {
        rig.Time.Advance(TimeSpan.FromMinutes(1));
        var unsigned = new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            rig.Time.UtcNow,
            new[] { new TransactionInput(0, source, Alice, SignatureBundle.Empty) },
            new[]
            {
                businessObject is null
                    ? new TransactionOutput(0, receiver ?? Alice, Amount.Zero, new TokenTransfer(CarrierToken, Amount.FromInt64(1)))
                    : new TransactionOutput(0, receiver ?? Alice, Amount.Zero, new TokenTransfer(CarrierToken, Amount.FromInt64(1)), businessObject)
            },
            businessObject is null
                ? null
                : new TransactionData("application/json", businessObject.DataHash, "{\"theme\":\"dark\"}", null, TransactionDataItemType.BusinessObject));

        return Sign(unsigned, rig.Hasher);
    }

    private static TestRig CreateRig()
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-10T00:00:00Z"));
        var executor = new LedgerExecutor(
            store,
            hasher,
            new SignaturePayloadBuilder(hasher),
            new TestAdminSigner(),
            timeProvider,
            new SignatureProviderRegistry(new ISignatureProvider[] { new TestSignatureProvider() }));
        return new TestRig(executor, timeProvider, hasher);
    }

    private static LedgerTransaction Sign(LedgerTransaction transaction, ITransactionHasher hasher)
    {
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        LedgerTransaction withTxId = transaction with { TxId = hasher.CalculateTxId(transaction) };
        TransactionInput[] inputs = withTxId.Inputs
            .Select(input =>
            {
                HashHex payloadHash = payloadBuilder.BuildIndependentInputPayloadHash(withTxId, input);
                byte[] publicKey = PublicKeyFor(input.Address);
                return input with
                {
                    SignaturePayloadHash = payloadHash,
                    SignatureBundle = new SignatureBundle(new[]
                    {
                        new SignatureEnvelope(
                            SignatureAlgorithmId.EcdsaP256Sha256,
                            "alice",
                            Address.CalculatePublicKeyHash(publicKey),
                            payloadHash,
                            Hex.Utf8("alice-signature"),
                            publicKey)
                    })
                };
            })
            .ToArray();

        return withTxId with { Inputs = inputs };
    }

    private static byte[] PublicKeyFor(Address address)
        => address == Alice
            ? Hex.Utf8("alice-public")
            : throw new InvalidOperationException($"Unknown test signer address: {address.Value}.");

    private sealed record TestRig(
        LedgerExecutor Executor,
        ManualTimeProvider Time,
        ITransactionHasher Hasher);

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new(new[]
            {
                new SignatureEnvelope(
                    SignatureAlgorithmId.VefSecp256K1Sha256,
                    "test-admin",
                    Hex.Sha256Hex(Hex.Utf8("admin-public")),
                    payloadHash,
                    Hex.Utf8("admin-signature"))
            });
    }

    private sealed class TestSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

        public bool IsSupported => true;

        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
            => Hex.Utf8("alice-signature");

        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
            => payload.Length > 0 && signature.Length > 0 && publicKeyMaterial.Length > 0;
    }
}
