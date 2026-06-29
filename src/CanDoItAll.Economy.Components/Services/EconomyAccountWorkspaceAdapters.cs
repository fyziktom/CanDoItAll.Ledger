using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Economy.Components.Services;

public interface IEconomyAccountWorkspaceAdapter
{
    string HostMode { get; }

    bool UsesServerSideAccountSigning { get; }

    Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default);

    Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(TxId txId, CancellationToken cancellationToken = default);

    Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default);
}

public sealed record WasmAccountWorkspaceOptions
{
    public string LedgerId { get; init; } = "default";

    public string? ScenarioId { get; init; }

    public string AccountName { get; init; } = "Account";

    public string ConnectionLabel { get; init; } = "Browser API";

    public Uri? NodeBaseUri { get; init; }
}

public sealed record NodeAccountWorkspaceAdapterOptions
{
    public string AccountName { get; init; } = "alice";
}

public sealed class WasmAccountWorkspaceAdapter(
    ILedgerGateway ledgerGateway,
    ILedgerReadClient ledgerReadClient,
    BusinessObjectTransactionBuilder transactionBuilder,
    IAccountTransactionSigner transactionSigner,
    IBusinessObjectCastRegistry castRegistry,
    ITimeProvider timeProvider,
    WasmAccountWorkspaceOptions? options = null) : IEconomyAccountWorkspaceAdapter
{
    private readonly WasmAccountWorkspaceOptions options = options ?? new();
    private IAccountKeySource? keySource;
    private string? accountNameOverride;

    public string HostMode => "WASM";

    public bool UsesServerSideAccountSigning => false;

    public ValueTask UseUnlockedAccountAsync(
        AccountVaultUnlockedKey unlockedKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unlockedKey);
        keySource = new AccountVaultKeySource(unlockedKey);
        accountNameOverride = unlockedKey.DisplayName;
        return ValueTask.CompletedTask;
    }

    public ValueTask UseKeySourceAsync(
        IAccountKeySource accountKeySource,
        string? accountName = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        keySource = accountKeySource ?? throw new ArgumentNullException(nameof(accountKeySource));
        accountNameOverride = accountName;
        return ValueTask.CompletedTask;
    }

    public async Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        LedgerAccountBalance balance = await account.GetBalanceAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await account.GetSpendableOutputsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerTokenType> tokenTypes = await account.GetTokenTypesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerPermissionSource> permissionSources = await account.GetPermissionSourcesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IBusinessObjectView> businessObjects = await account
            .GetBusinessObjectViewsAsync(castRegistry, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await account
            .GetTransactionsAsync(maxCount: 100, includeData: true, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<EconomyTokenCreationAuthority> authorities = CreateTokenCreationAuthorities(permissionSources);
        IReadOnlyList<EconomyTokenOption> tokenOptions = CreateTokenOptions(balance, tokenTypes, authorities);
        IReadOnlyList<EconomyAddressBookmark> bookmarks = CreateAddressBookmarks(account.Address, transactions);
        Address receiver = bookmarks.FirstOrDefault(bookmark => bookmark.Address != account.Address)?.Address ?? account.Address;
        DateTimeOffset now = timeProvider.UtcNow;

        var draft = new BusinessObjectTransactionDraft(
            new BusinessObjectId($"bo:account-app:{now:yyyyMMddHHmmss}"),
            "account-app-message",
            "1.0",
            account.Address,
            receiver,
            receiver,
            Amount.Zero,
            Amount.FromInt64(1),
            "{\"source\":\"account-app\"}",
            $"account-app://draft/{now:yyyyMMddHHmmss}");

        IBusinessObjectView? selected = businessObjects.FirstOrDefault();
        var editor = new BusinessObjectEditorModel(
            selected?.ObjectId ?? draft.ObjectId,
            selected?.ObjectType ?? draft.ObjectType,
            selected?.SchemaVersion ?? draft.SchemaVersion,
            account.Address,
            selected?.StateJson ?? draft.StateJson,
            now.AddDays(30));

        return new BusinessObjectWorkspaceModel(
            accountNameOverride ?? options.AccountName,
            Context,
            account.Address,
            balance,
            tokenOptions,
            authorities,
            bookmarks,
            businessObjects,
            [],
            transactions,
            draft,
            editor,
            now,
            AccountAlias: keySource?.KeyId,
            NodeBaseUri: options.NodeBaseUri,
            ConnectionLabel: options.ConnectionLabel,
            SpendableOutputs: spendableOutputs,
            TokenTypes: tokenTypes,
            PermissionSources: permissionSources);
    }

    public async Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(
        TxId txId,
        CancellationToken cancellationToken = default)
    {
        LedgerTransactionSummary? summary = await ledgerReadClient
            .GetTransactionAsync(Context, txId, includeData: true, cancellationToken)
            .ConfigureAwait(false);
        return summary is null ? null : ToDetails(summary);
    }

    public async Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default)
    {
        BusinessObjectAccount account = CreateAccount();
        LedgerTransaction unsigned = await BuildUnsignedTransactionAsync(account, draft, cancellationToken).ConfigureAwait(false);
        LedgerTransaction signed = await transactionSigner.SignAsync(unsigned, RequiredKeySource, cancellationToken).ConfigureAwait(false);
        LedgerSubmitResult result = await ledgerGateway.SubmitTransactionAsync(signed, cancellationToken).ConfigureAwait(false);

        return new EconomyTransactionExecutionResult(
            draft.SelectedMode,
            result.Accepted,
            result.TxId,
            result.LedgerSequence,
            result.Accepted
                ? $"{LabelFor(draft.SelectedMode)} accepted at ledger sequence {result.LedgerSequence}."
                : string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}")));
    }

    private LedgerAccountContext Context
        => new(new LedgerId(options.LedgerId), string.IsNullOrWhiteSpace(options.ScenarioId) ? null : new ScenarioId(options.ScenarioId));

    private IAccountKeySource RequiredKeySource
        => keySource ?? throw new InvalidOperationException("The WASM account workspace adapter needs an unlocked account key before use.");

    private BusinessObjectAccount CreateAccount()
        => new(Context, RequiredKeySource, ledgerGateway, ledgerReadClient, transactionBuilder);

    private Task<LedgerTransaction> BuildUnsignedTransactionAsync(
        BusinessObjectAccount account,
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken)
        => draft.SelectedMode switch
        {
            BusinessObjectTransactionMode.Create => account.BuildUnsignedCreateBusinessObjectTransactionAsync(
                new CreateBusinessObjectRequest(
                    draft.ObjectId,
                    SafeText(draft.ObjectType, "business-object"),
                    SafeJson(draft.StateJson),
                    SafeText(draft.SchemaVersion, "1.0"),
                    draft.OwnerAddress,
                    draft.MainAmount,
                    draft.TokenId is not null && draft.TokenAmount is not null
                        ? new TokenTransfer(draft.TokenId.Value, draft.TokenAmount.Value)
                        : null,
                    ExternalRef: SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.TransferObject => account.BuildUnsignedTransferBusinessObjectTransactionAsync(
                new TransferBusinessObjectRequest(
                    draft.ObjectId,
                    draft.ReceiverAddress,
                    draft.MainAmount,
                    SafeJson(draft.StateJson),
                    SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.UpdateObject => account.BuildUnsignedUpdateBusinessObjectTransactionAsync(
                new UpdateBusinessObjectRequest(draft.ObjectId, SafeJson(draft.StateJson), SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.CloseObject => account.BuildUnsignedCloseBusinessObjectTransactionAsync(
                new CloseBusinessObjectRequest(draft.ObjectId, SafeJson(draft.StateJson), SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.CloneObject => account.BuildUnsignedCloneBusinessObjectTransactionAsync(
                new CloneBusinessObjectRequest(
                    draft.SourceObjectId ?? draft.ObjectId,
                    draft.TargetObjectId ?? new BusinessObjectId($"{draft.ObjectId.Value}:copy"),
                    SafeText(draft.ObjectType, "business-object"),
                    SafeJson(draft.StateJson),
                    SafeText(draft.SchemaVersion, "1.0"),
                    draft.OwnerAddress),
                cancellationToken),

            BusinessObjectTransactionMode.MergeObjects => account.BuildUnsignedMergeBusinessObjectsTransactionAsync(
                new MergeBusinessObjectsRequest(
                    draft.SourceObjectId ?? draft.ObjectId,
                    draft.SecondObjectId ?? draft.ObjectId,
                    draft.TargetObjectId ?? new BusinessObjectId($"{draft.ObjectId.Value}:merged"),
                    SafeJson(draft.StateJson),
                    SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.SplitBusinessObjectPayment => account.BuildUnsignedSplitBusinessObjectPaymentTransactionAsync(
                new SplitBusinessObjectPaymentRequest(
                    draft.ObjectId,
                    new[]
                    {
                        new BusinessObjectPaymentOutput(draft.PaymentReceiverAddress, PositiveOrDefault(draft.PaymentAmount, 1)),
                        new BusinessObjectPaymentOutput(draft.ReceiverAddress, PositiveOrDefault(draft.SecondaryPaymentAmount ?? Amount.Zero, 1))
                    },
                    SafeJson(draft.StateJson),
                    SafeNullable(draft.ExternalRef)),
                cancellationToken),

            BusinessObjectTransactionMode.MainPayment => account.BuildUnsignedPaymentTransactionAsync(
                new PaymentRequest(draft.PaymentReceiverAddress, PositiveOrDefault(draft.PaymentAmount, 1)),
                cancellationToken),

            BusinessObjectTransactionMode.TokenTransfer => account.BuildUnsignedTokenTransferTransactionAsync(
                new TokenTransferRequest(
                    draft.TokenId ?? new TokenId("token"),
                    PositiveOrDefault(draft.TokenAmount ?? Amount.Zero, 1),
                    draft.PaymentReceiverAddress,
                    draft.MainAmount),
                cancellationToken),

            _ => throw new InvalidOperationException($"Unsupported transaction mode: {draft.SelectedMode}.")
        };

    private static IReadOnlyList<EconomyTokenCreationAuthority> CreateTokenCreationAuthorities(
        IReadOnlyList<LedgerPermissionSource> permissionSources)
        => permissionSources
            .Where(source => source.Output.BusinessObject is not null)
            .Select(source => new EconomyTokenCreationAuthority(
                source.Grant.Namespace,
                source.Output.BusinessObject!.ObjectId,
                source.Output.Address))
            .Distinct()
            .ToArray();

    private static IReadOnlyList<EconomyTokenOption> CreateTokenOptions(
        LedgerAccountBalance balance,
        IReadOnlyList<LedgerTokenType> tokenTypes,
        IReadOnlyList<EconomyTokenCreationAuthority> authorities)
    {
        var rows = new List<EconomyTokenOption>
        {
            new(EconomyTokenOption.MainKey, "Main", null, balance.MainBalance, IsMain: true)
        };

        foreach (KeyValuePair<TokenId, Amount> token in balance.TokenBalances.OrderBy(token => token.Key.Value, StringComparer.Ordinal))
        {
            LedgerTokenType? type = tokenTypes.FirstOrDefault(item => item.TokenId == token.Key);
            rows.Add(new EconomyTokenOption(
                token.Key.Value,
                token.Key.Value,
                token.Key,
                token.Value,
                IsMain: false,
                CanCreateMore: authorities.Any(authority => PermissionTokenAuthority.IsTokenInNamespace(token.Key, authority.Namespace)),
                SourceAmount: type?.SourceAmount ?? Amount.Zero,
                UniqueAmount: type?.UniqueAmount ?? Amount.Zero,
                SourceUtxoCount: type?.SourceUtxoCount ?? 0,
                UniqueUtxoCount: type?.UniqueUtxoCount ?? 0,
                TotalUtxoCount: type?.TotalUtxoCount ?? 0));
        }

        return rows;
    }

    private static IReadOnlyList<EconomyAddressBookmark> CreateAddressBookmarks(
        Address accountAddress,
        IReadOnlyList<LedgerTransactionSummary> transactions)
    {
        Address[] addresses = transactions
            .SelectMany(transaction => transaction.InputAddresses.Concat(transaction.OutputAddresses))
            .Where(address => address != accountAddress)
            .Distinct()
            .Take(6)
            .ToArray();

        return addresses.Length == 0
            ? [new EconomyAddressBookmark("Current account", accountAddress)]
            : addresses.Select((address, index) => new EconomyAddressBookmark($"Recent address {index + 1}", address)).ToArray();
    }

    private static EconomyTransactionDetailsModel ToDetails(LedgerTransactionSummary summary)
        => new(
            summary.TxId,
            TxHash: null,
            summary.Kind,
            summary.Status,
            SigningMode.Independent,
            summary.Timestamp,
            summary.Data,
            summary.Inputs
                .OrderBy(input => input.InputIndex)
                .Select(input => new EconomyTransactionInputDetails(
                    input.InputIndex,
                    input.PreviousOutPoint,
                    input.Address,
                    MainAmount: null,
                    Token: null,
                    BusinessObject: null,
                    input.SignatureBundle,
                    input.SignaturePayloadHash,
                    input.OutPointKey))
                .ToArray(),
            summary.Outputs
                .OrderBy(output => output.OutputIndex)
                .Select(output => new EconomyTransactionOutputDetails(
                    output.OutputIndex,
                    output.Address,
                    output.MainAmount,
                    output.Token,
                    output.BusinessObject,
                    output.MetadataHash))
                .ToArray());

    private static string LabelFor(BusinessObjectTransactionMode mode)
        => mode switch
        {
            BusinessObjectTransactionMode.MainPayment => "Payment",
            BusinessObjectTransactionMode.TokenTransfer => "Token transfer",
            BusinessObjectTransactionMode.TransferObject => "BusinessObject transfer",
            BusinessObjectTransactionMode.UpdateObject => "BusinessObject update",
            BusinessObjectTransactionMode.CloseObject => "BusinessObject close",
            BusinessObjectTransactionMode.CloneObject => "BusinessObject clone",
            BusinessObjectTransactionMode.MergeObjects => "BusinessObject merge",
            BusinessObjectTransactionMode.SplitBusinessObjectPayment => "BusinessObject split payment",
            _ => "Transaction"
        };

    private static string SafeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string SafeJson(string? value)
        => string.IsNullOrWhiteSpace(value) ? "{}" : value;

    private static string? SafeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Amount PositiveOrDefault(Amount amount, long fallback)
        => amount.Value > 0m ? amount : Amount.FromInt64(fallback);
}

public sealed class DevelopmentEconomyWorkspaceAdapter(EconomyDevelopmentWorkspaceSession session) : IEconomyAccountWorkspaceAdapter
{
    public string HostMode => "SSR";

    public bool UsesServerSideAccountSigning => true;

    public Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
        => session.GetWorkspaceAsync(cancellationToken);

    public Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(TxId txId, CancellationToken cancellationToken = default)
        => session.GetTransactionDetailsAsync(txId, cancellationToken);

    public Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default)
        => session.SubmitTransactionAsync(draft, cancellationToken);
}

public sealed class NodeEconomyWorkspaceAdapter(
    EconomyNodeWorkspaceSession session,
    NodeAccountWorkspaceAdapterOptions? options = null) : IEconomyAccountWorkspaceAdapter
{
    private readonly NodeAccountWorkspaceAdapterOptions options = options ?? new();

    public string HostMode => "SSR";

    public bool UsesServerSideAccountSigning => true;

    public Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
        => session.GetWorkspaceAsync(options.AccountName, cancellationToken);

    public Task<EconomyTransactionDetailsModel?> GetTransactionDetailsAsync(TxId txId, CancellationToken cancellationToken = default)
        => session.GetTransactionDetailsAsync(txId, cancellationToken);

    public Task<EconomyTransactionExecutionResult> SubmitTransactionAsync(
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default)
        => session.SubmitTransactionAsync(options.AccountName, draft, cancellationToken);
}

public static class EconomyAccountWorkspaceAdapterServiceCollectionExtensions
{
    public static IServiceCollection AddEconomyWasmAccountWorkspaceAdapter(this IServiceCollection services)
    {
        services.TryAddScoped<ITransactionCanonicalizer, DefaultTransactionCanonicalizer>();
        services.TryAddScoped<ITransactionHasher, TransactionHasher>();
        services.TryAddScoped<ISignaturePayloadBuilder, SignaturePayloadBuilder>();
        services.TryAddScoped<ITimeProvider, ComponentSystemClock>();
        services.AddBusinessObjectBrowserAccountSigning();
        services.AddBusinessObjectCasting();
        services.TryAddScoped<BusinessObjectTransactionBuilder>();
        services.TryAddScoped<WasmAccountWorkspaceAdapter>();
        services.TryAddScoped<IEconomyAccountWorkspaceAdapter>(provider => provider.GetRequiredService<WasmAccountWorkspaceAdapter>());
        return services;
    }

    public static IServiceCollection AddEconomySsrAccountWorkspaceAdapters(this IServiceCollection services)
    {
        services.TryAddScoped<DevelopmentEconomyWorkspaceAdapter>();
        services.TryAddScoped<NodeEconomyWorkspaceAdapter>();
        return services;
    }

    private sealed class ComponentSystemClock : ITimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
