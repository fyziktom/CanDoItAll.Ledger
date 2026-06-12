using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Storage.InMemory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class LedgerCriticalInvariantTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly BusinessObjectId ObjectId = new("settings");

    [Fact]
    public void AssetConservationValidator_RejectsBurnThatCreatesAssets()
    {
        var validator = new AssetConservationValidator();
        var previousOutPoint = new OutPoint(new TxId("previous"), 0);
        var inputKey = new OutPointKey(new HashHex(new string('1', 64)));
        var tokenId = new TokenId("CDA-ACCESS");
        var unknownTokenId = new TokenId("UNAUTHORIZED");
        var inputs = new Dictionary<OutPointKey, UnspentOutput>
        {
            [inputKey] = new(
                inputKey,
                LedgerId,
                null,
                previousOutPoint,
                Alice,
                Amount.FromInt64(10),
                new TokenTransfer(tokenId, Amount.FromInt64(5)),
                null,
                DateTimeOffset.Parse("2026-05-10T00:00:00Z"))
        };
        var tx = new LedgerTransaction(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Burn,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-05-10T00:01:00Z"),
            new[] { new TransactionInput(0, previousOutPoint, Alice, SignatureBundle.Empty) },
            new[]
            {
                new TransactionOutput(0, Alice, Amount.FromInt64(11), new TokenTransfer(tokenId, Amount.FromInt64(6))),
                new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(unknownTokenId, Amount.FromInt64(1)))
            },
            null);

        ValidationResult result = validator.ValidateTransfer(tx, inputs);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.AssetImbalance && x.Message.Contains("main amount", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.AssetImbalance && x.Message.Contains(tokenId.Value, StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.AssetImbalance && x.Message.Contains(unknownTokenId.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void AssetConservationValidator_AllowsBurnThatDestroysAssetsAndReturnsChange()
    {
        var validator = new AssetConservationValidator();
        var previousOutPoint = new OutPoint(new TxId("previous"), 0);
        var inputKey = new OutPointKey(new HashHex(new string('1', 64)));
        var tokenId = new TokenId("CDA-ACCESS");
        var inputs = new Dictionary<OutPointKey, UnspentOutput>
        {
            [inputKey] = new(
                inputKey,
                LedgerId,
                null,
                previousOutPoint,
                Alice,
                Amount.FromInt64(10),
                new TokenTransfer(tokenId, Amount.FromInt64(5)),
                null,
                DateTimeOffset.Parse("2026-05-10T00:00:00Z"))
        };
        var tx = new LedgerTransaction(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Burn,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-05-10T00:01:00Z"),
            new[] { new TransactionInput(0, previousOutPoint, Alice, SignatureBundle.Empty) },
            new[] { new TransactionOutput(0, Alice, Amount.FromInt64(3), new TokenTransfer(tokenId, Amount.FromInt64(1))) },
            null);

        ValidationResult result = validator.ValidateTransfer(tx, inputs);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(x => x.Message)));
    }

    [Fact]
    public void TransactionShapeValidator_RejectsInlineJsonHashMismatch()
    {
        var validator = new TransactionShapeValidator();
        HashHex declaredHash = Hex.Sha256Hex(Hex.Utf8("{\"ok\":true}"));
        var transaction = new LedgerTransaction(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-05-10T00:00:00Z"),
            Array.Empty<TransactionInput>(),
            new[] { new TransactionOutput(0, Alice, Amount.FromInt64(1)) },
            new TransactionData("application/json", declaredHash, "{\"ok\":false}", null, TransactionDataItemType.Generic));

        ValidationResult result = validator.ValidateShape(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidHash && x.Message.Contains("inline JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void TransactionShapeValidator_RejectsInlineJsonWithoutDataHash()
    {
        var validator = new TransactionShapeValidator();
        var transaction = new LedgerTransaction(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-05-10T00:00:00Z"),
            Array.Empty<TransactionInput>(),
            new[] { new TransactionOutput(0, Alice, Amount.FromInt64(1)) },
            new TransactionData("application/json", null, "{\"ok\":true}", null, TransactionDataItemType.Generic));

        ValidationResult result = validator.ValidateShape(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidHash
            && x.Message.Contains("inline JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void TransactionShapeValidator_RejectsUnknownEnumsAndNegativePreviousOutputIndex()
    {
        var validator = new TransactionShapeValidator();
        const string inlineJson = "{\"ok\":true}";
        var transaction = new LedgerTransaction(
            LedgerId,
            null,
            Version: 1,
            (TransactionKind)999,
            (SigningMode)999,
            DateTimeOffset.Parse("2026-05-10T00:00:00Z"),
            new[] { new TransactionInput(0, new OutPoint(new TxId("previous"), -1), Alice, SignatureBundle.Empty) },
            new[] { new TransactionOutput(0, Alice, Amount.FromInt64(1)) },
            new TransactionData(
                "application/json",
                Hex.Sha256Hex(Hex.Utf8(inlineJson)),
                inlineJson,
                null,
                (TransactionDataItemType)999));

        ValidationResult result = validator.ValidateShape(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidTransactionShape
            && x.Message.Contains("Transaction kind", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidTransactionShape
            && x.Message.Contains("Signing mode", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidTransactionShape
            && x.Message.Contains("item type", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidTransactionShape
            && x.Message.Contains("previous output index", StringComparison.Ordinal));
    }

    [Fact]
    public void TransactionShapeValidator_AcceptsMatchingInlineJsonHash()
    {
        var validator = new TransactionShapeValidator();
        const string inlineJson = "{\"ok\":true}";
        HashHex declaredHash = Hex.Sha256Hex(Hex.Utf8(inlineJson));
        var transaction = new LedgerTransaction(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-05-10T00:00:00Z"),
            Array.Empty<TransactionInput>(),
            new[] { new TransactionOutput(0, Alice, Amount.FromInt64(1)) },
            new TransactionData("application/json", declaredHash, inlineJson, null, TransactionDataItemType.Generic));

        ValidationResult result = validator.ValidateShape(transaction);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(x => x.Message)));
    }

    [Fact]
    public async Task LedgerExecutor_RejectsBusinessObjectCreateWhenInlineDataHashDoesNotMatchOutputHash()
    {
        ITransactionHasher hasher = CreateHasher();
        TestRig rig = CreateRig(new InMemoryLedgerStore(hasher), hasher);
        const string inlineJson = "{\"theme\":\"dark\"}";
        HashHex inlineHash = Hex.Sha256Hex(Hex.Utf8(inlineJson));
        HashHex outputHash = Hex.Sha256Hex(Hex.Utf8("{\"theme\":\"light\"}"));

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            rig.Time.UtcNow,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(
                    0,
                    Alice,
                    Amount.FromInt64(1),
                    null,
                    new BusinessObjectRef(ObjectId, 1, BusinessObjectOperation.Create, outputHash))
            },
            new TransactionData("application/json", inlineHash, inlineJson, null, TransactionDataItemType.BusinessObject)), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(
            result.Errors,
            error => error.Code == ValidationErrorCodes.InvalidBusinessObjectTransition
                && error.Message.Contains("data hash must match", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LedgerExecutor_RejectsDuplicateBusinessObjectCreateAgainstCurrentProjection_InMemory()
    {
        ITransactionHasher hasher = CreateHasher();
        TestRig rig = CreateRig(new InMemoryLedgerStore(hasher), hasher);

        await AssertDuplicateBusinessObjectCreateRejectedAsync(rig);
    }

    [Fact]
    public async Task LedgerExecutor_RejectsDuplicateBusinessObjectCreateAgainstCurrentProjection_EfCore()
    {
        ITransactionHasher hasher = CreateHasher();
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var factory = new TestDbContextFactory(options);
        var store = new EfLedgerStore(factory, hasher);
        TestRig rig = CreateRig(store, hasher);

        await AssertDuplicateBusinessObjectCreateRejectedAsync(rig);
    }

    private static async Task AssertDuplicateBusinessObjectCreateRejectedAsync(TestRig rig)
    {
        HashHex dataHash = Hex.Sha256Hex(Hex.Utf8("{\"theme\":\"dark\"}"));
        LedgerSubmitResult genesis = await rig.Executor.SubmitAsync(CreateGenesis(rig.Time.UtcNow, dataHash), CancellationToken.None);
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));

        var plainOutPoint = new OutPoint(genesis.TxId!.Value, 1);
        LedgerSubmitResult duplicateCreate = await rig.Executor.SubmitAsync(
            CreateSignedBusinessObjectCreate(rig, plainOutPoint, dataHash),
            CancellationToken.None);

        Assert.False(duplicateCreate.Accepted);
        Assert.Contains(
            duplicateCreate.Errors,
            error => error.Code == ValidationErrorCodes.InvalidBusinessObjectTransition
                && error.Message.Contains("already exists", StringComparison.Ordinal));
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
                    Amount.FromInt64(10),
                    null,
                    new BusinessObjectRef(ObjectId, 1, BusinessObjectOperation.Create, dataHash)),
                new TransactionOutput(1, Alice, Amount.FromInt64(5))
            },
            new TransactionData("application/json", dataHash, "{\"theme\":\"dark\"}", null, TransactionDataItemType.BusinessObject));

    private static LedgerTransaction CreateSignedBusinessObjectCreate(TestRig rig, OutPoint source, HashHex dataHash)
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
                new TransactionOutput(
                    0,
                    Alice,
                    Amount.FromInt64(5),
                    null,
                    new BusinessObjectRef(ObjectId, 1, BusinessObjectOperation.Create, dataHash))
            },
            new TransactionData("application/json", dataHash, "{\"theme\":\"dark\"}", null, TransactionDataItemType.BusinessObject));

        return Sign(unsigned, rig.Hasher);
    }

    private static TestRig CreateRig(ILedgerStore store, ITransactionHasher hasher)
    {
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

    private static ITransactionHasher CreateHasher()
        => new TransactionHasher(new DefaultTransactionCanonicalizer());

    private static LedgerTransaction Sign(LedgerTransaction transaction, ITransactionHasher hasher)
    {
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        LedgerTransaction withTxId = transaction with { TxId = hasher.CalculateTxId(transaction) };
        TransactionInput[] inputs = withTxId.Inputs
            .Select(input =>
            {
                HashHex payloadHash = payloadBuilder.BuildIndependentInputPayloadHash(withTxId, input);
                byte[] publicKey = Hex.Utf8("alice-public");
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

    private sealed class TestDbContextFactory(DbContextOptions<EconomyDbContext> options) : IDbContextFactory<EconomyDbContext>
    {
        public EconomyDbContext CreateDbContext()
            => new(options);

        public ValueTask<EconomyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}
