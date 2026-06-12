using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Storage.InMemory;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectAccountTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly LedgerAccountContext Context = new(LedgerId);
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
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
                new BusinessObjectId("settings"),
                "settings",
                "{\"theme\":\"dense\"}",
                MainAmount: Amount.FromInt64(100),
                ItemType: TransactionDataItemType.AccountSettings),
            CancellationToken.None);

        Assert.True(create.Accepted);
        Assert.Equal("100", (await account.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());

        timeProvider.Advance(TimeSpan.FromDays(1));
        LedgerTransaction transfer = await account.BuildTransferBusinessObjectTransactionAsync(
            new TransferBusinessObjectRequest(new BusinessObjectId("settings"), Bob, Amount.FromInt64(40)),
            CancellationToken.None);

        TransactionInput input = Assert.Single(transfer.Inputs);
        SignatureEnvelope signature = Assert.Single(input.SignatureBundle.Signatures);
        Assert.NotNull(input.SignaturePayloadHash);
        Assert.Equal(SignatureAlgorithmId.EcdsaP256Sha256, signature.Algorithm);
        Assert.Equal(input.SignaturePayloadHash, signature.PayloadHash);
        Assert.NotNull(signature.PublicKeyMaterial);
        Assert.Equal(Hex.Utf8("alice-public"), signature.PublicKeyMaterial!);
        Assert.Equal(Alice, Address.FromPublicKey(signature.PublicKeyMaterial!));
        Assert.Equal(Address.CalculatePublicKeyHash(signature.PublicKeyMaterial!), signature.PublicKeyHash);
        Assert.Contains("alice-private", System.Text.Encoding.UTF8.GetString(signature.SignatureBytes), StringComparison.Ordinal);

        LedgerSubmitResult transferResult = await account.SubmitTransactionAsync(transfer, CancellationToken.None);

        Assert.True(transferResult.Accepted);
        Assert.Equal("60", (await account.GetBalanceAsync(CancellationToken.None)).MainBalance.ToString());
        IReadOnlyList<LedgerBusinessObjectHolding> bobObjects = await gateway.GetBusinessObjectsAsync(new LedgerAddressQuery(Context, Bob), CancellationToken.None);
        LedgerBusinessObjectHolding bobObject = Assert.Single(bobObjects);
        Assert.Equal(new BusinessObjectId("settings"), bobObject.ObjectId);
        Assert.Equal(2, bobObject.Version);
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
            new[] { new TransactionOutput(0, Alice, Amount.FromInt64(100)) },
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
