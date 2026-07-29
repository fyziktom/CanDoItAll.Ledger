using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Storage.InMemory;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class BusinessObjectAccountTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly LedgerAccountContext Context = new(LedgerId);
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
    private static readonly TokenId CarrierToken = new("bo.carrier");
    [Fact]
    public async Task Account_CreatesSignsAndTransfersBusinessObject()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-03T00:00:00Z"));
        var gateway = new InProcessLedgerGateway(CreateExecutor(store, hasher, timeProvider), hasher, timeProvider);
        LedgerSubmitResult genesis = await gateway.SubmitTransactionAsync(CreateBootstrapGenesis(timeProvider.UtcNow), CancellationToken.None);
        Assert.True(genesis.Accepted);

        var signer = new AccountInputSigner(new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() }));
        var builder = new BusinessObjectTransactionBuilder(gateway, hasher, new SignaturePayloadBuilder(hasher), signer, timeProvider);
        IAccountKeySource key = new InMemoryAccountKeySource(
            SignatureAlgorithmId.EcdsaP256Sha256,
            "alice-key",
            Hex.Utf8("alice-public"),
            Hex.Utf8("alice-private"));
        var account = new BusinessObjectAccount(Context, key, gateway, gateway, builder);

        LedgerSubmitResult create = await account.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "settings",
                "{\"theme\":\"dense\"}",
                MainAmount: Amount.FromInt64(100),
                ItemType: TransactionDataItemType.AccountSettings),
            CancellationToken.None);

        Assert.True(create.Accepted);
        Assert.True(create.TxId.HasValue);
        BusinessObjectId createdObjectId = BusinessObjectId.FromOutPoint(
            new OutPoint(create.TxId.Value, 0));
        Assert.Equal("100", (await account.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());

        timeProvider.Advance(TimeSpan.FromDays(1));
        LedgerTransaction transfer = await account.BuildTransferBusinessObjectTransactionAsync(
            new TransferBusinessObjectRequest(createdObjectId, Bob, Amount.FromInt64(40)),
            CancellationToken.None);

        Assert.True(BusinessObjectPayloadEnvelopeReader.TryRead(
            transfer.Data?.InlineJson,
            out BusinessObjectPayloadEnvelope transferEnvelope));
        BusinessObjectDataReference transferSource = Assert.Single(transferEnvelope.References);
        Assert.Equal(BusinessObjectReferenceRelationships.PreviousVersion, transferSource.Relationship);
        Assert.Equal(createdObjectId, transferSource.ObjectId);

        Assert.Equal(2, transfer.Inputs.Count);
        TransactionInput input = transfer.Inputs[0];
        SignatureEnvelope signature = Assert.Single(input.SignatureBundle.Signatures);
        Assert.NotNull(input.SignaturePayloadHash);
        Assert.Equal(SignatureAlgorithmId.EcdsaP256Sha256, signature.Algorithm);
        Assert.Equal(input.SignaturePayloadHash, signature.PayloadHash);
        Assert.NotNull(signature.PublicKeyMaterial);
        Assert.Equal(Hex.Utf8("alice-public"), signature.PublicKeyMaterial!);
        Assert.Equal(Alice, Address.FromPublicKey(signature.PublicKeyMaterial!));
        Assert.Equal(Address.CalculatePublicKeyHash(signature.PublicKeyMaterial!), signature.PublicKeyHash);
        Assert.Contains("alice-private", System.Text.Encoding.UTF8.GetString(signature.SignatureBytes), StringComparison.Ordinal);
        Assert.All(transfer.Inputs, signedInput => Assert.NotEmpty(signedInput.SignatureBundle.Signatures));

        LedgerSubmitResult transferResult = await account.SubmitTransactionAsync(transfer, CancellationToken.None);

        Assert.True(transferResult.Accepted);
        Assert.Equal("60", (await account.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());
        IReadOnlyList<LedgerBusinessObjectHolding> bobObjects = await gateway.GetBusinessObjectsAsync(new LedgerAddressQuery(Context, Bob), CancellationToken.None);
        LedgerBusinessObjectHolding bobObject = Assert.Single(bobObjects);
        Assert.True(transferResult.TxId.HasValue);
        Assert.Equal(
            BusinessObjectId.FromOutPoint(new OutPoint(transferResult.TxId.Value, 0)),
            bobObject.ObjectId);
        Assert.NotEqual(createdObjectId, bobObject.ObjectId);
        Assert.Equal(bobObject.ObjectId, BusinessObjectId.FromOutPoint(bobObject.CurrentOutPoint));
        Assert.Equal(2, bobObject.Version);
    }

    [Fact]
    public async Task Clone_ReferencesAnotherOwnersOutPointWithoutConsumingSource()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-04T00:00:00Z"));
        var gateway = new InProcessLedgerGateway(CreateExecutor(store, hasher, timeProvider), hasher, timeProvider);
        Assert.True((await gateway.SubmitTransactionAsync(
            CreateBootstrapGenesis(timeProvider.UtcNow),
            CancellationToken.None)).Accepted);

        var signer = new AccountInputSigner(new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() }));
        var builder = new BusinessObjectTransactionBuilder(gateway, hasher, new SignaturePayloadBuilder(hasher), signer, timeProvider);
        IAccountKeySource aliceKey = new InMemoryAccountKeySource(
            SignatureAlgorithmId.EcdsaP256Sha256,
            "alice-key",
            Hex.Utf8("alice-public"),
            Hex.Utf8("alice-private"));
        var alice = new BusinessObjectAccount(Context, aliceKey, gateway, gateway, builder);

        LedgerSubmitResult sourceCreate = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "post",
                "{\"name\":\"Source post\",\"body\":\"original\"}",
                OwnerAddress: Bob),
            CancellationToken.None);
        Assert.True(sourceCreate.Accepted, string.Join("; ", sourceCreate.Errors));
        Assert.True(sourceCreate.TxId.HasValue);
        BusinessObjectId sourceId = BusinessObjectId.FromOutPoint(sourceCreate.TxId.Value, 0);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerTransaction clone = await alice.BuildCloneBusinessObjectTransactionAsync(
            new CloneBusinessObjectRequest(
                sourceId,
                "post",
                "{\"name\":\"Cloned post\",\"body\":\"copy\"}"),
            CancellationToken.None);

        Assert.DoesNotContain(clone.Inputs, input => input.PreviousOutPoint == sourceId.RequireOutPoint());
        Assert.True(BusinessObjectPayloadEnvelopeReader.TryRead(
            clone.Data?.InlineJson,
            out BusinessObjectPayloadEnvelope cloneEnvelope));
        BusinessObjectDataReference sourceReference = Assert.Single(cloneEnvelope.References);
        Assert.Equal(BusinessObjectReferenceRelationships.CloneSource, sourceReference.Relationship);
        Assert.Equal(sourceId, sourceReference.ObjectId);

        LedgerSubmitResult cloneResult = await alice.SubmitTransactionAsync(clone, CancellationToken.None);
        Assert.True(cloneResult.Accepted, string.Join("; ", cloneResult.Errors));
        Assert.True(cloneResult.TxId.HasValue);

        LedgerBusinessObjectHolding bobSource = Assert.Single(await gateway.GetBusinessObjectsAsync(
            new LedgerAddressQuery(Context, Bob),
            CancellationToken.None));
        Assert.Equal(sourceId, bobSource.ObjectId);
        Assert.Equal(1, bobSource.Version);

        LedgerBusinessObjectHolding aliceClone = Assert.Single(await alice.GetBusinessObjectsAsync(CancellationToken.None));
        Assert.Equal(BusinessObjectId.FromOutPoint(cloneResult.TxId.Value, 0), aliceClone.ObjectId);
        Assert.Equal("post", aliceClone.ObjectType);
        Assert.Equal(sourceId, Assert.Single(aliceClone.References!).ObjectId);
    }

    private static LedgerExecutor CreateExecutor(ILedgerStore store, ITransactionHasher hasher, ITimeProvider timeProvider)
        => new(store, hasher, new SignaturePayloadBuilder(hasher), new TestAdminSigner(), timeProvider, new SignatureProviderRegistry(new ISignatureProvider[] { new EchoSignatureProvider() }));

    private static ITransactionHasher CreateHasher()
        => new TransactionHasher(new DefaultTransactionCanonicalizer());

    private static LedgerTransaction CreateBootstrapGenesis(DateTimeOffset timestamp)
        => new(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            timestamp,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(0, Alice, Amount.FromInt64(100)),
                new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(CarrierToken, Amount.FromInt64(10)))
            },
            null);

    private sealed class EchoSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

        public bool IsSupported => true;

        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
            => System.Text.Encoding.UTF8.GetBytes($"{System.Text.Encoding.UTF8.GetString(payload)}|{System.Text.Encoding.UTF8.GetString(privateKeyMaterial)}");

        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
            => signature.Length > 0 && payload.Length > 0 && publicKeyMaterial.Length > 0;
    }

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
}
