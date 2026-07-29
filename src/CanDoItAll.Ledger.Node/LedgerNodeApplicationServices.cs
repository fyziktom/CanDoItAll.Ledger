using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Persistence.EFCore;
using CanDoItAll.Ledger.Persistence.EFCore.Records;
using CanDoItAll.Ledger.Sdk;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CanDoItAll.Ledger.Storage.InMemory;

namespace CanDoItAll.Ledger.Node;

public sealed class LedgerNodeBootstrapService(LedgerNodeService node)
{
    public Task<NodeBootstrapInfo> GetBootstrapInfoAsync(CancellationToken cancellationToken)
        => node.GetBootstrapInfoAsync(cancellationToken);
}

public sealed class LedgerCommandService(LedgerNodeService node)
{
    public Task ResetAsync(CancellationToken cancellationToken)
        => node.ResetAsync(cancellationToken);

    public Task<LedgerSubmitResult> SubmitLedgerTransactionAsync(
        LedgerTransaction transaction,
        CancellationToken cancellationToken)
        => node.SubmitLedgerTransactionAsync(transaction, cancellationToken);
}

public sealed class LedgerTreasuryCommandService(LedgerNodeService node)
{
    public Task<TransactionReceipt> MintAsync(MintTransactionRequest request, CancellationToken cancellationToken)
        => node.MintAsync(request, cancellationToken);

    public Task<TransactionReceipt> TransferAsync(TransferTransactionRequest request, CancellationToken cancellationToken)
        => node.TransferAsync(request, cancellationToken);

    public Task<TokenAuthorityGrantReceipt> GrantTokenCreationAuthorityAsync(
        TokenAuthorityGrantRequest request,
        CancellationToken cancellationToken)
        => node.GrantTokenCreationAuthorityAsync(request, cancellationToken);

    public Task<PermissionGrantReceipt> GrantPermissionAsync(
        PermissionGrantRequest request,
        CancellationToken cancellationToken)
        => node.GrantPermissionAsync(request, cancellationToken);
}

public sealed class LedgerQueryService(LedgerReadModelService readModels)
{
    public Task<LedgerState?> GetLedgerStateAsync(
        LedgerAccountContext context,
        CancellationToken cancellationToken)
        => readModels.GetLedgerStateAsync(context, cancellationToken);

    public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
        => readModels.GetSpendableOutputsAsync(query, cancellationToken);

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectHoldingsAsync(
        LedgerAddressQuery query,
        CancellationToken cancellationToken)
        => readModels.GetBusinessObjectHoldingsAsync(query, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> GetLedgerTransactionsAsync(
        LedgerAddressQuery query,
        bool includeData,
        CancellationToken cancellationToken)
        => readModels.GetLedgerTransactionsAsync(query, includeData, cancellationToken);

    public Task<LedgerTransactionSummary?> GetLedgerTransactionAsync(
        LedgerAccountContext context,
        TxId txId,
        bool includeData,
        CancellationToken cancellationToken)
        => readModels.GetLedgerTransactionAsync(context, txId, includeData, cancellationToken);

    public Task<LedgerBusinessObjectHolding?> GetBusinessObjectHoldingAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
        => readModels.GetBusinessObjectHoldingAsync(query, cancellationToken);

    public Task<IReadOnlyList<LedgerBusinessObjectHolding>> QueryBusinessObjectHoldingsAsync(
        LedgerBusinessObjectQuery query,
        CancellationToken cancellationToken)
        => readModels.QueryBusinessObjectHoldingsAsync(query, cancellationToken);

    public Task<IReadOnlyList<LedgerTransactionSummary>> QueryLedgerTransactionsAsync(
        LedgerTransactionQuery query,
        CancellationToken cancellationToken)
        => readModels.QueryLedgerTransactionsAsync(query, cancellationToken);

    public Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(CancellationToken cancellationToken)
        => readModels.GetAccountsAsync(cancellationToken);

    public Task<IReadOnlyList<TransactionSummary>> GetAddressTransactionsAsync(
        string address,
        CancellationToken cancellationToken)
        => readModels.GetAddressTransactionsAsync(address, cancellationToken);

    public Task<TransactionDetails?> GetTransactionAsync(
        string txId,
        bool includeData,
        CancellationToken cancellationToken)
        => readModels.GetTransactionAsync(txId, includeData, cancellationToken);
}

public sealed class LedgerNodeHealthService(
    IDbContextFactory<LedgerDbContext> dbContextFactory,
    EfLedgerHistoryReader historyReader,
    ITransactionHasher hasher,
    ISignaturePayloadBuilder payloadBuilder,
    ISignatureProviderRegistry signatureProviderRegistry,
    LedgerSemanticReplayVerifier semanticReplayVerifier,
    EfLedgerProjectionVerifier projectionVerifier,
    ITimeProvider timeProvider,
    LedgerNodeHostOptions options)
{
    private static readonly LedgerId DefaultLedgerId = new("default");
    private static readonly HashHex EmptyLedgerHash = new(new string('0', 64));

    public NodeHealthResponse GetLiveness()
    {
        DateTimeOffset checkedAt = timeProvider.UtcNow;
        return CreateHealthResponse(
            "ok",
            checkedAt,
            "not_checked",
            0,
            EmptyLedgerHash.Value,
            null,
            "not_checked",
            0,
            Array.Empty<string>(),
            "not_checked",
            EmptyLedgerHash.Value,
            0,
            Array.Empty<string>(),
            genesisCreated: false);
    }

    public async Task<NodeHealthResponse> GetReadinessAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset checkedAt = timeProvider.UtcNow;
        try
        {
            await using LedgerDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            LedgerStateRecord? state = await db.LedgerStates
                .AsNoTracking()
                .Where(x => x.LedgerId == DefaultLedgerId.Value && x.ScenarioId == null)
                .OrderByDescending(x => x.CurrentSequence)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<ConfirmedTransaction> history = await historyReader
                .ReadConfirmedTransactionsAsync(DefaultLedgerId, null, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyCollection<LedgerAdminVerificationKey> trustedAdminKeys = CreateTrustedAdminKeys(options);
            var verifier = new LedgerChainVerifier(
                hasher,
                payloadBuilder,
                trustedAdminKeys.Count == 0 ? null : signatureProviderRegistry,
                trustedAdminKeys);
            LedgerChainVerificationReport verification = verifier.Verify(history);
            LedgerSemanticReplayReport semanticVerification = await semanticReplayVerifier
                .VerifyAsync(history, new InMemoryLedgerStore(hasher), cancellationToken)
                .ConfigureAwait(false);
            EfLedgerProjectionVerificationReport projectionVerification = await projectionVerifier
                .VerifyAsync(DefaultLedgerId, null, cancellationToken)
                .ConfigureAwait(false);

            bool missingRequiredTrustedVerification =
                options.RequiresTrustedAdminVerification
                && history.Count > 0
                && trustedAdminKeys.Count == 0;
            string verificationStatus = missingRequiredTrustedVerification
                ? "missing_trusted_admin_key"
                : history.Count == 0
                    ? "empty"
                    : verification.IsValid && semanticVerification.IsValid && trustedAdminKeys.Count == 0
                        ? "valid_semantic_payload"
                        : verification.IsValid && semanticVerification.IsValid
                            ? "valid_semantic_trusted"
                            : "invalid";
            string[] verificationIssueCodes = verification.Issues
                .Select(x => x.Code.ToString())
                .Concat(semanticVerification.Issues.Select(x =>
                    $"Semantic:{x.ValidationCode ?? x.Code.ToString()}"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (missingRequiredTrustedVerification)
            {
                verificationIssueCodes = verificationIssueCodes
                    .Append("MissingTrustedAdminKey")
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
            }

            bool overallOk = verification.IsValid
                && semanticVerification.IsValid
                && projectionVerification.IsValid
                && !missingRequiredTrustedVerification;

            return CreateHealthResponse(
                overallOk ? "ok" : "degraded",
                checkedAt,
                db.Database.ProviderName ?? "unknown",
                state?.CurrentSequence ?? 0,
                state?.CurrentLedgerHash ?? EmptyLedgerHash.Value,
                state?.UpdatedAt,
                verificationStatus,
                verificationIssueCodes.Length,
                verificationIssueCodes,
                projectionVerification.IsValid ? "valid" : "invalid",
                projectionVerification.HashSet.ProjectionHash.Value,
                projectionVerification.Issues.Count,
                projectionVerification.Issues.Select(x => x.Code.ToString()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                state is not null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or System.Data.Common.DbException or TimeoutException)
        {
            return CreateHealthResponse(
                "degraded",
                checkedAt,
                "unavailable",
                0,
                EmptyLedgerHash.Value,
                null,
                "storage_error",
                1,
                new[] { ex.GetType().Name },
                "unknown",
                EmptyLedgerHash.Value,
                1,
                new[] { ex.GetType().Name },
                genesisCreated: false);
        }
    }

    private static IReadOnlyCollection<LedgerAdminVerificationKey> CreateTrustedAdminKeys(LedgerNodeHostOptions options)
        => options.LedgerAdminAccount is null
            ? Array.Empty<LedgerAdminVerificationKey>()
            : new[]
            {
                new LedgerAdminVerificationKey(
                    options.LedgerAdminAccount.KeyId,
                    options.LedgerAdminAccount.Algorithm,
                    options.LedgerAdminAccount.PublicKeyHash,
                    options.LedgerAdminAccount.PublicKeyMaterial)
            };

    private NodeHealthResponse CreateHealthResponse(
        string status,
        DateTimeOffset checkedAt,
        string storageProvider,
        long latestLedgerSequence,
        string latestLedgerHash,
        DateTimeOffset? ledgerStateUpdatedAt,
        string verificationStatus,
        int verificationIssueCount,
        IReadOnlyList<string> verificationIssueCodes,
        string projectionVerificationStatus,
        string projectionHash,
        int projectionIssueCount,
        IReadOnlyList<string> projectionIssueCodes,
        bool genesisCreated)
        => new(
            status,
            checkedAt,
            storageProvider,
            DefaultLedgerId.Value,
            latestLedgerSequence,
            latestLedgerHash,
            ledgerStateUpdatedAt,
            verificationStatus,
            checkedAt,
            verificationIssueCount,
            verificationIssueCodes,
            projectionVerificationStatus,
            projectionHash,
            projectionIssueCount,
            projectionIssueCodes,
            options.BootstrapTreasuryAdmin,
            genesisCreated,
            options.NormalizedApiVersion,
            options.VersionedApiBasePath,
            options.LegacyApiBasePath,
            options.SecurityProfile.ToString(),
            options.ApiSecurityMode.ToString(),
            options.ReadSecurityMode.ToString(),
            options.UiAuthMode.ToString(),
            options.RequireTwoFactorAuthentication,
            options.AuthPersistenceProvider.ToString(),
            options.JwtRequireActiveTokenRecord,
            options.UsesDevelopmentLedgerAdminSigner ? "Development" : "Configured",
            options.AllowLedgerReset,
            options.SchemaManagementMode.ToString(),
            options.RequiresTrustedAdminVerification);
}

internal static class LedgerNodeApiResults
{
    public static async Task<IResult> OkOrProblemAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await action(cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Ledger node request failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    public static async Task<IResult> NoContentOrProblemAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                title: "Ledger node request failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
