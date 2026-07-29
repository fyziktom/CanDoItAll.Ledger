using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;

namespace CanDoItAll.Ledger.Node;

public sealed record LedgerNodeUiAccount(
    string KeyId,
    string PublicKeyMaterial,
    string Address,
    string PublicKeyHash);

public sealed record LedgerNodeUiSnapshot(
    NodeHealthResponse Health,
    NodeBootstrapInfo Bootstrap,
    IReadOnlyList<AccountSummary> Accounts,
    string? SelectedAddress,
    BusinessObjectWorkspaceModel? Workspace,
    IReadOnlyList<NodeAdminAddressSummary> AdminAddresses,
    IReadOnlyList<NodeAdminTransactionSummary> AdminTransactions);

public sealed class LedgerNodeUiService(
    LedgerNodeBootstrapService bootstrapService,
    LedgerNodeHealthService healthService,
    LedgerQueryService queries,
    LedgerCommandService ledgerCommands,
    LedgerTreasuryCommandService treasuryCommands,
    ManualTimeProvider timeProvider)
{
    private static readonly LedgerAccountContext DefaultContext = new(new LedgerId("default"));
    private const string DefaultOperationsKeyId = "node-operations-console";
    private const string DefaultOperationsPublicKey = "candoitall-economy-node-operations-public-v1";

    public LedgerNodeUiAccount DefaultOperationsAccount => DeriveAccount(DefaultOperationsKeyId, DefaultOperationsPublicKey);

    public async Task<LedgerNodeUiSnapshot> GetSnapshotAsync(
        string? selectedAddress,
        CancellationToken cancellationToken)
    {
        NodeHealthResponse health = await healthService.GetReadinessAsync(cancellationToken).ConfigureAwait(false);
        NodeBootstrapInfo bootstrap = await bootstrapService.GetBootstrapInfoAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<AccountSummary> accounts = await queries.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
        string? resolvedAddress = ResolveSelectedAddress(selectedAddress, bootstrap, accounts);
        BusinessObjectWorkspaceModel? workspace = resolvedAddress is null
            ? null
            : await CreateWorkspaceAsync(resolvedAddress, accounts, bootstrap, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<NodeAdminTransactionSummary> adminTransactions = workspace?.Transactions
            .Select(ToAdminTransaction)
            .ToArray() ?? Array.Empty<NodeAdminTransactionSummary>();

        return new LedgerNodeUiSnapshot(
            health,
            bootstrap,
            accounts,
            resolvedAddress,
            workspace,
            accounts.Select(account => ToAdminAddress(account, bootstrap, adminTransactions)).ToArray(),
            adminTransactions);
    }

    public LedgerNodeUiAccount DeriveAccount(string keyId, string publicKeyMaterial)
    {
        string normalizedKeyId = string.IsNullOrWhiteSpace(keyId)
            ? DefaultOperationsKeyId
            : keyId.Trim();
        string normalizedPublicKey = string.IsNullOrWhiteSpace(publicKeyMaterial)
            ? throw new InvalidOperationException("Public key material is required.")
            : publicKeyMaterial.Trim();
        byte[] publicKeyBytes = Encoding.UTF8.GetBytes(normalizedPublicKey);
        Address address = Address.FromPublicKey(publicKeyBytes);
        HashHex publicKeyHash = Address.CalculatePublicKeyHash(publicKeyBytes);
        return new LedgerNodeUiAccount(normalizedKeyId, normalizedPublicKey, address.Value, publicKeyHash.Value);
    }

    public async Task ResetLedgerAsync(CancellationToken cancellationToken)
        => await ledgerCommands.ResetAsync(cancellationToken).ConfigureAwait(false);

    public void AdvanceTime(double days, double hours)
    {
        if (days < 0 || hours < 0)
        {
            throw new InvalidOperationException("Simulation time can only advance forward.");
        }

        timeProvider.Advance(TimeSpan.FromDays(days).Add(TimeSpan.FromHours(hours)));
    }

    public async Task<TransactionReceipt> MintAsync(MintTransactionRequest request, CancellationToken cancellationToken)
        => await treasuryCommands.MintAsync(request, cancellationToken).ConfigureAwait(false);

    public async Task<TransactionReceipt> TransferAsync(TransferTransactionRequest request, CancellationToken cancellationToken)
        => await treasuryCommands.TransferAsync(request, cancellationToken).ConfigureAwait(false);

    public async Task<PermissionGrantReceipt> GrantPermissionAsync(PermissionGrantRequest request, CancellationToken cancellationToken)
        => await treasuryCommands.GrantPermissionAsync(request, cancellationToken).ConfigureAwait(false);

    public async Task<string> RunDemoFlowAsync(string operationsPublicKey, CancellationToken cancellationToken)
    {
        await ledgerCommands.ResetAsync(cancellationToken).ConfigureAwait(false);
        NodeBootstrapInfo bootstrap = await bootstrapService.GetBootstrapInfoAsync(cancellationToken).ConfigureAwait(false);
        LedgerNodeUiAccount operations = DeriveAccount(DefaultOperationsKeyId, operationsPublicKey);
        PermissionGrantReceipt grant = await treasuryCommands
            .GrantPermissionAsync(
                new PermissionGrantRequest(
                    "messages",
                    operations.Address,
                    Amount: 1m,
                    Repeatable: true,
                    Action: PermissionTokenAuthority.MintNewTokenTypeAction,
                    ScenarioId: null),
                cancellationToken)
            .ConfigureAwait(false);

        TransactionReceipt transfer = await treasuryCommands
            .TransferAsync(
                new TransferTransactionRequest(
                    bootstrap.AdminAddress,
                    operations.Address,
                    250m,
                    null,
                    null,
                    TransactionDataItemType.BusinessObject.ToString(),
                    "{\"source\":\"demo\",\"purpose\":\"operations-budget\"}",
                    null,
                    "bo_operations_budget",
                    null),
                cancellationToken)
            .ConfigureAwait(false);

        return $"Demo flow accepted. Grant {grant.TxId}; transfer {transfer.TxId}.";
    }

    public async Task<LedgerTransactionDetailsModel?> GetTransactionDetailsAsync(
        string txId,
        CancellationToken cancellationToken)
    {
        TransactionDetails? details = await queries.GetTransactionAsync(txId, includeData: true, cancellationToken).ConfigureAwait(false);
        return details is null ? null : ToTransactionDetails(details);
    }

    private async Task<BusinessObjectWorkspaceModel> CreateWorkspaceAsync(
        string addressText,
        IReadOnlyList<AccountSummary> accounts,
        NodeBootstrapInfo bootstrap,
        CancellationToken cancellationToken)
    {
        Address address = new(addressText);
        var query = new LedgerAddressQuery(DefaultContext, address);
        AccountSummary? summary = accounts.FirstOrDefault(account => account.Address.Equals(address.Value, StringComparison.Ordinal));
        DateTimeOffset viewedAt = timeProvider.UtcNow;
        LedgerAccountBalance balance = CreateBalance(query, summary, viewedAt);
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await queries
            .GetSpendableOutputsAsync(query, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerBusinessObjectHolding> holdings = await queries
            .GetBusinessObjectHoldingsAsync(query, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await queries
            .GetLedgerTransactionsAsync(query, includeData: true, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTokenType> tokenTypes = CreateTokenTypes(spendableOutputs);
        IReadOnlyList<LedgerTokenCreationAuthority> tokenAuthorities = [];
        IReadOnlyList<IBusinessObjectView> businessObjects = holdings
            .Select(holding => ToBusinessObjectView(query.Context, holding, viewedAt))
            .ToArray();
        Address peerAddress = businessObjects.FirstOrDefault()?.OwnerAddress ?? address;
        var draft = new BusinessObjectTransactionDraft(
            default,
            "node-console",
            "1.0",
            address,
            peerAddress,
            peerAddress,
            Amount.Zero,
            Amount.FromInt64(1),
            "{\"source\":\"node-console\"}",
            $"node://{bootstrap.LedgerId}/console/{viewedAt:yyyyMMddHHmmss}");
        IBusinessObjectView? selectedObject = businessObjects.FirstOrDefault();
        var editor = new BusinessObjectEditorModel(
            selectedObject?.ObjectId ?? draft.ObjectId,
            selectedObject?.ObjectType ?? draft.ObjectType,
            selectedObject?.SchemaVersion ?? draft.SchemaVersion,
            address,
            selectedObject?.StateJson ?? draft.StateJson,
            viewedAt);

        return new BusinessObjectWorkspaceModel(
            IsAdminAddress(address.Value, bootstrap) ? "Treasury admin account" : "Node account",
            query.Context,
            address,
            balance,
            CreateTokenOptions(balance, tokenTypes, tokenAuthorities),
            tokenAuthorities,
            CreateBookmarks(accounts, bootstrap, address.Value),
            businessObjects,
            CreateSubAccounts(accounts, bootstrap, address.Value, viewedAt),
            transactions,
            draft,
            editor,
            viewedAt,
            AccountAlias: IsAdminAddress(address.Value, bootstrap) ? "treasury-admin" : "node-account",
            ConnectionLabel: "In-process node",
            SpendableOutputs: spendableOutputs,
            TokenTypes: tokenTypes,
            PermissionSources: []);
    }

    private static string? ResolveSelectedAddress(
        string? selectedAddress,
        NodeBootstrapInfo bootstrap,
        IReadOnlyList<AccountSummary> accounts)
    {
        if (!string.IsNullOrWhiteSpace(selectedAddress)
            && accounts.Any(account => account.Address.Equals(selectedAddress.Trim(), StringComparison.Ordinal)))
        {
            return selectedAddress.Trim();
        }

        return accounts.FirstOrDefault(account => IsAdminAddress(account.Address, bootstrap))?.Address
            ?? accounts.FirstOrDefault()?.Address;
    }

    private static LedgerAccountBalance CreateBalance(
        LedgerAddressQuery query,
        AccountSummary? summary,
        DateTimeOffset viewedAt)
    {
        IReadOnlyDictionary<TokenId, Amount> tokens = summary?.TokenBalances
            .ToDictionary(pair => new TokenId(pair.Key), pair => new Amount(pair.Value))
            ?? new Dictionary<TokenId, Amount>();
        return new LedgerAccountBalance(
            query.Context,
            query.Address,
            new Amount(summary?.MainBalance ?? 0m),
            tokens,
            summary?.UtxoCount ?? 0,
            viewedAt);
    }

    private static IReadOnlyList<LedgerAddressBookmark> CreateBookmarks(
        IReadOnlyList<AccountSummary> accounts,
        NodeBootstrapInfo bootstrap,
        string selectedAddress)
        => accounts
            .Where(account => !account.Address.Equals(selectedAddress, StringComparison.Ordinal))
            .Take(8)
            .Select(account => new LedgerAddressBookmark(
                IsAdminAddress(account.Address, bootstrap) ? "Treasury admin" : $"Account {Short(account.Address)}",
                new Address(account.Address)))
            .ToArray();

    private static IReadOnlyList<BusinessObjectSubAccountSummary> CreateSubAccounts(
        IReadOnlyList<AccountSummary> accounts,
        NodeBootstrapInfo bootstrap,
        string selectedAddress,
        DateTimeOffset viewedAt)
        => accounts
            .Where(account => !account.Address.Equals(selectedAddress, StringComparison.Ordinal))
            .Take(6)
            .Select(account => new BusinessObjectSubAccountSummary(
                IsAdminAddress(account.Address, bootstrap) ? "Treasury admin" : $"Account {Short(account.Address)}",
                new Address(account.Address),
                new Amount(account.MainBalance),
                account.UtxoCount,
                BusinessObjectCount: 0,
                viewedAt))
            .ToArray();

    private static IReadOnlyList<LedgerTokenType> CreateTokenTypes(IReadOnlyList<LedgerSpendableOutput> outputs)
        => outputs
            .Where(output => output.Token is not null)
            .GroupBy(output => output.Token!.TokenId)
            .Select(group =>
            {
                Amount total = new(group.Sum(output => output.Token?.Amount.Value ?? 0m));
                int sourceCount = group.Count(output => output.Token?.Amount.Value > 1m);
                int uniqueCount = group.Count(output => output.Token?.Amount.Value == 1m);
                return new LedgerTokenType(
                    group.Key,
                    total,
                    new Amount(group.Where(output => output.Token?.Amount.Value > 1m).Sum(output => output.Token?.Amount.Value ?? 0m)),
                    new Amount(group.Where(output => output.Token?.Amount.Value == 1m).Sum(output => output.Token?.Amount.Value ?? 0m)),
                    sourceCount,
                    uniqueCount,
                    group.Count());
            })
            .OrderBy(type => type.TokenId.Value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<LedgerTokenOption> CreateTokenOptions(
        LedgerAccountBalance balance,
        IReadOnlyList<LedgerTokenType> tokenTypes,
        IReadOnlyList<LedgerTokenCreationAuthority> tokenAuthorities)
    {
        Dictionary<TokenId, LedgerTokenType> tokenTypeById = tokenTypes.ToDictionary(type => type.TokenId);
        return
        [
            new(LedgerTokenOption.MainKey, "Main", null, balance.MainBalance, IsMain: true),
            .. balance.TokenBalances
                .OrderBy(token => token.Key.Value, StringComparer.Ordinal)
                .Select(token =>
                {
                    tokenTypeById.TryGetValue(token.Key, out LedgerTokenType? tokenType);
                    return new LedgerTokenOption(
                        token.Key.Value,
                        token.Key.Value,
                        token.Key,
                        token.Value,
                        IsMain: false,
                        CanCreateMore: tokenAuthorities.Any(authority => PermissionTokenAuthority.IsTokenInNamespace(token.Key, authority.Namespace)),
                        SourceAmount: tokenType?.SourceAmount ?? Amount.Zero,
                        UniqueAmount: tokenType?.UniqueAmount ?? Amount.Zero,
                        SourceUtxoCount: tokenType?.SourceUtxoCount ?? 0,
                        UniqueUtxoCount: tokenType?.UniqueUtxoCount ?? 0,
                        TotalUtxoCount: tokenType?.TotalUtxoCount ?? 0);
                })
        ];
    }

    private static IBusinessObjectView ToBusinessObjectView(
        LedgerAccountContext context,
        LedgerBusinessObjectHolding holding,
        DateTimeOffset viewedAt)
        => new GenericBusinessObjectView(
            context,
            holding.ObjectId,
            holding.ObjectType,
            holding.SchemaVersion,
            holding.Version,
            holding.OwnerAddress,
            holding.Operation,
            holding.DataHash,
            holding.CurrentOutPoint,
            holding.StateJson,
            holding.ExternalRef,
            holding.ItemType,
            holding.CreatedAt,
            viewedAt);

    private static NodeAdminAddressSummary ToAdminAddress(
        AccountSummary account,
        NodeBootstrapInfo bootstrap,
        IReadOnlyList<NodeAdminTransactionSummary> transactions)
        => new(
            IsAdminAddress(account.Address, bootstrap) ? "Treasury admin" : $"Account {Short(account.Address)}",
            account.Address,
            IsAdminAddress(account.Address, bootstrap) ? "Admin" : "Ledger account",
            $"{account.MainBalanceText} main",
            0,
            transactions.Count(tx => tx.PrimaryAddress.Equals(account.Address, StringComparison.Ordinal)
                || tx.Route.Contains(account.Address, StringComparison.Ordinal)),
            "Active",
            transactions
                .Where(tx => tx.PrimaryAddress.Equals(account.Address, StringComparison.Ordinal)
                    || tx.Route.Contains(account.Address, StringComparison.Ordinal))
                .Select(tx => tx.TimestampUtc)
                .DefaultIfEmpty(DateTimeOffset.UtcNow)
                .Max());

    private static NodeAdminTransactionSummary ToAdminTransaction(LedgerTransactionSummary transaction)
        => new(
            transaction.TxId.Value,
            $"{FirstAddress(transaction.InputAddresses)} -> {FirstAddress(transaction.OutputAddresses)}",
            transaction.Kind,
            transaction.Status,
            transaction.Inputs.Count,
            transaction.Outputs.Count,
            FirstAddressOrEmpty(transaction.OutputAddresses, transaction.InputAddresses),
            transaction.Data?.ItemType.ToString() ?? "Ledger",
            transaction.Timestamp);

    private static LedgerTransactionDetailsModel ToTransactionDetails(TransactionDetails details)
    {
        var inputs = details.Inputs
            .Select(input => new LedgerTransactionInputDetails(
                input.InputIndex,
                new OutPoint(new TxId(input.PreviousTxId), input.PreviousOutputIndex),
                new Address(input.Address),
                MainAmount: null,
                Token: null,
                BusinessObject: null,
                input.SignatureBundle,
                ToHash(input.SignaturePayloadHash),
                ToOutPointKey(input.OutPointKey)))
            .ToArray();
        var outputs = details.Outputs
            .Select(output => new LedgerTransactionOutputDetails(
                output.OutputIndex,
                new Address(output.Address),
                new Amount(output.MainAmount),
                string.IsNullOrWhiteSpace(output.TokenId) ? null : new TokenTransfer(new TokenId(output.TokenId), new Amount(output.TokenAmount ?? 0m)),
                string.IsNullOrWhiteSpace(output.BusinessObjectId) ? null : new LedgerTransactionBusinessObjectDetails(
                    output.BusinessObjectVersion ?? 0,
                    ParseEnum(output.BusinessObjectOperation, BusinessObjectOperation.Create),
                    ToHash(output.BusinessObjectDataHash) ?? EmptyHash()),
                ToHash(output.BusinessObjectDataHash)))
            .ToArray();

        return new LedgerTransactionDetailsModel(
            new TxId(details.TxId),
            ToHash(details.TxHash),
            ParseEnum(details.Kind, TransactionKind.Transfer),
            ParseEnum(details.Status, TransactionStatus.Confirmed),
            SigningMode.Independent,
            details.ConfirmedAt ?? details.Timestamp,
            CreateTransactionData(details),
            inputs,
            outputs,
            details.LedgerSequence,
            ToHash(details.LedgerEntryHash));
    }

    private static TransactionData? CreateTransactionData(TransactionDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.DataContentType)
            && string.IsNullOrWhiteSpace(details.DataHash)
            && string.IsNullOrWhiteSpace(details.InlineJson)
            && string.IsNullOrWhiteSpace(details.ExternalRef))
        {
            return null;
        }

        return new TransactionData(
            details.DataContentType,
            ToHash(details.DataHash),
            details.InlineJson,
            details.ExternalRef,
            ParseEnum(details.DataItemType, TransactionDataItemType.Generic));
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
        => Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;

    private static HashHex? ToHash(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new HashHex(value);

    private static OutPointKey? ToOutPointKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new OutPointKey(new HashHex(value));

    private static HashHex EmptyHash()
        => new(Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant());

    private static bool IsAdminAddress(string address, NodeBootstrapInfo bootstrap)
        => address.Equals(bootstrap.AdminAddress, StringComparison.Ordinal);

    private static string FirstAddress(IReadOnlyList<Address> addresses)
        => addresses.Count == 0 ? "genesis" : addresses[0].Value;

    private static string FirstAddressOrEmpty(IReadOnlyList<Address> primary, IReadOnlyList<Address> secondary)
        => primary.Count > 0
            ? primary[0].Value
            : secondary.Count > 0
                ? secondary[0].Value
                : string.Empty;

    private static string Short(string value)
        => value.Length <= 12
            ? value
            : string.Create(CultureInfo.InvariantCulture, $"{value[..8]}...{value[^4..]}");
}
