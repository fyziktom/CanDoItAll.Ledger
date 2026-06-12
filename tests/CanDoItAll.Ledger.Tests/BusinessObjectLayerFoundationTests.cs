using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectLayerFoundationTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly LedgerAccountContext Context = new(LedgerId);
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));

    [Fact]
    public async Task InProcessGateway_SubmitsTransactions_AndProjectsAccountReads()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-01T00:00:00Z"));
        var gateway = new InProcessLedgerGateway(CreateExecutor(store, hasher, timeProvider), hasher, timeProvider);

        LedgerTransaction genesis = CreateGenesis(timeProvider.UtcNow);
        LedgerSubmitResult genesisResult = await gateway.SubmitTransactionAsync(genesis, CancellationToken.None);

        Assert.True(genesisResult.Accepted);
        LedgerAccountBalance aliceAfterGenesis = await gateway.GetBalanceAsync(new LedgerAddressQuery(Context, Alice), CancellationToken.None);
        Assert.Equal("100", aliceAfterGenesis.MainBalance.ToString());
        Assert.Equal(1, aliceAfterGenesis.UtxoCount);
        Assert.Equal(timeProvider.UtcNow, aliceAfterGenesis.AsOfUtc);
        Assert.Single(await gateway.GetBusinessObjectsAsync(new LedgerAddressQuery(Context, Alice), CancellationToken.None));

        timeProvider.Advance(TimeSpan.FromDays(2));
        LedgerTransaction transfer = Sign(CreateTransfer(timeProvider.UtcNow, new OutPoint(genesisResult.TxId!.Value, 0)), hasher);
        LedgerSubmitResult transferResult = await gateway.SubmitTransactionAsync(transfer, CancellationToken.None);

        Assert.True(transferResult.Accepted);
        LedgerAccountBalance aliceAfterTransfer = await gateway.GetBalanceAsync(new LedgerAddressQuery(Context, Alice), CancellationToken.None);
        LedgerAccountBalance bobAfterTransfer = await gateway.GetBalanceAsync(new LedgerAddressQuery(Context, Bob), CancellationToken.None);

        Assert.Equal("60", aliceAfterTransfer.MainBalance.ToString());
        Assert.Equal("40", bobAfterTransfer.MainBalance.ToString());
        Assert.Equal(1, aliceAfterTransfer.UtxoCount);
        Assert.Equal(1, bobAfterTransfer.UtxoCount);
        Assert.Single(await gateway.GetBusinessObjectsAsync(new LedgerAddressQuery(Context, Bob), CancellationToken.None));
        Assert.Equal(2, (await gateway.GetTransactionsAsync(new LedgerAddressQuery(Context, Alice), CancellationToken.None)).Count);
        Assert.Single(await gateway.GetTransactionsAsync(new LedgerAddressQuery(Context, Bob), CancellationToken.None));

        IReadOnlyList<LedgerTransactionSummary> aliceTransactions = await gateway.GetTransactionsAsync(
            new LedgerAddressQuery(Context, Alice),
            includeData: true,
            CancellationToken.None);
        LedgerTransactionSummary transferHistory = Assert.Single(aliceTransactions, x => x.TxId == transferResult.TxId!.Value);

        LedgerTransactionInputSummary input = Assert.Single(transferHistory.Inputs);
        Assert.Equal(0, input.InputIndex);
        Assert.Equal(Alice, input.Address);
        Assert.Equal(genesisResult.TxId.Value, input.PreviousOutPoint.TxId);
        Assert.Equal(0, input.PreviousOutPoint.OutputIndex);
        Assert.NotNull(input.OutPointKey);

        Assert.Equal(2, transferHistory.Outputs.Count);
        Assert.Contains(transferHistory.Outputs, output => output.OutputIndex == 0 && output.Address == Bob && output.MainAmount == Amount.FromInt64(40));
        Assert.Contains(transferHistory.Outputs, output => output.OutputIndex == 1 && output.Address == Alice && output.MainAmount == Amount.FromInt64(60));
        Assert.Equal("{\"name\":\"settings\"}", transferHistory.Data?.InlineJson);

        Assert.All(
            await gateway.GetTransactionsAsync(new LedgerAddressQuery(Context, Alice), CancellationToken.None),
            transaction => Assert.Null(transaction.Data));
    }

    [Fact]
    public void ServiceRegistration_DoesNotReplaceCallerProvidedGateway()
    {
        var callerGateway = new CallerProvidedLedgerGateway();
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<ILedgerGateway>(callerGateway)
            .AddSingleton<ILedgerReadClient>(callerGateway)
            .AddBusinessObjectLedgerLayer()
            .BuildServiceProvider();

        Assert.Same(callerGateway, provider.GetRequiredService<ILedgerGateway>());
        Assert.Same(callerGateway, provider.GetRequiredService<ILedgerReadClient>());
    }

    private static LedgerTransaction CreateGenesis(DateTimeOffset timestamp)
    {
        HashHex dataHash = Hex.Sha256Hex(Hex.Utf8("{\"name\":\"settings\"}"));
        return new LedgerTransaction(
            LedgerId,
            null,
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
                    Amount.FromInt64(100),
                    null,
                    new BusinessObjectRef(new BusinessObjectId("settings"), 1, BusinessObjectOperation.Create, dataHash))
            },
            new TransactionData("application/json", dataHash, "{\"name\":\"settings\"}", null, TransactionDataItemType.AccountSettings));
    }

    private static LedgerTransaction CreateTransfer(DateTimeOffset timestamp, OutPoint previousOutPoint)
    {
        HashHex dataHash = Hex.Sha256Hex(Hex.Utf8("{\"name\":\"settings\"}"));
        return new LedgerTransaction(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            timestamp,
            new[]
            {
                new TransactionInput(0, previousOutPoint, Alice, SignatureBundle.Empty)
            },
            new[]
            {
                new TransactionOutput(
                    0,
                    Bob,
                    Amount.FromInt64(40),
                    null,
                    new BusinessObjectRef(new BusinessObjectId("settings"), 2, BusinessObjectOperation.Transfer, dataHash)),
                new TransactionOutput(1, Alice, Amount.FromInt64(60))
            },
            new TransactionData("application/json", dataHash, "{\"name\":\"settings\"}", null, TransactionDataItemType.BusinessObject));
    }

    private static LedgerExecutor CreateExecutor(ILedgerStore store, ITransactionHasher hasher, ITimeProvider timeProvider)
        => new(store, hasher, new SignaturePayloadBuilder(hasher), new TestAdminSigner(), timeProvider, new SignatureProviderRegistry(new ISignatureProvider[] { new TestSignatureProvider() }));

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

    private sealed class CallerProvidedLedgerGateway : ILedgerGateway, ILedgerReadClient
    {
        public Task<LedgerSubmitResult> SubmitTransactionAsync(LedgerTransaction transaction, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<LedgerAccountBalance> GetBalanceAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
