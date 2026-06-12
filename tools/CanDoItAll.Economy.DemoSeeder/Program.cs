using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    Converters = { new JsonStringEnumConverter() }
};

DemoSeedOptions options = DemoSeedOptions.Parse(args);
using HttpClient http = CreateHttpClient(options);

await WaitForNodeAsync(http, options, jsonOptions);
NodeBootstrapInfo bootstrap = await GetRequiredAsync<NodeBootstrapInfo>(http, "api/v1/node/bootstrap", options, jsonOptions);

DemoAccountKey alice = await LoadOrCreateKeyAsync(options, "alice", jsonOptions);
DemoAccountKey bob = await LoadOrCreateKeyAsync(options, "bob", jsonOptions);

IReadOnlyList<AccountSummary> before = await GetAccountsAsync(http, options, jsonOptions);
decimal aliceBefore = GetTokenBalance(before, alice.Address, options.TokenId);
decimal bobBefore = GetTokenBalance(before, bob.Address, options.TokenId);
decimal adminBefore = GetTokenBalance(before, bootstrap.AdminAddress, options.TokenId);

if (aliceBefore > options.TransferAmount)
{
    throw new InvalidOperationException($"Alice already has {aliceBefore.ToString(CultureInfo.InvariantCulture)} {options.TokenId}; expected at most {options.TransferAmount.ToString(CultureInfo.InvariantCulture)}.");
}

if (bobBefore > options.TransferAmount)
{
    throw new InvalidOperationException($"Bob already has {bobBefore.ToString(CultureInfo.InvariantCulture)} {options.TokenId}; expected at most {options.TransferAmount.ToString(CultureInfo.InvariantCulture)}.");
}

decimal aliceDeficit = options.TransferAmount - aliceBefore;
decimal bobDeficit = options.TransferAmount - bobBefore;
decimal requiredFromAdmin = aliceDeficit + bobDeficit;
TransactionReceipt? mint = null;
TransactionReceipt? aliceTransfer = null;
TransactionReceipt? bobTransfer = null;

if (requiredFromAdmin > 0m && adminBefore < requiredFromAdmin)
{
    mint = await PostRequiredAsync<TransactionReceipt>(
        http,
        "api/v1/transactions/mint",
        new MintTransactionRequest(
            bootstrap.AdminAddress,
            0m,
            options.TokenId,
            options.SourceAmount,
            "TokenMetadata",
            JsonSerializer.Serialize(new
            {
                token = options.TokenId,
                purpose = "docker-compose-customer-demo",
                source = "admin",
                sourceAmount = options.SourceAmount.ToString(CultureInfo.InvariantCulture)
            }),
            $"demo://{options.TokenId}/admin-source",
            null,
            null),
        options,
        jsonOptions);
}

if (aliceDeficit > 0m)
{
    aliceTransfer = await TransferAsync(http, options, bootstrap.AdminAddress, alice.Address, "alice", aliceDeficit, jsonOptions);
}

if (bobDeficit > 0m)
{
    bobTransfer = await TransferAsync(http, options, bootstrap.AdminAddress, bob.Address, "bob", bobDeficit, jsonOptions);
}

IReadOnlyList<AccountSummary> after = await GetAccountsAsync(http, options, jsonOptions);
decimal aliceAfter = GetTokenBalance(after, alice.Address, options.TokenId);
decimal bobAfter = GetTokenBalance(after, bob.Address, options.TokenId);

if (aliceAfter != options.TransferAmount)
{
    throw new InvalidOperationException($"Alice {options.TokenId} balance expected {options.TransferAmount.ToString(CultureInfo.InvariantCulture)}, got {aliceAfter.ToString(CultureInfo.InvariantCulture)}.");
}

if (bobAfter != options.TransferAmount)
{
    throw new InvalidOperationException($"Bob {options.TokenId} balance expected {options.TransferAmount.ToString(CultureInfo.InvariantCulture)}, got {bobAfter.ToString(CultureInfo.InvariantCulture)}.");
}

await AssertTransferOutputAsync(http, options, aliceTransfer?.TxId, alice.Address, options.TransferAmount, jsonOptions);
await AssertTransferOutputAsync(http, options, bobTransfer?.TxId, bob.Address, options.TransferAmount, jsonOptions);

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = "seeded",
    node = options.NodeBaseUri.ToString(),
    token = options.TokenId,
    sourceAmount = options.SourceAmount.ToString(CultureInfo.InvariantCulture),
    admin = new
    {
        address = bootstrap.AdminAddress,
        before = adminBefore.ToString(CultureInfo.InvariantCulture),
        mintTransaction = mint?.TxId
    },
    alice = new
    {
        address = alice.Address,
        before = aliceBefore.ToString(CultureInfo.InvariantCulture),
        after = aliceAfter.ToString(CultureInfo.InvariantCulture),
        keyPath = alice.Path,
        transaction = aliceTransfer?.TxId
    },
    bob = new
    {
        address = bob.Address,
        before = bobBefore.ToString(CultureInfo.InvariantCulture),
        after = bobAfter.ToString(CultureInfo.InvariantCulture),
        keyPath = bob.Path,
        transaction = bobTransfer?.TxId
    }
}, jsonOptions));

static HttpClient CreateHttpClient(DemoSeedOptions options)
{
    if (!options.AllowInvalidHttpsCertificate)
    {
        return new HttpClient { BaseAddress = options.NodeBaseUri, Timeout = TimeSpan.FromSeconds(30) };
    }

    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    return new HttpClient(handler) { BaseAddress = options.NodeBaseUri, Timeout = TimeSpan.FromSeconds(30) };
}

static async Task WaitForNodeAsync(
    HttpClient http,
    DemoSeedOptions options,
    JsonSerializerOptions jsonOptions)
{
    DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(options.WaitTimeout);
    Exception? lastException = null;
    while (DateTimeOffset.UtcNow < deadline)
    {
        try
        {
            NodeHealthResponse health = await GetRequiredAsync<NodeHealthResponse>(http, "api/v1/health", options, jsonOptions);
            if (string.Equals(health.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            lastException = exception;
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    throw new TimeoutException($"Economy node was not ready within {options.WaitTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds.", lastException);
}

static async Task<TransactionReceipt> TransferAsync(
    HttpClient http,
    DemoSeedOptions options,
    string fromAddress,
    string toAddress,
    string receiverName,
    decimal amount,
    JsonSerializerOptions jsonOptions)
    => await PostRequiredAsync<TransactionReceipt>(
        http,
        "api/v1/transactions/transfer",
        new TransferTransactionRequest(
            fromAddress,
            toAddress,
            0m,
            options.TokenId,
            amount,
            "TokenMetadata",
            JsonSerializer.Serialize(new
            {
                token = options.TokenId,
                purpose = "docker-compose-customer-demo",
                receiver = receiverName,
                adminIssue = amount.ToString(CultureInfo.InvariantCulture)
            }),
            $"demo://{options.TokenId}/admin-to-{receiverName}",
            null,
            null),
        options,
        jsonOptions);

static async Task AssertTransferOutputAsync(
    HttpClient http,
    DemoSeedOptions options,
    string? txId,
    string receiverAddress,
    decimal amount,
    JsonSerializerOptions jsonOptions)
{
    if (string.IsNullOrWhiteSpace(txId))
    {
        return;
    }

    TransactionDetails details = await GetRequiredAsync<TransactionDetails>(
        http,
        $"api/v1/transactions/{Uri.EscapeDataString(txId)}?includeData=true",
        options,
        jsonOptions);
    bool hasOutput = details.Outputs.Any(output =>
        string.Equals(output.Address, receiverAddress, StringComparison.Ordinal)
        && string.Equals(output.TokenId, options.TokenId, StringComparison.Ordinal)
        && output.TokenAmount == amount);

    if (!hasOutput)
    {
        throw new InvalidOperationException($"Transaction {txId} does not contain the expected {amount.ToString(CultureInfo.InvariantCulture)} {options.TokenId} output for {receiverAddress}.");
    }
}

static async Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(
    HttpClient http,
    DemoSeedOptions options,
    JsonSerializerOptions jsonOptions)
    => await GetRequiredAsync<List<AccountSummary>>(http, "api/v1/accounts", options, jsonOptions);

static decimal GetTokenBalance(
    IReadOnlyList<AccountSummary> accounts,
    string address,
    string tokenId)
{
    AccountSummary? account = accounts.FirstOrDefault(x => string.Equals(x.Address, address, StringComparison.Ordinal));
    if (account is null)
    {
        return 0m;
    }

    return account.TokenBalances.TryGetValue(tokenId, out decimal balance) ? balance : 0m;
}

static async Task<T> GetRequiredAsync<T>(
    HttpClient http,
    string path,
    DemoSeedOptions options,
    JsonSerializerOptions jsonOptions)
{
    using HttpRequestMessage request = CreateRequest(HttpMethod.Get, path, options);
    using HttpResponseMessage response = await http.SendAsync(request);
    return await ReadRequiredAsync<T>(response, jsonOptions);
}

static async Task<TResponse> PostRequiredAsync<TResponse>(
    HttpClient http,
    string path,
    object body,
    DemoSeedOptions options,
    JsonSerializerOptions jsonOptions)
{
    using HttpRequestMessage request = CreateRequest(HttpMethod.Post, path, options);
    request.Content = JsonContent.Create(body, options: jsonOptions);
    using HttpResponseMessage response = await http.SendAsync(request);
    return await ReadRequiredAsync<TResponse>(response, jsonOptions);
}

static HttpRequestMessage CreateRequest(HttpMethod method, string path, DemoSeedOptions options)
{
    var request = new HttpRequestMessage(method, path);
    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        request.Headers.TryAddWithoutValidation(options.ApiKeyHeaderName, options.ApiKey);
    }

    return request;
}

static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, JsonSerializerOptions jsonOptions)
{
    string content = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {content}");
    }

    return JsonSerializer.Deserialize<T>(content, jsonOptions)
        ?? throw new InvalidOperationException("Economy node returned an empty JSON response.");
}

static async Task<DemoAccountKey> LoadOrCreateKeyAsync(
    DemoSeedOptions options,
    string accountName,
    JsonSerializerOptions jsonOptions)
{
    string directory = Path.Combine(options.KeyRoot, options.KeySetName, "accounts", accountName);
    string path = Path.Combine(directory, "account-key.json");
    if (File.Exists(path))
    {
        AccountKeyFile existing = JsonSerializer.Deserialize<AccountKeyFile>(
            await File.ReadAllTextAsync(path),
            jsonOptions)
            ?? throw new InvalidOperationException($"Could not read key file {path}.");
        return new DemoAccountKey(existing.Address, path);
    }

    Directory.CreateDirectory(directory);
    (byte[] publicKeyMaterial, byte[] privateKeyMaterial) = EcdsaP256KeyMaterial.CreateKeyPair();
    string address = Address.FromPublicKey(publicKeyMaterial).Value;
    var created = new AccountKeyFile(
        address,
        SignatureAlgorithmId.EcdsaP256Sha256,
        $"compose-demo-{accountName}-{Guid.NewGuid():N}",
        Convert.ToBase64String(publicKeyMaterial),
        Convert.ToBase64String(privateKeyMaterial),
        Address.CalculatePublicKeyHash(publicKeyMaterial).Value);
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(created, jsonOptions));
    return new DemoAccountKey(address, path);
}

internal sealed record DemoSeedOptions(
    Uri NodeBaseUri,
    string ApiKeyHeaderName,
    string? ApiKey,
    string KeyRoot,
    string KeySetName,
    string TokenId,
    decimal SourceAmount,
    decimal TransferAmount,
    TimeSpan WaitTimeout,
    bool AllowInvalidHttpsCertificate)
{
    public static DemoSeedOptions Parse(string[] args)
    {
        string node = Read(args, "--node")
            ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_NODE_URL")
            ?? "http://localhost:8080/";
        string keyRoot = Read(args, "--keys")
            ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_TEST_KEY_ROOT")
            ?? Path.Combine(Environment.CurrentDirectory, "docker", "demo-keys");
        string tokenId = Read(args, "--token")
            ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_DEMO_TOKEN_ID")
            ?? "HreVaDa";
        decimal sourceAmount = ReadDecimal(
            Read(args, "--source-amount") ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_DEMO_TOKEN_SOURCE_AMOUNT"),
            decimal.MaxValue);
        decimal transferAmount = ReadDecimal(
            Read(args, "--transfer-amount") ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_DEMO_TRANSFER_AMOUNT"),
            100m);
        int waitSeconds = ReadInt(
            Read(args, "--wait-seconds") ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_DEMO_WAIT_SECONDS"),
            180);

        return new DemoSeedOptions(
            EnsureTrailingSlash(new Uri(node, UriKind.Absolute)),
            Read(args, "--api-key-header") ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_API_KEY_HEADER") ?? "X-CDA-Economy-Key",
            Read(args, "--api-key") ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_API_KEY"),
            keyRoot,
            Read(args, "--key-set") ?? Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_DEMO_KEY_SET") ?? "economy-console-demo",
            tokenId,
            sourceAmount,
            transferAmount,
            TimeSpan.FromSeconds(waitSeconds),
            HasSwitch(args, "--allow-invalid-https-certificate")
                || ReadBool(Environment.GetEnvironmentVariable("CANDOITALL_ECONOMY_ALLOW_INVALID_HTTPS_CERTIFICATE")));
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        string value = uri.ToString();
        return value.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(value + "/", UriKind.Absolute);
    }

    private static string? Read(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {name}.");
            }

            return args[i + 1];
        }

        return null;
    }

    private static bool HasSwitch(string[] args, string name)
        => args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    private static decimal ReadDecimal(string? value, decimal fallback)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : fallback;

    private static int ReadInt(string? value, int fallback)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;

    private static bool ReadBool(string? value)
        => bool.TryParse(value, out bool parsed) && parsed;
}

internal sealed record DemoAccountKey(string Address, string Path);

internal sealed record AccountKeyFile(
    string Address,
    SignatureAlgorithmId Algorithm,
    string KeyId,
    string PublicKeyMaterialBase64,
    string PrivateKeyMaterialBase64,
    string PublicKeyHash);

internal sealed record NodeHealthResponse(string Status, DateTimeOffset UtcNow);

internal sealed record NodeBootstrapInfo(
    string LedgerId,
    bool BootstrapEnabled,
    bool GenesisCreated,
    string AdminAddress,
    string KeyId,
    string Algorithm,
    string ApiVersion,
    string ApiBasePath,
    string? LegacyApiBasePath);

internal sealed record MintTransactionRequest(
    string Address,
    decimal MainAmount,
    string? TokenId,
    decimal? TokenAmount,
    string? DataItemType,
    string? DataJson,
    string? ExternalRef,
    string? BusinessObjectId,
    string? ScenarioId);

internal sealed record TransferTransactionRequest(
    string FromAddress,
    string ToAddress,
    decimal MainAmount,
    string? TokenId,
    decimal? TokenAmount,
    string? DataItemType,
    string? DataJson,
    string? ExternalRef,
    string? BusinessObjectId,
    string? ScenarioId);

internal sealed record TransactionReceipt(string TxId, long LedgerSequence);

internal sealed record AccountSummary(
    string Address,
    decimal MainBalance,
    string MainBalanceText,
    IReadOnlyDictionary<string, decimal> TokenBalances,
    IReadOnlyDictionary<string, string> TokenBalanceTexts,
    int UtxoCount);

internal sealed record TransactionDetails(
    string TxId,
    long LedgerSequence,
    string Kind,
    string Status,
    DateTimeOffset Timestamp,
    DateTimeOffset? ConfirmedAt,
    string? TxHash,
    string? LedgerEntryHash,
    string? DataItemType,
    string? DataContentType,
    string? DataHash,
    string? InlineJson,
    string? ExternalRef,
    IReadOnlyList<TransactionInputDetails> Inputs,
    IReadOnlyList<TransactionOutputDetails> Outputs);

internal sealed record TransactionInputDetails(
    int InputIndex,
    string PreviousTxId,
    int PreviousOutputIndex,
    string Address,
    string? OutPointKey,
    string? SignaturePayloadHash);

internal sealed record TransactionOutputDetails(
    int OutputIndex,
    string Address,
    decimal MainAmount,
    string? TokenId,
    decimal? TokenAmount,
    string? BusinessObjectId,
    int? BusinessObjectVersion,
    string? BusinessObjectOperation,
    string? BusinessObjectDataHash);
