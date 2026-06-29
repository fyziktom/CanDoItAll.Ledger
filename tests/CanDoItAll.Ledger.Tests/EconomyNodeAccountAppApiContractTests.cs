using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomyNodeAccountAppApiContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static readonly LedgerAccountContext Context = new(new LedgerId("account-app-ledger"), new ScenarioId("browser-demo"));
    private static readonly byte[] PublicKey = Encoding.UTF8.GetBytes("account-app-public-key");
    private static readonly string PrivateKeySentinel = Convert.ToBase64String(Encoding.UTF8.GetBytes("private-key-do-not-send"));
    private static readonly Address AccountAddress = Address.FromPublicKey(PublicKey);
    private static readonly TxId TransactionId = new("tx-account-app-detail");

    [Fact]
    public async Task AccountAppNodeClient_UsesOnlyApiReadsAndSignedBroadcastWithoutPrivateKeyMaterial()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://node.example/") };
        using var client = new EconomyNodeLedgerClient(httpClient);
        LedgerAddressQuery query = new(Context, AccountAddress);
        LedgerTransaction signedTransaction = CreateSignedTransaction();

        handler.QueueJson(HttpStatusCode.OK, Array.Empty<LedgerSpendableOutput>());
        await client.GetSpendableOutputsAsync(query, CancellationToken.None);

        handler.QueueJson(HttpStatusCode.OK, new[] { CreateTransactionSummary(includeData: true) });
        await client.GetTransactionsAsync(query, includeData: true, CancellationToken.None);

        handler.QueueJson(HttpStatusCode.OK, CreateTransactionSummary(includeData: true));
        await client.GetTransactionAsync(Context, TransactionId, includeData: true, CancellationToken.None);

        handler.QueueJson(HttpStatusCode.OK, CreateNodeTransactionDetails());
        await client.GetTransactionAsync(TransactionId, includeData: true, CancellationToken.None);

        handler.QueueJson(HttpStatusCode.OK, LedgerSubmitResult.Confirmed(new TxId("tx-submitted"), 42));
        LedgerSubmitResult result = await client.SubmitTransactionAsync(signedTransaction, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal<string>(
            new[]
            {
                "GET /api/v1/ledger/account-app-ledger/accounts/" + AccountAddress.Value + "/spendable-outputs?scenarioId=browser-demo",
                "GET /api/v1/ledger/account-app-ledger/accounts/" + AccountAddress.Value + "/transactions?scenarioId=browser-demo&includeData=true",
                "GET /api/v1/ledger/account-app-ledger/transactions/tx-account-app-detail?scenarioId=browser-demo&includeData=true",
                "GET /api/v1/transactions/tx-account-app-detail?includeData=true",
                "POST /api/v1/ledger/transactions"
            },
            handler.Requests.Select(request => $"{request.Method} {request.PathAndQuery}").ToArray());

        Assert.All(handler.Requests, request =>
        {
            Assert.Null(request.Authorization);
            Assert.False(request.Headers.ContainsKey("X-CDA-Economy-Key"));
        });

        CapturedRequest broadcast = Assert.Single(handler.Requests, request => request.Method == "POST");
        Assert.Contains("\"signatureBundle\"", broadcast.Body, StringComparison.Ordinal);
        Assert.Contains("\"publicKeyMaterial\"", broadcast.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(PrivateKeySentinel, broadcast.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("privateKey", broadcast.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("privateKeyMaterial", broadcast.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BrowserNodeDriverRegistration_ExposesLedgerClientsWithoutStaticSecrets()
    {
        var services = new ServiceCollection();

        services.AddEconomySdkWithBrowserNodeDriver(
            new Uri("https://node.example/"),
            options => options.ApiBasePath = "/api/v1/");

        using ServiceProvider provider = services.BuildServiceProvider();
        var nodeClient = provider.GetRequiredService<EconomyNodeLedgerClient>();

        Assert.Same(nodeClient, provider.GetRequiredService<ILedgerGateway>());
        Assert.Same(nodeClient, provider.GetRequiredService<ILedgerReadClient>());
        Assert.Equal(new Uri("https://node.example/"), nodeClient.NodeBaseUri);
        Assert.Equal("api/v1/health", nodeClient.BuildNodeApiPath("health"));
        Assert.IsType<CryptoDriverAccountInputSigner>(provider.GetRequiredService<IAccountInputSigner>());
        Assert.IsType<AccountTransactionSigner>(provider.GetRequiredService<IAccountTransactionSigner>());
    }

    private static LedgerTransaction CreateSignedTransaction()
    {
        var payloadHash = new HashHex("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var signature = new SignatureEnvelope(
            SignatureAlgorithmId.EcdsaP256Sha256,
            "account-app-key",
            Address.CalculatePublicKeyHash(PublicKey),
            payloadHash,
            Encoding.UTF8.GetBytes("browser-signature"),
            PublicKey);

        return new LedgerTransaction(
            Context.LedgerId,
            Context.ScenarioId,
            Version: 1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            DateTimeOffset.Parse("2026-06-29T12:00:00Z"),
            [
                new TransactionInput(
                    0,
                    new OutPoint(new TxId("tx-source"), 0),
                    AccountAddress,
                    new SignatureBundle([signature]),
                    payloadHash)
            ],
            [
                new TransactionOutput(0, AccountAddress, Amount.FromInt64(1))
            ],
            Data: null,
            TxId: new TxId("tx-account-app-broadcast"));
    }

    private static LedgerTransactionSummary CreateTransactionSummary(bool includeData)
        => new(
            Context,
            TransactionId,
            TransactionKind.Transfer,
            TransactionStatus.Confirmed,
            DateTimeOffset.Parse("2026-06-29T12:00:00Z"),
            [AccountAddress],
            [AccountAddress],
            [
                new LedgerTransactionInputSummary(
                    0,
                    new OutPoint(new TxId("tx-source"), 0),
                    AccountAddress,
                    signatureBundle: new SignatureBundle([
                        new SignatureEnvelope(
                            SignatureAlgorithmId.EcdsaP256Sha256,
                            "account-app-key",
                            Address.CalculatePublicKeyHash(PublicKey),
                            new HashHex("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
                            Encoding.UTF8.GetBytes("browser-signature"),
                            PublicKey)
                    ]),
                    signaturePayloadHash: new HashHex("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"))
            ],
            [
                new LedgerTransactionOutputSummary(0, AccountAddress, Amount.FromInt64(1))
            ],
            includeData
                ? new TransactionData("application/json", null, "{\"source\":\"api-contract\"}", null)
                : null);

    private static EconomyNodeTransactionDetails CreateNodeTransactionDetails()
        => new(
            TransactionId.Value,
            7,
            "Transfer",
            "Confirmed",
            DateTimeOffset.Parse("2026-06-29T12:00:00Z"),
            DateTimeOffset.Parse("2026-06-29T12:00:05Z"),
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            null,
            null,
            null,
            null,
            null,
            [
                new EconomyNodeTransactionInputDetails(
                    0,
                    "tx-source",
                    0,
                    AccountAddress.Value,
                    null,
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    SignatureBundle.Empty)
            ],
            [
                new EconomyNodeTransactionOutputDetails(
                    0,
                    AccountAddress.Value,
                    1m,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)
            ]);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = [];

        public List<CapturedRequest> Requests { get; } = [];

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
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.ToString(),
                request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.Ordinal),
                body));

            if (responses.Count == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{}")
                };
            }

            return responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(
        string Method,
        string PathAndQuery,
        string? Authorization,
        IReadOnlyDictionary<string, string> Headers,
        string Body);
}
