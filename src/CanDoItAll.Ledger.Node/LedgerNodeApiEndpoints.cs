using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Components.Mermaid.Infrastructure;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Rendering;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Node.Components;
using CanDoItAll.Ledger.Persistence.EFCore;
using CanDoItAll.Ledger.Persistence.PostgreSql;
using CanDoItAll.Ledger.Sdk;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;

namespace CanDoItAll.Ledger.Node;
public static partial class LedgerNodeHost
{
    private static void MapLedgerApi(RouteGroupBuilder api)
    {
        api.MapGet("/health", (LedgerNodeHealthService health) =>
            Results.Ok(health.GetLiveness()))
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapGet("/health/readiness", async (LedgerNodeHealthService health, CancellationToken cancellationToken) =>
            Results.Ok(await health.GetReadinessAsync(cancellationToken).ConfigureAwait(false)))
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(DeepHealthRateLimitPolicy);

        api.MapGet("/node/bootstrap", async (LedgerNodeBootstrapService bootstrap, CancellationToken cancellationToken) =>
            Results.Ok(await bootstrap.GetBootstrapInfoAsync(cancellationToken).ConfigureAwait(false)))
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapGet("/time", (ManualTimeProvider timeProvider) => Results.Ok(new { utcNow = timeProvider.UtcNow }))
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapPost("/time/advance", (AdvanceTimeRequest request, ManualTimeProvider timeProvider, LedgerNodeHostOptions options) =>
        {
            TimeSpan duration = TimeSpan.FromDays(request.Days) + TimeSpan.FromHours(request.Hours);
            if (Math.Abs(duration.TotalDays) > options.MaxTimeAdvanceDays)
            {
                return Results.Problem(
                    title: "Ledger node request failed",
                    detail: $"Time advance cannot exceed {options.MaxTimeAdvanceDays.ToString(CultureInfo.InvariantCulture)} days.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Ok(new { utcNow = timeProvider.Advance(duration) });
        })
        .AddEndpointFilter(ValidateRequestBodySizeAsync)
        .AddEndpointFilter(ValidateLedgerAdminAuthorizationAsync)
        .RequireRateLimiting(WriteRateLimitPolicy);

        api.MapPost("/ledger/reset", async (LedgerCommandService commands, LedgerNodeHostOptions options, CancellationToken cancellationToken) =>
        {
            if (!options.AllowLedgerReset)
            {
                return Results.Problem(
                    title: "Ledger node request failed",
                    detail: "Ledger reset is disabled by node configuration.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            await commands.ResetAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { status = "reset" });
        })
        .AddEndpointFilter(ValidateRequestBodySizeAsync)
        .AddEndpointFilter(ValidateLedgerAdminAuthorizationAsync)
        .RequireRateLimiting(WriteRateLimitPolicy);

        api.MapPost("/transactions/mint", async (MintTransactionRequest request, LedgerTreasuryCommandService treasury, CancellationToken cancellationToken) =>
        {
            return await LedgerNodeApiResults.OkOrProblemAsync(
                token => treasury.MintAsync(request, token),
                cancellationToken).ConfigureAwait(false);
        })
        .AddEndpointFilter(ValidateRequestBodySizeAsync)
        .AddEndpointFilter(ValidateLedgerAdminAuthorizationAsync)
        .RequireRateLimiting(WriteRateLimitPolicy);

        api.MapPost("/transactions/transfer", async (TransferTransactionRequest request, LedgerTreasuryCommandService treasury, CancellationToken cancellationToken) =>
        {
            return await LedgerNodeApiResults.OkOrProblemAsync(
                token => treasury.TransferAsync(request, token),
                cancellationToken).ConfigureAwait(false);
        })
        .AddEndpointFilter(ValidateRequestBodySizeAsync)
        .AddEndpointFilter(ValidateLedgerAdminAuthorizationAsync)
        .RequireRateLimiting(WriteRateLimitPolicy);

        api.MapPost("/token-authorities/grants", async (TokenAuthorityGrantRequest request, LedgerTreasuryCommandService treasury, CancellationToken cancellationToken) =>
        {
            return await LedgerNodeApiResults.OkOrProblemAsync(
                token => treasury.GrantTokenCreationAuthorityAsync(request, token),
                cancellationToken).ConfigureAwait(false);
        })
        .AddEndpointFilter(ValidateRequestBodySizeAsync)
        .AddEndpointFilter(ValidateLedgerAdminAuthorizationAsync)
        .RequireRateLimiting(WriteRateLimitPolicy);

        api.MapPost("/permissions/grants", async (PermissionGrantRequest request, LedgerTreasuryCommandService treasury, CancellationToken cancellationToken) =>
        {
            return await LedgerNodeApiResults.OkOrProblemAsync(
                token => treasury.GrantPermissionAsync(request, token),
                cancellationToken).ConfigureAwait(false);
        })
        .AddEndpointFilter(ValidateRequestBodySizeAsync)
        .AddEndpointFilter(ValidateLedgerAdminAuthorizationAsync)
        .RequireRateLimiting(WriteRateLimitPolicy);

        api.MapPost("/ledger/transactions", async (LedgerTransaction transaction, LedgerCommandService commands, CancellationToken cancellationToken) =>
            Results.Ok(await commands.SubmitLedgerTransactionAsync(transaction, cancellationToken).ConfigureAwait(false)))
        .AddEndpointFilter(ValidateRequestBodySizeAsync)
        .AddEndpointFilter(ValidateLedgerWriteAuthorizationAsync)
        .RequireRateLimiting(WriteRateLimitPolicy);

        api.MapGet("/ledger/{ledgerId}/accounts/{address}/spendable-outputs", async (
            string ledgerId,
            string address,
            string? scenarioId,
            LedgerQueryService queries,
            CancellationToken cancellationToken) =>
            await LedgerNodeApiResults.OkOrProblemAsync(
                token => GetPublicSpendableOutputsAsync(
                    queries,
                    CreateLedgerAddressQuery(ledgerId, address, scenarioId),
                    token),
                cancellationToken).ConfigureAwait(false))
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapGet("/ledger/{ledgerId}/accounts/{address}/business-objects", async (
            string ledgerId,
            string address,
            string? scenarioId,
            LedgerQueryService queries,
            CancellationToken cancellationToken) =>
            await LedgerNodeApiResults.OkOrProblemAsync(
                token => queries.GetBusinessObjectHoldingsAsync(CreateLedgerAddressQuery(ledgerId, address, scenarioId), token),
                cancellationToken).ConfigureAwait(false))
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapGet("/ledger/{ledgerId}/accounts/{address}/transactions", async (
            string ledgerId,
            string address,
            string? scenarioId,
            bool? includeData,
            LedgerQueryService queries,
            CancellationToken cancellationToken) =>
            await LedgerNodeApiResults.OkOrProblemAsync(
                token => ToPublicLedgerTransactionsAsync(
                    queries.GetLedgerTransactionsAsync(
                        CreateLedgerAddressQuery(ledgerId, address, scenarioId),
                        includeData ?? false,
                        token)),
                cancellationToken).ConfigureAwait(false))
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapGet("/ledger/{ledgerId}/state", async (
            string ledgerId,
            string? scenarioId,
            LedgerQueryService queries,
            CancellationToken cancellationToken) =>
            await LedgerNodeApiResults.OkOrProblemAsync(
                token => queries.GetLedgerStateAsync(CreateLedgerAccountContext(ledgerId, scenarioId), token),
                cancellationToken).ConfigureAwait(false))
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapGet("/ledger/{ledgerId}/transactions/{txId}", async (
            string ledgerId,
            string txId,
            string? scenarioId,
            bool? includeData,
            LedgerQueryService queries,
            CancellationToken cancellationToken) =>
        {
            try
            {
                ValidateIdentifier(txId, LedgerValidationLimits.MaxTxIdLength, "transaction id");
                LedgerTransactionSummary? summary = await queries
                    .GetLedgerTransactionAsync(CreateLedgerAccountContext(ledgerId, scenarioId), new TxId(txId), includeData ?? false, cancellationToken)
                    .ConfigureAwait(false);
                return summary is null
                    ? Results.NotFound()
                    : Results.Ok(ToPublicLedgerTransaction(summary));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    title: "Ledger node request failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapPost("/ledger/{ledgerId}/business-objects/query", async (
            string ledgerId,
            LedgerBusinessObjectQueryRequest request,
            LedgerQueryService queries,
            CancellationToken cancellationToken) =>
            await LedgerNodeApiResults.OkOrProblemAsync(
                token => queries.QueryBusinessObjectHoldingsAsync(CreateBusinessObjectQuery(ledgerId, request), token),
                cancellationToken).ConfigureAwait(false))
            .AddEndpointFilter(ValidateRequestBodySizeAsync)
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapPost("/ledger/{ledgerId}/transactions/query", async (
            string ledgerId,
            LedgerTransactionQueryRequest request,
            LedgerQueryService queries,
            CancellationToken cancellationToken) =>
            await LedgerNodeApiResults.OkOrProblemAsync(
                token => ToPublicLedgerTransactionsAsync(
                    queries.QueryLedgerTransactionsAsync(
                        CreateLedgerTransactionQuery(ledgerId, request),
                        token)),
                cancellationToken).ConfigureAwait(false))
            .AddEndpointFilter(ValidateRequestBodySizeAsync)
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapGet("/accounts", async (LedgerQueryService queries, CancellationToken cancellationToken) =>
            await LedgerNodeApiResults.OkOrProblemAsync(
                token => queries.GetAccountsAsync(token),
                cancellationToken).ConfigureAwait(false))
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapGet("/accounts/{address}/transactions", async (string address, LedgerQueryService queries, CancellationToken cancellationToken) =>
            await LedgerNodeApiResults.OkOrProblemAsync(
                token =>
                {
                    ValidateAddress(address, "address");
                    return queries.GetAddressTransactionsAsync(address, token);
                },
                cancellationToken).ConfigureAwait(false))
            .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
            .RequireRateLimiting(ReadRateLimitPolicy);

        api.MapGet("/transactions/{txId}", async (string txId, bool? includeData, LedgerQueryService queries, CancellationToken cancellationToken) =>
        {
            try
            {
                ValidateIdentifier(txId, LedgerValidationLimits.MaxTxIdLength, "transaction id");
                TransactionDetails? details = await queries.GetTransactionAsync(txId, includeData ?? false, cancellationToken).ConfigureAwait(false);
                return details is null ? Results.NotFound() : Results.Ok(details);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    title: "Ledger node request failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .AddEndpointFilter(ValidateLedgerReadAuthorizationAsync)
        .RequireRateLimiting(ReadRateLimitPolicy);
    }

    private static async ValueTask<object?> ValidateLedgerWriteAuthorizationAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        LedgerNodeHostOptions options = context.HttpContext.RequestServices.GetRequiredService<LedgerNodeHostOptions>();
        return options.ApiSecurityMode switch
        {
            LedgerNodeApiSecurityMode.Development => await next(context).ConfigureAwait(false),
            LedgerNodeApiSecurityMode.RequireApiKey => await ValidateApiKeyAuthorizationAsync(context, next).ConfigureAwait(false),
            LedgerNodeApiSecurityMode.RequireJwtBearer => await ValidateJwtBearerAuthorizationAsync(context, next, LedgerNodeAuthPolicies.WriteApi).ConfigureAwait(false),
            LedgerNodeApiSecurityMode.RequireApiKeyOrJwt => await ValidateApiKeyOrJwtAuthorizationAsync(context, next, LedgerNodeAuthPolicies.WriteApi).ConfigureAwait(false),
            _ => UnsupportedSecurityMode(context, options.ApiSecurityMode.ToString())
        };
    }

    private static async ValueTask<object?> ValidateLedgerAdminAuthorizationAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        LedgerNodeHostOptions options = context.HttpContext.RequestServices.GetRequiredService<LedgerNodeHostOptions>();
        return options.ApiSecurityMode switch
        {
            LedgerNodeApiSecurityMode.Development => await next(context).ConfigureAwait(false),
            LedgerNodeApiSecurityMode.RequireApiKey => await ValidateApiKeyAuthorizationAsync(context, next).ConfigureAwait(false),
            LedgerNodeApiSecurityMode.RequireJwtBearer => await ValidateJwtBearerAuthorizationAsync(context, next, LedgerNodeAuthPolicies.AdminApi).ConfigureAwait(false),
            LedgerNodeApiSecurityMode.RequireApiKeyOrJwt => await ValidateApiKeyOrJwtAuthorizationAsync(context, next, LedgerNodeAuthPolicies.AdminApi).ConfigureAwait(false),
            _ => UnsupportedSecurityMode(context, options.ApiSecurityMode.ToString())
        };
    }

    private static async ValueTask<object?> ValidateLedgerReadAuthorizationAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        LedgerNodeHostOptions options = context.HttpContext.RequestServices.GetRequiredService<LedgerNodeHostOptions>();
        return options.ReadSecurityMode switch
        {
            LedgerNodeApiReadSecurityMode.Public => await next(context).ConfigureAwait(false),
            LedgerNodeApiReadSecurityMode.RequireApiKey => await ValidateApiKeyAuthorizationAsync(context, next).ConfigureAwait(false),
            LedgerNodeApiReadSecurityMode.RequireJwtBearer => await ValidateJwtBearerAuthorizationAsync(context, next, LedgerNodeAuthPolicies.ReadApi).ConfigureAwait(false),
            LedgerNodeApiReadSecurityMode.RequireApiKeyOrJwt => await ValidateApiKeyOrJwtAuthorizationAsync(context, next, LedgerNodeAuthPolicies.ReadApi).ConfigureAwait(false),
            _ => UnsupportedSecurityMode(context, options.ReadSecurityMode.ToString())
        };
    }

    private static async ValueTask<object?> ValidateApiKeyAuthorizationAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        LedgerNodeHostOptions options = context.HttpContext.RequestServices.GetRequiredService<LedgerNodeHostOptions>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            LogAuthorizationFailure(context, "api_key_not_configured");
            return Results.Problem(
                title: "Ledger node authorization failed",
                detail: "Ledger node API key is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (HasValidApiKey(context.HttpContext, options))
        {
            return await next(context).ConfigureAwait(false);
        }

        LogAuthorizationFailure(context, "invalid_api_key");
        return Results.Problem(
            title: "Ledger node authorization failed",
            detail: "A valid economy node API key is required for this operation.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async ValueTask<object?> ValidateApiKeyOrJwtAuthorizationAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        string requiredPolicy)
    {
        LedgerNodeHostOptions options = context.HttpContext.RequestServices.GetRequiredService<LedgerNodeHostOptions>();
        if (HasValidApiKey(context.HttpContext, options))
        {
            return await next(context).ConfigureAwait(false);
        }

        return await ValidateJwtBearerAuthorizationAsync(context, next, requiredPolicy).ConfigureAwait(false);
    }

    private static async ValueTask<object?> ValidateJwtBearerAuthorizationAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        string requiredPolicy)
    {
        AuthenticateResult result = await context.HttpContext
            .AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme)
            .ConfigureAwait(false);
        if (result.Succeeded && result.Principal?.Identity?.IsAuthenticated == true)
        {
            context.HttpContext.User = result.Principal;
            AuthorizationResult authorization = await context.HttpContext.RequestServices
                .GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(result.Principal, context.HttpContext, requiredPolicy)
                .ConfigureAwait(false);
            if (authorization.Succeeded)
            {
                return await next(context).ConfigureAwait(false);
            }

            LogAuthorizationFailure(context, $"insufficient_scope:{requiredPolicy}");
            return Results.Problem(
                title: "Ledger node authorization failed",
                detail: $"The bearer token does not grant the required {requiredPolicy} policy.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        LogAuthorizationFailure(context, "invalid_jwt_bearer_token");
        return Results.Problem(
            title: "Ledger node authorization failed",
            detail: "A valid economy node bearer token is required for this operation.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private static object? UnsupportedSecurityMode(EndpointFilterInvocationContext context, string mode)
    {
        LogAuthorizationFailure(context, "unsupported_security_mode");
        return Results.Problem(
            title: "Ledger node authorization failed",
            detail: $"Unsupported economy node security mode: {mode}.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private static bool HasValidApiKey(HttpContext context, LedgerNodeHostOptions options)
        => !string.IsNullOrWhiteSpace(options.ApiKey)
           && context.Request.Headers.TryGetValue(options.ApiKeyHeaderName, out StringValues provided)
           && IsApiKeyMatch(options.ApiKey, provided.ToString());

    private static void LogAuthorizationFailure(EndpointFilterInvocationContext context, string reason)
    {
        ILogger logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CanDoItAll.Ledger.Node.SecurityAudit");
        logger.LogWarning(
            "Ledger node authorization denied: {Reason} {Method} {Path}",
            reason,
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path.Value);
    }

    private static ValueTask<object?> ValidateRequestBodySizeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        LedgerNodeHostOptions options = context.HttpContext.RequestServices.GetRequiredService<LedgerNodeHostOptions>();
        long? contentLength = context.HttpContext.Request.ContentLength;
        if (contentLength is > 0 && contentLength > options.MaxRequestBodyBytes)
        {
            return ValueTask.FromResult<object?>(Results.Problem(
                title: "Ledger node request failed",
                detail: $"Request body exceeds the configured {options.MaxRequestBodyBytes.ToString(CultureInfo.InvariantCulture)} byte limit.",
                statusCode: StatusCodes.Status413PayloadTooLarge));
        }

        return next(context);
    }

    private static bool IsApiKeyMatch(string expected, string provided)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static LedgerAddressQuery CreateLedgerAddressQuery(string ledgerId, string address, string? scenarioId)
    {
        ValidateAddress(address, "address");
        return new(CreateLedgerAccountContext(ledgerId, scenarioId), new Address(address));
    }

    private static async Task<IReadOnlyList<LedgerNodeSpendableOutput>> GetPublicSpendableOutputsAsync(
        LedgerQueryService queries,
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerSpendableOutput> outputs = await queries
            .GetSpendableOutputsAsync(query, cancellationToken)
            .ConfigureAwait(false);
        return outputs.Select(ToPublicSpendableOutput).ToArray();
    }

    private static LedgerNodeSpendableOutput ToPublicSpendableOutput(LedgerSpendableOutput output)
        => new(
            output.Context,
            output.OutPointKey,
            output.OutPoint,
            output.Address,
            output.MainAmount,
            output.Token,
            output.BusinessObject is null
                ? null
                : new LedgerNodeSpendableBusinessObjectDetails(
                    BusinessObjectId.FromOutPoint(output.OutPoint),
                    output.BusinessObject.LineageKey,
                    output.BusinessObject.Version,
                    output.BusinessObject.Operation,
                    output.BusinessObject.DataHash),
            output.CreatedAt);

    private static async Task<IReadOnlyList<LedgerNodeLedgerTransactionSummary>> ToPublicLedgerTransactionsAsync(
        Task<IReadOnlyList<LedgerTransactionSummary>> summariesTask)
    {
        IReadOnlyList<LedgerTransactionSummary> summaries = await summariesTask.ConfigureAwait(false);
        return summaries.Select(ToPublicLedgerTransaction).ToArray();
    }

    private static LedgerNodeLedgerTransactionSummary ToPublicLedgerTransaction(
        LedgerTransactionSummary summary)
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
                .Select(output => ToPublicLedgerTransactionOutput(summary.TxId, output))
                .ToArray(),
            summary.Data,
            summary.LedgerSequence);

    private static LedgerNodeLedgerTransactionOutputSummary ToPublicLedgerTransactionOutput(
        TxId txId,
        LedgerTransactionOutputSummary output)
    {
        LedgerBusinessObjectOutputDetails? details = output.BusinessObjectDetails;
        if (details is not null)
        {
            BusinessObjectId expectedObjectId = BusinessObjectId.FromOutPoint(txId, output.OutputIndex);
            if (details.ObjectId != expectedObjectId)
            {
                throw new InvalidOperationException(
                    $"Transaction {txId.Value} output {output.OutputIndex} has BusinessObject id {details.ObjectId.Value}; expected {expectedObjectId.Value}.");
            }
        }
        else if (output.BusinessObject is { } internalReference)
        {
            details = new LedgerBusinessObjectOutputDetails(
                BusinessObjectId.FromOutPoint(txId, output.OutputIndex),
                internalReference.Version,
                internalReference.Operation,
                internalReference.DataHash);
        }

        return new LedgerNodeLedgerTransactionOutputSummary(
            output.OutputIndex,
            output.Address,
            output.MainAmount,
            output.Token,
            details,
            output.MetadataHash);
    }

    private static LedgerAccountContext CreateLedgerAccountContext(string ledgerId, string? scenarioId)
    {
        ValidateIdentifier(ledgerId, LedgerValidationLimits.MaxLedgerIdLength, "ledger id");
        return new(new LedgerId(ledgerId), ToScenarioId(scenarioId));
    }

    private static LedgerBusinessObjectQuery CreateBusinessObjectQuery(
        string ledgerId,
        LedgerBusinessObjectQueryRequest request)
    {
        LedgerAccountContext context = CreateLedgerAccountContext(ledgerId, request.ScenarioId);
        Address? ownerAddress = null;
        if (!string.IsNullOrWhiteSpace(request.OwnerAddress))
        {
            ValidateAddress(request.OwnerAddress, "owner address");
            ownerAddress = new Address(request.OwnerAddress);
        }

        BusinessObjectId? objectId = ToCanonicalBusinessObjectId(request.ObjectId);

        return new LedgerBusinessObjectQuery(
            context,
            objectId,
            string.IsNullOrWhiteSpace(request.ObjectType) ? null : request.ObjectType.Trim(),
            string.IsNullOrWhiteSpace(request.SchemaVersion) ? null : request.SchemaVersion.Trim(),
            ownerAddress,
            request.StateProperties,
            request.Skip,
            request.Take);
    }

    private static LedgerTransactionQuery CreateLedgerTransactionQuery(
        string ledgerId,
        LedgerTransactionQueryRequest request)
    {
        LedgerAccountContext context = CreateLedgerAccountContext(ledgerId, request.ScenarioId);
        Address? address = null;
        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            ValidateAddress(request.Address, "address");
            address = new Address(request.Address);
        }

        BusinessObjectId? businessObjectId = ToCanonicalBusinessObjectId(request.BusinessObjectId);

        TransactionKind? kind = null;
        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            if (!Enum.TryParse(request.Kind, ignoreCase: true, out TransactionKind parsedKind))
            {
                throw new InvalidOperationException($"Unknown transaction kind: {request.Kind}.");
            }

            kind = parsedKind;
        }

        return new LedgerTransactionQuery(
            context,
            address,
            businessObjectId,
            kind,
            request.IncludeData,
            request.Skip,
            request.Take);
    }

    private static ScenarioId? ToScenarioId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        ValidateIdentifier(normalized, LedgerValidationLimits.MaxScenarioIdLength, "scenario id");
        return new ScenarioId(normalized);
    }

    private static BusinessObjectId? ToCanonicalBusinessObjectId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        ValidateIdentifier(value, LedgerValidationLimits.MaxBusinessObjectIdLength, "BusinessObject id");
        var objectId = new BusinessObjectId(value);
        if (!objectId.IsCanonicalOutPointId())
        {
            throw new InvalidOperationException(
                "BusinessObject id must be a canonical UTXO id in '64-character transaction-hash:output-index' format.");
        }

        return objectId;
    }

    private static void ValidateAddress(string value, string name)
    {
        if (!Address.IsValid(value))
        {
            throw new InvalidOperationException($"{name} is not a valid derived address.");
        }
    }

    private static void ValidateIdentifier(string value, int maxLength, string name)
    {
        if (!LedgerValueValidation.IsValidIdentifier(value, maxLength))
        {
            throw new InvalidOperationException($"{name} contains unsupported characters, is empty, or is too long.");
        }
    }
}
