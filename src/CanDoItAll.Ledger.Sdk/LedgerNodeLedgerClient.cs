using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Sdk;

public sealed class LedgerNodeLedgerClient : ILedgerGateway, ILedgerReadClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly LedgerNodeLedgerClientOptions options;
    private readonly string apiBasePath;

    public LedgerNodeLedgerClient(Uri nodeBaseUri)
        : this(nodeBaseUri, new LedgerNodeLedgerClientOptions())
    {
    }

    public LedgerNodeLedgerClient(Uri nodeBaseUri, LedgerNodeLedgerClientOptions? options)
        : this(new HttpClient { BaseAddress = EnsureTrailingSlash(nodeBaseUri) }, ownsHttpClient: true, options)
    {
    }

    public LedgerNodeLedgerClient(HttpClient httpClient)
        : this(httpClient, new LedgerNodeLedgerClientOptions())
    {
    }

    public LedgerNodeLedgerClient(HttpClient httpClient, LedgerNodeLedgerClientOptions? options)
        : this(httpClient, ownsHttpClient: false, options)
    {
    }

    private LedgerNodeLedgerClient(HttpClient httpClient, bool ownsHttpClient, LedgerNodeLedgerClientOptions? options)
    {
        this.httpClient = httpClient;
        this.ownsHttpClient = ownsHttpClient;
        this.options = (options ?? new LedgerNodeLedgerClientOptions()).Clone();
        apiBasePath = NormalizeApiBasePath(this.options.ApiBasePath);

        if (this.httpClient.BaseAddress is null)
        {
            throw new ArgumentException("HTTP client must have a BaseAddress pointing at an economy node.", nameof(httpClient));
        }
    }

    public Uri NodeBaseUri => httpClient.BaseAddress!;

    public string BuildNodeApiPath(string childResource)
        => BuildApiPath(childResource);

    public Task<NodeHealthResponse> GetHealthAsync(CancellationToken cancellationToken)
        => GetRequiredAsync<NodeHealthResponse>(BuildApiPath("health"), cancellationToken);

    public Task<NodeHealthResponse> GetReadinessAsync(CancellationToken cancellationToken)
        => GetRequiredAsync<NodeHealthResponse>(BuildApiPath("health/readiness"), cancellationToken);

    public Task<NodeBootstrapInfo> GetBootstrapInfoAsync(CancellationToken cancellationToken)
        => GetRequiredAsync<NodeBootstrapInfo>(BuildApiPath("node/bootstrap"), cancellationToken);

    public Task<AdvanceTimeResponse> AdvanceTimeAsync(AdvanceTimeRequest request, CancellationToken cancellationToken)
        => PostRequiredAsync<AdvanceTimeRequest, AdvanceTimeResponse>(BuildApiPath("time/advance"), request, cancellationToken);

    public Task<LedgerResetResponse> ResetLedgerAsync(CancellationToken cancellationToken)
        => PostRequiredAsync<object, LedgerResetResponse>(BuildApiPath("ledger/reset"), new { }, cancellationToken);

    public Task<TransactionReceipt> MintAsync(MintTransactionRequest request, CancellationToken cancellationToken)
        => PostRequiredAsync<MintTransactionRequest, TransactionReceipt>(BuildApiPath("transactions/mint"), request, cancellationToken);

    public Task<TransactionReceipt> TransferAsync(TransferTransactionRequest request, CancellationToken cancellationToken)
        => PostRequiredAsync<TransferTransactionRequest, TransactionReceipt>(BuildApiPath("transactions/transfer"), request, cancellationToken);

    public Task<TokenAuthorityGrantReceipt> GrantTokenCreationAuthorityAsync(
        TokenAuthorityGrantRequest request,
        CancellationToken cancellationToken)
        => PostRequiredAsync<TokenAuthorityGrantRequest, TokenAuthorityGrantReceipt>(
            BuildApiPath("token-authorities/grants"),
            request,
            cancellationToken);

    public Task<PermissionGrantReceipt> GrantPermissionAsync(
        PermissionGrantRequest request,
        CancellationToken cancellationToken)
        => PostRequiredAsync<PermissionGrantRequest, PermissionGrantReceipt>(
            BuildApiPath("permissions/grants"),
            request,
            cancellationToken);

    public async Task<LedgerSubmitResult> SubmitTransactionAsync(
        LedgerTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, BuildApiPath("ledger/transactions"));
        request.Content = JsonContent.Create(transaction, options: JsonOptions);

        using HttpResponseMessage response = await httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return await ReadRequiredAsync<LedgerSubmitResult>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
    {
        List<LedgerNodeSpendableOutput> outputs = await GetRequiredAsync<List<LedgerNodeSpendableOutput>>(
            BuildAccountPath(query, "spendable-outputs"),
            cancellationToken).ConfigureAwait(false);

        return outputs.Select(ToLedgerSpendableOutput).ToArray();
    }

    public async Task<LedgerAccountBalance> GetBalanceAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerSpendableOutput> outputs = await GetSpendableOutputsAsync(query, cancellationToken)
            .ConfigureAwait(false);

        return LedgerBalanceCalculator.Calculate(query, outputs, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
    {
        List<LedgerBusinessObjectHolding> holdings = await GetRequiredAsync<List<LedgerBusinessObjectHolding>>(
            BuildAccountPath(query, "business-objects"),
            cancellationToken).ConfigureAwait(false);

        return holdings;
    }

    public async Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
        => await GetTransactionsAsync(query, includeData: false, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(
        LedgerAddressQuery query,
        bool includeData,
        CancellationToken cancellationToken)
    {
        List<LedgerNodeLedgerTransactionSummary> transactions = await GetRequiredAsync<List<LedgerNodeLedgerTransactionSummary>>(
            BuildAccountPath(query, "transactions", includeData),
            cancellationToken).ConfigureAwait(false);

        return transactions.Select(ToLedgerTransactionSummary).ToArray();
    }

    public async Task<LedgerState?> GetLedgerStateAsync(
        LedgerAccountContext context,
        CancellationToken cancellationToken)
    {
        string path = BuildLedgerPath(context, "state");

        using var request = CreateRequest(HttpMethod.Get, path);
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadRequiredAsync<LedgerState?>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LedgerTransactionSummary?> GetTransactionAsync(
        LedgerAccountContext context,
        TxId txId,
        bool includeData,
        CancellationToken cancellationToken)
    {
        string path = BuildLedgerPath(context, $"transactions/{Uri.EscapeDataString(txId.Value)}");
        if (includeData)
        {
            path += path.Contains('?', StringComparison.Ordinal) ? "&includeData=true" : "?includeData=true";
        }

        using var request = CreateRequest(HttpMethod.Get, path);
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        LedgerNodeLedgerTransactionSummary summary = await ReadRequiredAsync<LedgerNodeLedgerTransactionSummary>(
            response,
            cancellationToken).ConfigureAwait(false);
        return ToLedgerTransactionSummary(summary);
    }

    public async Task<LedgerBusinessObjectHolding?> GetBusinessObjectAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerBusinessObjectHolding> holdings = await QueryBusinessObjectsAsync(
            query with { Take = 1 },
            cancellationToken).ConfigureAwait(false);

        return holdings.FirstOrDefault();
    }

    public async Task<IReadOnlyList<LedgerBusinessObjectHolding>> QueryBusinessObjectsAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
    {
        var request = new LedgerBusinessObjectQueryRequest(
            query.Context.ScenarioId?.Value,
            query.ObjectId?.Value,
            query.ObjectType,
            query.SchemaVersion,
            query.OwnerAddress?.Value,
            query.StateProperties,
            query.Skip,
            query.Take);

        List<LedgerBusinessObjectHolding> holdings = await PostRequiredAsync<LedgerBusinessObjectQueryRequest, List<LedgerBusinessObjectHolding>>(
            BuildLedgerPath(query.Context, "business-objects/query", includeScenario: false),
            request,
            cancellationToken).ConfigureAwait(false);

        return holdings;
    }

    public async Task<IReadOnlyList<LedgerTransactionSummary>> QueryTransactionsAsync(
        LedgerTransactionQuery query,
        CancellationToken cancellationToken)
    {
        var request = new LedgerTransactionQueryRequest(
            query.Context.ScenarioId?.Value,
            query.Address?.Value,
            query.BusinessObjectId?.Value,
            query.Kind?.ToString(),
            query.IncludeData,
            query.Skip,
            query.Take);

        List<LedgerNodeLedgerTransactionSummary> transactions = await PostRequiredAsync<
            LedgerTransactionQueryRequest,
            List<LedgerNodeLedgerTransactionSummary>>(
            BuildLedgerPath(query.Context, "transactions/query", includeScenario: false),
            request,
            cancellationToken).ConfigureAwait(false);

        return transactions.Select(ToLedgerTransactionSummary).ToArray();
    }

    public async Task<LedgerNodeTransactionDetails?> GetTransactionAsync(
        TxId txId,
        CancellationToken cancellationToken)
        => await GetTransactionAsync(txId, includeData: false, cancellationToken).ConfigureAwait(false);

    public async Task<LedgerNodeTransactionDetails?> GetTransactionAsync(
        TxId txId,
        bool includeData,
        CancellationToken cancellationToken)
    {
        string path = BuildApiPath($"transactions/{Uri.EscapeDataString(txId.Value)}");
        if (includeData)
        {
            path += "?includeData=true";
        }

        using var request = CreateRequest(HttpMethod.Get, path);
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadRequiredAsync<LedgerNodeTransactionDetails>(response, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }

    private async Task<T> GetRequiredAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, requestUri);
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return await ReadRequiredAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TResponse> PostRequiredAsync<TRequest, TResponse>(
        string requestUri,
        TRequest requestBody,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, requestUri);
        request.Content = JsonContent.Create(requestBody, options: JsonOptions);

        using HttpResponseMessage response = await httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return await ReadRequiredAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw LedgerNodeRequestException.FromResponse(response.StatusCode, response.ReasonPhrase, content);
        }

        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new InvalidOperationException("Ledger node returned an empty JSON response.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        if (!string.IsNullOrWhiteSpace(options.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(options.ApiKeyHeaderName, options.ApiKey);
        }

        return request;
    }

    private static LedgerSpendableOutput ToLedgerSpendableOutput(LedgerNodeSpendableOutput output)
    {
        BusinessObjectRef? businessObject = null;
        if (output.BusinessObject is { } details)
        {
            BusinessObjectId expectedObjectId = BusinessObjectId.FromOutPoint(output.OutPoint);
            if (details.ObjectId != expectedObjectId)
            {
                throw new InvalidOperationException(
                    $"Ledger node returned BusinessObject id {details.ObjectId.Value} for output {output.OutPoint}; expected {expectedObjectId.Value}.");
            }

            businessObject = new BusinessObjectRef(
                details.LineageKey,
                details.Version,
                details.Operation,
                details.DataHash);
        }

        return new LedgerSpendableOutput(
            output.Context,
            output.OutPointKey,
            output.OutPoint,
            output.Address,
            output.MainAmount,
            output.Token,
            businessObject,
            output.CreatedAt);
    }

    private static LedgerTransactionSummary ToLedgerTransactionSummary(
        LedgerNodeLedgerTransactionSummary summary)
        => new(
            summary.Context,
            summary.TxId,
            summary.Kind,
            summary.Status,
            summary.Timestamp,
            summary.InputAddresses,
            summary.OutputAddresses,
            summary.Inputs,
            summary.Outputs
                .Select(output => new LedgerTransactionOutputSummary(
                    output.OutputIndex,
                    output.Address,
                    output.MainAmount,
                     output.Token,
                     BusinessObject: null,
                     MetadataHash: output.MetadataHash,
                     BusinessObjectDetails: ValidateTransactionBusinessObjectDetails(summary.TxId, output)))
                .ToArray(),
            summary.Data,
            summary.LedgerSequence);

    private static LedgerBusinessObjectOutputDetails? ValidateTransactionBusinessObjectDetails(
        TxId transactionId,
        LedgerNodeLedgerTransactionOutputSummary output)
    {
        if (output.BusinessObject is not { } details)
        {
            return null;
        }

        BusinessObjectId expectedObjectId = BusinessObjectId.FromOutPoint(transactionId, output.OutputIndex);
        if (details.ObjectId != expectedObjectId)
        {
            throw new InvalidOperationException(
                $"Ledger node returned BusinessObject id {details.ObjectId.Value} for transaction output " +
                $"{transactionId.Value}:{output.OutputIndex}; expected {expectedObjectId.Value}.");
        }

        return details;
    }

    private string BuildAccountPath(
        LedgerAddressQuery query,
        string childResource,
        bool includeData = false)
    {
        string path = string.Join(
            '/',
            apiBasePath,
            "ledger",
            Uri.EscapeDataString(query.Context.LedgerId.Value),
            "accounts",
            Uri.EscapeDataString(query.Address.Value),
            childResource);

        var queryParameters = new List<string>();
        if (query.Context.ScenarioId is { } scenarioId)
        {
            queryParameters.Add($"scenarioId={Uri.EscapeDataString(scenarioId.Value)}");
        }

        if (includeData)
        {
            queryParameters.Add("includeData=true");
        }

        return queryParameters.Count == 0
            ? path
            : $"{path}?{string.Join('&', queryParameters)}";
    }

    private string BuildLedgerPath(
        LedgerAccountContext context,
        string childResource,
        bool includeScenario = true)
    {
        string path = string.Join(
            '/',
            apiBasePath,
            "ledger",
            Uri.EscapeDataString(context.LedgerId.Value),
            childResource.Trim('/'));

        return includeScenario && context.ScenarioId is { } scenarioId
            ? $"{path}?scenarioId={Uri.EscapeDataString(scenarioId.Value)}"
            : path;
    }

    private string BuildApiPath(string childResource)
        => string.Join('/', apiBasePath, childResource.Trim('/'));

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        string value = uri.ToString();
        return value.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(value + "/", UriKind.Absolute);
    }

    private static string NormalizeApiBasePath(string? apiBasePath)
    {
        string value = string.IsNullOrWhiteSpace(apiBasePath)
            ? "api/v1"
            : apiBasePath.Trim().Trim('/');

        return string.IsNullOrWhiteSpace(value) ? "api/v1" : value;
    }
}

public sealed class LedgerNodeLedgerClientOptions
{
    public string ApiBasePath { get; set; } = "api/v1";

    public string ApiKeyHeaderName { get; set; } = "X-CDA-Ledger-Key";

    public string? ApiKey { get; set; }

    public string? BearerToken { get; set; }

    internal LedgerNodeLedgerClientOptions Clone()
        => new()
        {
            ApiBasePath = ApiBasePath,
            ApiKeyHeaderName = string.IsNullOrWhiteSpace(ApiKeyHeaderName)
                ? "X-CDA-Ledger-Key"
                : ApiKeyHeaderName,
            ApiKey = ApiKey,
            BearerToken = BearerToken
        };
}

public sealed record LedgerNodeTransactionDetails(
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
    IReadOnlyList<LedgerNodeTransactionInputDetails> Inputs,
    IReadOnlyList<LedgerNodeTransactionOutputDetails> Outputs);

public sealed record LedgerNodeTransactionInputDetails(
    int InputIndex,
    string PreviousTxId,
    int PreviousOutputIndex,
    string Address,
    string? OutPointKey,
    string? SignaturePayloadHash,
    SignatureBundle SignatureBundle);

public sealed record LedgerNodeTransactionOutputDetails(
    int OutputIndex,
    string Address,
    decimal MainAmount,
    string? TokenId,
    decimal? TokenAmount,
    string? BusinessObjectId,
    int? BusinessObjectVersion,
    string? BusinessObjectOperation,
    string? BusinessObjectDataHash);

public sealed class LedgerNodeRequestException : InvalidOperationException
{
    public LedgerNodeRequestException(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string responseBody,
        string? problemTitle,
        string? problemDetail)
        : base(CreateMessage(statusCode, reasonPhrase, responseBody, problemTitle, problemDetail))
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        ResponseBody = responseBody;
        ProblemTitle = problemTitle;
        ProblemDetail = problemDetail;
    }

    public HttpStatusCode StatusCode { get; }

    public string? ReasonPhrase { get; }

    public string ResponseBody { get; }

    public string? ProblemTitle { get; }

    public string? ProblemDetail { get; }

    public static LedgerNodeRequestException FromResponse(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string responseBody)
    {
        (string? title, string? detail) = TryReadProblem(responseBody);
        return new LedgerNodeRequestException(statusCode, reasonPhrase, responseBody, title, detail);
    }

    private static (string? Title, string? Detail) TryReadProblem(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return (null, null);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            JsonElement root = document.RootElement;
            string? title = root.TryGetProperty("title", out JsonElement titleElement) && titleElement.ValueKind == JsonValueKind.String
                ? titleElement.GetString()
                : null;
            string? detail = root.TryGetProperty("detail", out JsonElement detailElement) && detailElement.ValueKind == JsonValueKind.String
                ? detailElement.GetString()
                : null;
            return (title, detail);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string CreateMessage(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string responseBody,
        string? problemTitle,
        string? problemDetail)
    {
        string problemText = string.IsNullOrWhiteSpace(problemDetail)
            ? problemTitle ?? responseBody
            : problemDetail;
        return $"Ledger node request failed with HTTP {(int)statusCode} {reasonPhrase}: {problemText}";
    }
}
