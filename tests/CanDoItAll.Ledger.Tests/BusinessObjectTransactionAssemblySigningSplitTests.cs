using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Sdk;
using CanDoItAll.Economy.Storage.InMemory;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class BusinessObjectTransactionAssemblySigningSplitTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("account-app-ledger"), new ScenarioId("browser-demo"));
    private static readonly Address Receiver = Address.FromPublicKey(Encoding.UTF8.GetBytes("receiver-public"));

    [Fact]
    public async Task UnsignedPaymentDraft_CanBeSignedWithCryptoDriverAndAcceptedByLedger()
    {
        BrowserSigningRig rig = await BrowserSigningRig.CreateAsync();
        LedgerTransaction unsigned = await rig.Builder.BuildUnsignedPaymentAsync(
            Context,
            rig.KeySource,
            new PaymentRequest(Receiver, Amount.FromInt64(25)),
            CancellationToken.None);

        Assert.Null(unsigned.TxId);
        Assert.All(unsigned.Inputs, input =>
        {
            Assert.Null(input.SignaturePayloadHash);
            Assert.Empty(input.SignatureBundle.Signatures);
        });

        LedgerTransaction signed = await rig.TransactionSigner.SignAsync(unsigned, rig.KeySource, CancellationToken.None);

        Assert.NotNull(signed.TxId);
        Assert.All(signed.Inputs, input =>
        {
            SignatureEnvelope signature = Assert.Single(input.SignatureBundle.Signatures);
            Assert.Equal(input.SignaturePayloadHash, signature.PayloadHash);
            Assert.Equal(SignatureAlgorithmId.EcdsaP256Sha256, signature.Algorithm);
            Assert.Equal(rig.KeySource.PublicKeyMaterial.ToArray(), signature.PublicKeyMaterial);
        });

        LedgerSubmitResult accepted = await rig.Gateway.SubmitTransactionAsync(signed, CancellationToken.None);

        Assert.True(accepted.Accepted, string.Join(Environment.NewLine, accepted.Errors.Select(error => $"{error.Code}: {error.Message}")));
        LedgerAccountBalance balance = await rig.Gateway.GetBalanceAsync(new LedgerAddressQuery(Context, rig.KeySource.Address), CancellationToken.None);
        Assert.Equal("75", balance.MainBalance.ToString());
    }

    [Fact]
    public async Task ExistingSignedBuilderBehavior_StillSignsPaymentTransactions()
    {
        BrowserSigningRig rig = await BrowserSigningRig.CreateAsync();

        LedgerTransaction signed = await rig.Builder.BuildPaymentAsync(
            Context,
            rig.KeySource,
            new PaymentRequest(Receiver, Amount.FromInt64(10)),
            CancellationToken.None);

        Assert.NotNull(signed.TxId);
        TransactionInput input = Assert.Single(signed.Inputs);
        Assert.NotNull(input.SignaturePayloadHash);
        Assert.NotEmpty(input.SignatureBundle.Signatures);
    }

    [Fact]
    public async Task UnsignedDraftAssembly_DoesNotRequirePrivateKeyMaterial()
    {
        BrowserSigningRig rig = await BrowserSigningRig.CreateAsync(usePrivateKeyGuard: true);

        LedgerTransaction unsigned = await rig.Builder.BuildUnsignedPaymentAsync(
            Context,
            rig.KeySource,
            new PaymentRequest(Receiver, Amount.FromInt64(5)),
            CancellationToken.None);

        Assert.NotEmpty(unsigned.Inputs);
        Assert.All(unsigned.Inputs, input => Assert.Empty(input.SignatureBundle.Signatures));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => rig.TransactionSigner.SignAsync(unsigned, rig.KeySource, CancellationToken.None));
    }

    [Fact]
    public async Task SignedBrowserTransaction_BroadcastPayloadContainsNoPrivateKeyMaterial()
    {
        BrowserSigningRig rig = await BrowserSigningRig.CreateAsync();
        LedgerTransaction unsigned = await rig.Builder.BuildUnsignedPaymentAsync(
            Context,
            rig.KeySource,
            new PaymentRequest(Receiver, Amount.FromInt64(10)),
            CancellationToken.None);
        LedgerTransaction signed = await rig.TransactionSigner.SignAsync(unsigned, rig.KeySource, CancellationToken.None);
        TxId signedTxId = signed.TxId ?? throw new InvalidOperationException("Signed transaction is missing its transaction id.");
        ReadOnlyMemory<byte> privateKeyMaterial = await rig.KeySource.GetPrivateKeyMaterialAsync(CancellationToken.None);
        string privateKeyBase64 = Convert.ToBase64String(privateKeyMaterial.Span);

        var handler = new CapturingHandler();
        handler.QueueJson(HttpStatusCode.OK, LedgerSubmitResult.Confirmed(signedTxId, 2));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://node.example/") };
        using var client = new EconomyNodeLedgerClient(httpClient);

        LedgerSubmitResult result = await client.SubmitTransactionAsync(signed, CancellationToken.None);

        Assert.True(result.Accepted);
        string body = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"signatureBundle\"", body, StringComparison.Ordinal);
        Assert.Contains("\"publicKeyMaterial\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain(privateKeyBase64, body, StringComparison.Ordinal);
        Assert.DoesNotContain("privateKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("privateKeyMaterial", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class BrowserSigningRig
    {
        private BrowserSigningRig(
            InProcessLedgerGateway gateway,
            BusinessObjectTransactionBuilder builder,
            IAccountTransactionSigner transactionSigner,
            IAccountKeySource keySource)
        {
            Gateway = gateway;
            Builder = builder;
            TransactionSigner = transactionSigner;
            KeySource = keySource;
        }

        public InProcessLedgerGateway Gateway { get; }

        public BusinessObjectTransactionBuilder Builder { get; }

        public IAccountTransactionSigner TransactionSigner { get; }

        public IAccountKeySource KeySource { get; }

        public static async Task<BrowserSigningRig> CreateAsync(bool usePrivateKeyGuard = false)
        {
            ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
            var payloadBuilder = new SignaturePayloadBuilder(hasher);
            var store = new InMemoryLedgerStore(hasher);
            var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-06-29T12:00:00Z"));
            var registry = SignatureProviderRegistry.CreateDefault();
            var gateway = new InProcessLedgerGateway(
                new LedgerExecutor(store, hasher, payloadBuilder, new TestAdminSigner(), timeProvider, registry),
                hasher,
                timeProvider);
            var cryptoDriver = new DotNetEconomyCryptoDriver();
            EcdsaP256KeyPair keyPair = await cryptoDriver.CreateEcdsaP256KeyPairAsync();
            IAccountKeySource keySource = new InMemoryAccountKeySource(
                SignatureAlgorithmId.EcdsaP256Sha256,
                "browser-account-key",
                keyPair.PublicKeyMaterial,
                keyPair.PrivateKeyMaterial);

            LedgerSubmitResult genesis = await gateway.SubmitTransactionAsync(
                CreateGenesis(keySource.Address, timeProvider.UtcNow),
                CancellationToken.None);
            Assert.True(genesis.Accepted, string.Join(Environment.NewLine, genesis.Errors.Select(error => $"{error.Code}: {error.Message}")));

            if (usePrivateKeyGuard)
            {
                keySource = new PrivateKeyGuardAccountKeySource(keySource);
            }

            var inputSigner = new CryptoDriverAccountInputSigner(cryptoDriver);
            var transactionSigner = new AccountTransactionSigner(hasher, payloadBuilder, inputSigner);
            var builder = new BusinessObjectTransactionBuilder(gateway, hasher, payloadBuilder, inputSigner, timeProvider);
            return new BrowserSigningRig(gateway, builder, transactionSigner, keySource);
        }

        private static LedgerTransaction CreateGenesis(Address address, DateTimeOffset timestamp)
            => new(
                Context.LedgerId,
                Context.ScenarioId,
                Version: 1,
                TransactionKind.Genesis,
                SigningMode.Independent,
                timestamp,
                Array.Empty<TransactionInput>(),
                new[] { new TransactionOutput(0, address, Amount.FromInt64(100)) },
                Data: null);
    }

    private sealed class PrivateKeyGuardAccountKeySource(IAccountKeySource inner) : IAccountKeySource
    {
        public Address Address => inner.Address;

        public SignatureAlgorithmId Algorithm => inner.Algorithm;

        public string KeyId => inner.KeyId;

        public HashHex PublicKeyHash => inner.PublicKeyHash;

        public ReadOnlyMemory<byte> PublicKeyMaterial => inner.PublicKeyMaterial;

        public ValueTask<ReadOnlyMemory<byte>> GetPrivateKeyMaterialAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("Private key material is unavailable in the assembly-only path.");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private readonly Queue<HttpResponseMessage> responses = [];

        public List<string> RequestBodies { get; } = [];

        public void QueueJson<T>(HttpStatusCode statusCode, T value)
        {
            string json = JsonSerializer.Serialize(value, JsonOptions);
            responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

            return responses.Count == 0
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : responses.Dequeue();
        }
    }

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new(new[]
            {
                new SignatureEnvelope(
                    SignatureAlgorithmId.VefSecp256K1Sha256,
                    "test-admin",
                    Hex.Sha256Hex(Encoding.UTF8.GetBytes("admin-public")),
                    payloadHash,
                    Encoding.UTF8.GetBytes("admin-signature"))
            });
    }
}
