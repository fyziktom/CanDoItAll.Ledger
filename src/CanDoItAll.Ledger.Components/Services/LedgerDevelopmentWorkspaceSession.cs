using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Sdk;
using CanDoItAll.Ledger.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Ledger.Components.Services;

public sealed class LedgerDevelopmentWorkspaceSession(
    LedgerClient economyClient,
    ILedgerGateway ledgerGateway,
    ILedgerStore ledgerStore,
    IBusinessObjectCastRegistry castRegistry,
    ManualTimeProvider timeProvider)
{
    private static readonly LedgerAccountContext SeedContext = new(new LedgerId("company-economy"), new ScenarioId("ssr-demo"));
    private static readonly Address MainAddress = AddressForKey("operations-primary");
    private static readonly Address ApprovalAddress = AddressForKey("operations-approval");
    private static readonly Address WarehouseAddress = AddressForKey("warehouse-north");
    private static readonly Address SupplierAddress = AddressForKey("supplier-acme");
    private static readonly TokenId OperatingCreditToken = new("USD-CREDIT");
    private const string TokenAuthorityNamespace = "ops.credit";
    private const string RuleAuthorityNamespace = "rules.company";
    private readonly SemaphoreSlim seedLock = new(1, 1);
    private BusinessObjectAccount? mainAccount;
    private int operationNonce;
    private bool seeded;

    public async Task<BusinessObjectWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = mainAccount ?? throw new InvalidOperationException("The development workspace was not initialized.");

        LedgerAccountBalance balance = await account.GetBalanceAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IBusinessObjectView> businessObjects = await account
            .GetBusinessObjectViewsAsync(castRegistry, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<BusinessObjectSubAccountSummary> subAccounts = await GetSubAccountSummariesAsync(account, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTokenCreationAuthority> tokenAuthorities = await GetTokenCreationAuthoritiesAsync(account, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerSpendableOutput> spendableOutputs = await account.GetSpendableOutputsAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTokenType> tokenTypes = await account.GetTokenTypesAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerPermissionSource> permissionSources = await account.GetPermissionSourcesAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> transactions = await economyClient
            .GetTransactionsAsync(SeedContext, account.Address, includeData: true, cancellationToken)
            .ConfigureAwait(false);

        IBusinessObjectView? selected = businessObjects.FirstOrDefault();
        var draft = new BusinessObjectTransactionDraft(
            default,
            "purchase-order",
            "1.0",
            account.Address,
            ApprovalAddress,
            SupplierAddress,
            Amount.FromInt64(0),
            Amount.FromInt64(42000),
            "{\"supplier\":\"Acme Metals\",\"amount\":42000,\"status\":\"draft\"}",
            "erp://purchase-orders/1043",
            TokenId: OperatingCreditToken,
            TokenAmount: Amount.FromInt64(1),
            SecondaryPaymentAmount: Amount.FromInt64(500));

        var editor = new BusinessObjectEditorModel(
            selected?.ObjectId ?? default,
            selected?.ObjectType ?? "purchase-order",
            selected?.SchemaVersion ?? "1.0",
            account.Address,
            selected?.StateJson ?? "{\"status\":\"draft\"}",
            timeProvider.UtcNow.AddDays(30));

        return new BusinessObjectWorkspaceModel(
            "Operations account (seeded SSR)",
            SeedContext,
            account.Address,
            balance,
            CreateTokenOptions(balance, tokenTypes, tokenAuthorities),
            tokenAuthorities,
            CreateAddressBookmarks(),
            businessObjects,
            subAccounts,
            transactions,
            draft,
            editor,
            timeProvider.UtcNow,
            AccountAlias: "development",
            ConnectionLabel: "Seeded in-memory SSR",
            SpendableOutputs: spendableOutputs,
            TokenTypes: tokenTypes,
            PermissionSources: permissionSources);
    }

    public async Task<LedgerTransactionExecutionResult> SubmitTransactionAsync(
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = mainAccount ?? throw new InvalidOperationException("The development workspace was not initialized.");
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        LedgerSubmitResult result = draft.SelectedMode switch
        {
            BusinessObjectTransactionMode.Create => await account.CreateBusinessObjectAsync(
                new CreateBusinessObjectRequest(
                    SafeText(draft.ObjectType, "purchase-order"),
                    SafeJson(draft.StateJson),
                    SafeText(draft.SchemaVersion, "1.0"),
                    AddressOrDefault(draft.OwnerAddress, account.Address),
                    MainAmount: PositiveOrDefault(draft.MainAmount, 100),
                    Token: draft.TokenId is not null && draft.TokenAmount is not null && draft.TokenAmount.Value.Value > 0m
                        ? new TokenTransfer(draft.TokenId.Value, draft.TokenAmount.Value)
                        : null,
                    ExternalRef: SafeNullable(draft.ExternalRef)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.TransferObject => await TransferSeededObjectAsync(account, draft, cancellationToken).ConfigureAwait(false),
            BusinessObjectTransactionMode.UpdateObject => await UpdateSeededObjectAsync(account, draft, cancellationToken).ConfigureAwait(false),
            BusinessObjectTransactionMode.CloseObject => await CloseSeededObjectAsync(account, draft, cancellationToken).ConfigureAwait(false),
            BusinessObjectTransactionMode.CloneObject => await CloneSeededObjectAsync(account, draft, cancellationToken).ConfigureAwait(false),
            BusinessObjectTransactionMode.MergeObjects => await MergeSeededObjectsAsync(account, draft, cancellationToken).ConfigureAwait(false),
            BusinessObjectTransactionMode.SplitBusinessObjectPayment => await SplitSeededObjectPaymentAsync(account, draft, cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.MainPayment => await account.SendPaymentAsync(
                new PaymentRequest(
                    AddressOrDefault(draft.PaymentReceiverAddress, SupplierAddress),
                    PositiveOrDefault(draft.PaymentAmount, 250)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.TokenTransfer => await account.SendTokenTransferAsync(
                new TokenTransferRequest(
                    draft.TokenId ?? OperatingCreditToken,
                    PositiveOrDefault(draft.TokenAmount ?? Amount.Zero, 25),
                    AddressOrDefault(draft.PaymentReceiverAddress, SupplierAddress),
                    PositiveOrDefault(draft.MainAmount, 1)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.UtxoMerge => await account.MergeUtxosAsync(
                new UtxoMergeRequest(
                    draft.SelectedUtxosOrEmpty,
                    ResolveOptionalAddress(draft.PaymentReceiverAddress)),
                cancellationToken).ConfigureAwait(false),

            BusinessObjectTransactionMode.UtxoSplit => await account.SplitUtxosAsync(
                new UtxoSplitRequest(
                    draft.SelectedUtxosOrEmpty,
                    draft.UtxoSplitOutputsOrEmpty),
                cancellationToken).ConfigureAwait(false),

            _ => throw new InvalidOperationException($"Unsupported transaction mode: {draft.SelectedMode}.")
        };

        return new LedgerTransactionExecutionResult(
            draft.SelectedMode,
            result.Accepted,
            result.TxId,
            result.LedgerSequence,
            result.Accepted
                ? $"{LabelFor(draft.SelectedMode)} accepted at ledger sequence {result.LedgerSequence}."
                : string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}")));
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (seeded)
        {
            return;
        }

        await seedLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (seeded)
            {
                return;
            }

            timeProvider.SetUtcNow(new DateTimeOffset(2026, 05, 07, 09, 00, 00, TimeSpan.Zero));
            mainAccount = economyClient.LoadAccount(SeedContext, CreateKey("operations-primary"));
            BusinessObjectSubAccount approval = economyClient.CreateSubAccount(
                mainAccount,
                "Operations approval",
                CreateKey("operations-approval"));
            BusinessObjectSubAccount warehouse = economyClient.CreateSubAccount(
                mainAccount,
                "Warehouse north",
                CreateKey("warehouse-north"));

            await SeedGenesisAsync(
                cancellationToken,
                new TransactionOutput(0, MainAddress, Amount.FromInt64(126500)),
                new TransactionOutput(1, MainAddress, Amount.FromInt64(250)),
                new TransactionOutput(2, MainAddress, Amount.Zero, new TokenTransfer(OperatingCreditToken, Amount.FromInt64(39000))),
                new TransactionOutput(3, ApprovalAddress, Amount.FromInt64(25000)),
                new TransactionOutput(4, WarehouseAddress, Amount.FromInt64(41000)),
                new TransactionOutput(5, MainAddress, Amount.Zero, new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(10_000))),
                new TransactionOutput(6, WarehouseAddress, Amount.Zero, new TokenTransfer(OperatingCreditToken, Amount.FromInt64(2_000))))
                .ConfigureAwait(false);

            timeProvider.Advance(TimeSpan.FromMinutes(5));
            await mainAccount.IssuePermissionAsync(
                new PermissionIssueRequest(
                    mainAccount.Address,
                    TokenAuthorityNamespace,
                    Amount.FromInt64(1),
                    Repeatable: true),
                cancellationToken)
                .ConfigureAwait(false);

            timeProvider.Advance(TimeSpan.FromMinutes(5));
            await mainAccount.IssuePermissionAsync(
                new PermissionIssueRequest(
                    mainAccount.Address,
                    RuleAuthorityNamespace,
                    Amount.FromInt64(5),
                    Repeatable: true,
                    Action: RuleTokenAuthority.CreateRuleTokenAction),
                cancellationToken)
                .ConfigureAwait(false);

            timeProvider.Advance(TimeSpan.FromMinutes(5));
            await mainAccount.CreateRuleTokenAsync(
                new RuleTokenCreateRequest(CreateCashpoolRule(SupplierAddress)),
                cancellationToken)
                .ConfigureAwait(false);

            timeProvider.Advance(TimeSpan.FromMinutes(15));
            await mainAccount.CreateBusinessObjectAsync(
                new CreateBusinessObjectRequest(
                    "settings",
                    "{\"currency\":\"USD\",\"approvalMode\":\"two-step\",\"simulationClock\":\"manual\"}",
                    MainAmount: Amount.FromInt64(5),
                    ItemType: TransactionDataItemType.AccountSettings,
                    ExternalRef: "settings://operations"),
                cancellationToken)
                .ConfigureAwait(false);

            timeProvider.Advance(TimeSpan.FromMinutes(15));
            LedgerSubmitResult purchaseOrderCreate = await mainAccount.CreateBusinessObjectAsync(
                new CreateBusinessObjectRequest(
                    "purchase-order",
                    "{\"supplier\":\"Acme Metals\",\"amount\":42000,\"status\":\"draft\"}",
                    MainAmount: Amount.FromInt64(42000),
                    ExternalRef: "erp://purchase-orders/1042"),
                cancellationToken)
                .ConfigureAwait(false);
            BusinessObjectId purchaseOrderId = RequireResultBusinessObjectId(purchaseOrderCreate, "Seed purchase order");

            timeProvider.Advance(TimeSpan.FromMinutes(15));
            await mainAccount.UpdateBusinessObjectAsync(
                new UpdateBusinessObjectRequest(
                    purchaseOrderId,
                    "{\"supplier\":\"Acme Metals\",\"amount\":42000,\"status\":\"approved\"}",
                    "erp://purchase-orders/1042"),
                cancellationToken)
                .ConfigureAwait(false);

            timeProvider.Advance(TimeSpan.FromMinutes(15));
            await warehouse.CreateBusinessObjectAsync(
                new CreateBusinessObjectRequest(
                    "inventory-lot",
                    "{\"sku\":\"steel-sheet\",\"quantity\":900,\"uom\":\"kg\",\"reserved\":120}",
                    MainAmount: Amount.FromInt64(900),
                    ExternalRef: "wms://lots/steel-77"),
                cancellationToken)
                .ConfigureAwait(false);

            timeProvider.Advance(TimeSpan.FromMinutes(15));
            await mainAccount.SendPaymentAsync(
                new PaymentRequest(SupplierAddress, Amount.FromInt64(4200)),
                cancellationToken)
                .ConfigureAwait(false);

            timeProvider.Advance(TimeSpan.FromMinutes(15));
            await mainAccount.SendTokenTransferAsync(
                new TokenTransferRequest(OperatingCreditToken, Amount.FromInt64(1500), SupplierAddress, Amount.FromInt64(10)),
                cancellationToken)
                .ConfigureAwait(false);

            timeProvider.Advance(TimeSpan.FromMinutes(15));
            await approval.SendPaymentAsync(
                new PaymentRequest(SupplierAddress, Amount.FromInt64(1250)),
                cancellationToken)
                .ConfigureAwait(false);

            seeded = true;
        }
        finally
        {
            seedLock.Release();
        }
    }

    private async Task<IReadOnlyList<BusinessObjectSubAccountSummary>> GetSubAccountSummariesAsync(
        BusinessObjectAccount account,
        CancellationToken cancellationToken)
    {
        var summaries = new List<BusinessObjectSubAccountSummary>();

        foreach (BusinessObjectSubAccount subAccount in account.SubAccounts.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            LedgerAccountBalance balance = await subAccount.GetBalanceAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<LedgerBusinessObjectHolding> objects = await subAccount.GetBusinessObjectsAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<LedgerTransactionSummary> transactions = await economyClient
                .GetTransactionsAsync(SeedContext, subAccount.Address, cancellationToken)
                .ConfigureAwait(false);

            summaries.Add(new BusinessObjectSubAccountSummary(
                subAccount.Name,
                subAccount.Address,
                balance.MainBalance,
                balance.UtxoCount,
                objects.Count + transactions.Count,
                balance.AsOfUtc));
        }

        return summaries;
    }

    private async Task<IReadOnlyList<LedgerTokenCreationAuthority>> GetTokenCreationAuthoritiesAsync(
        BusinessObjectAccount account,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerPermissionSource> permissions = await account.GetPermissionSourcesAsync(cancellationToken)
            .ConfigureAwait(false);

        return permissions
            .Where(source => string.Equals(source.Grant.Action, PermissionTokenAuthority.MintNewTokenTypeAction, StringComparison.Ordinal)
                && source.Output.BusinessObject is not null
                && source.Amount.Value > 0m)
            .Select(source => new LedgerTokenCreationAuthority(
                source.Grant.Namespace,
                BusinessObjectId.FromOutPoint(source.Output.OutPoint),
                source.Output.Address,
                source.OutPoint,
                source.Amount,
                source.Grant.Repeatable))
            .OrderBy(authority => authority.Namespace, StringComparer.Ordinal)
            .ToArray();
    }

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
                        CanCreateMore: tokenAuthorities.Any(authority => TokenCreationAuthority.IsTokenInNamespace(token.Key, authority.Namespace)),
                        SourceAmount: tokenType?.SourceAmount ?? Amount.Zero,
                        UniqueAmount: tokenType?.UniqueAmount ?? Amount.Zero,
                        SourceUtxoCount: tokenType?.SourceUtxoCount ?? 0,
                        UniqueUtxoCount: tokenType?.UniqueUtxoCount ?? 0,
                        TotalUtxoCount: tokenType?.TotalUtxoCount ?? 0);
                })
        ];
    }

    private static IReadOnlyList<LedgerAddressBookmark> CreateAddressBookmarks()
        =>
        [
            new("Operations approval", ApprovalAddress),
            new("Warehouse north", WarehouseAddress),
            new("Supplier Acme", SupplierAddress)
        ];

    private Task<LedgerSubmitResult> SeedGenesisAsync(CancellationToken cancellationToken, params TransactionOutput[] outputs)
        => ledgerGateway.SubmitTransactionAsync(
            new LedgerTransaction(
                SeedContext.LedgerId,
                SeedContext.ScenarioId,
                Version: 1,
                TransactionKind.Genesis,
                SigningMode.Independent,
                timeProvider.UtcNow,
                Array.Empty<TransactionInput>(),
                outputs,
                Data: null),
            cancellationToken);

    private static LedgerTransactionExecutionResult ToExecutionResult(
        BusinessObjectTransactionMode mode,
        LedgerSubmitResult result,
        string label)
        => new(
            mode,
            result.Accepted,
            result.TxId,
            result.LedgerSequence,
            result.Accepted
                ? $"{label} accepted at ledger sequence {result.LedgerSequence}."
                : string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}")));

    private async Task<LedgerSubmitResult> TransferSeededObjectAsync(
        BusinessObjectAccount account,
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken)
    {
        BusinessObjectId objectId = await CreatePrerequisiteObjectAsync(
            account,
            draft,
            Amount.FromInt64(100),
            cancellationToken).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        return await account.TransferBusinessObjectAsync(
            new TransferBusinessObjectRequest(
                objectId,
                AddressOrDefault(draft.ReceiverAddress, ApprovalAddress),
                PositiveOrDefault(draft.MainAmount, 25),
                SafeJson(draft.StateJson),
                SafeNullable(draft.ExternalRef)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<LedgerSubmitResult> UpdateSeededObjectAsync(
        BusinessObjectAccount account,
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken)
    {
        BusinessObjectId objectId = await CreatePrerequisiteObjectAsync(
            account,
            draft,
            Amount.FromInt64(75),
            cancellationToken).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        return await account.UpdateBusinessObjectAsync(
            new UpdateBusinessObjectRequest(
                objectId,
                SafeJson(draft.StateJson),
                SafeNullable(draft.ExternalRef)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<LedgerSubmitResult> CloseSeededObjectAsync(
        BusinessObjectAccount account,
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken)
    {
        BusinessObjectId objectId = await CreatePrerequisiteObjectAsync(
            account,
            draft,
            Amount.FromInt64(60),
            cancellationToken).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        return await account.CloseBusinessObjectAsync(
            new CloseBusinessObjectRequest(
                objectId,
                SafeJson(draft.StateJson),
                SafeNullable(draft.ExternalRef)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<LedgerSubmitResult> CloneSeededObjectAsync(
        BusinessObjectAccount account,
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken)
    {
        BusinessObjectId sourceId = await CreatePrerequisiteObjectAsync(
            account,
            draft,
            Amount.FromInt64(50),
            cancellationToken).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        return await account.CloneBusinessObjectAsync(
            new CloneBusinessObjectRequest(
                sourceId,
                SafeText(draft.ObjectType, "purchase-order"),
                SafeJson(draft.StateJson),
                SafeText(draft.SchemaVersion, "1.0"),
                Token: draft.TokenId is not null && draft.TokenAmount is not null && draft.TokenAmount.Value.Value > 0m
                    ? new TokenTransfer(draft.TokenId.Value, draft.TokenAmount.Value)
                    : null),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<LedgerSubmitResult> MergeSeededObjectsAsync(
        BusinessObjectAccount account,
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken)
    {
        BusinessObjectId firstId = await CreatePrerequisiteObjectAsync(
            account,
            draft,
            Amount.FromInt64(40),
            cancellationToken).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        BusinessObjectId secondId = await CreatePrerequisiteObjectAsync(
            account,
            draft,
            Amount.FromInt64(45),
            cancellationToken).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        return await account.MergeBusinessObjectsAsync(
            new MergeBusinessObjectsRequest(
                firstId,
                secondId,
                SafeText(draft.ObjectType, "purchase-order"),
                SafeJson(draft.StateJson),
                SafeText(draft.SchemaVersion, "1.0"),
                SafeNullable(draft.ExternalRef)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<LedgerSubmitResult> SplitSeededObjectPaymentAsync(
        BusinessObjectAccount account,
        BusinessObjectTransactionDraft draft,
        CancellationToken cancellationToken)
    {
        Amount firstPayment = PositiveOrDefault(draft.PaymentAmount, 250);
        Amount secondPayment = PositiveOrDefault(draft.SecondaryPaymentAmount ?? Amount.Zero, 125);
        Amount fundingAmount = new(firstPayment.Value + secondPayment.Value + 50m);

        BusinessObjectId objectId = await CreatePrerequisiteObjectAsync(
            account,
            draft,
            fundingAmount,
            cancellationToken).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        return await account.SplitBusinessObjectPaymentAsync(
            new SplitBusinessObjectPaymentRequest(
                objectId,
                new[]
                {
                    new BusinessObjectPaymentOutput(AddressOrDefault(draft.PaymentReceiverAddress, SupplierAddress), firstPayment),
                    new BusinessObjectPaymentOutput(AddressOrDefault(draft.ReceiverAddress, ApprovalAddress), secondPayment)
                },
                SafeJson(draft.StateJson),
                SafeNullable(draft.ExternalRef)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<BusinessObjectId> CreatePrerequisiteObjectAsync(
        BusinessObjectAccount account,
        BusinessObjectTransactionDraft draft,
        Amount mainAmount,
        CancellationToken cancellationToken)
    {
        LedgerSubmitResult prerequisite = await account.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                SafeText(draft.ObjectType, "purchase-order"),
                SafeJson(draft.StateJson),
                SafeText(draft.SchemaVersion, "1.0"),
                MainAmount: mainAmount,
                ExternalRef: SafeNullable(draft.ExternalRef)),
            cancellationToken).ConfigureAwait(false);

        if (!prerequisite.Accepted)
        {
            throw new InvalidOperationException(string.Join("; ", prerequisite.Errors.Select(error => $"{error.Code}: {error.Message}")));
        }

        return RequireResultBusinessObjectId(prerequisite, "Prerequisite business object");
    }

    private static LedgerAccountKeyEnvelope CreateKey(string keyId)
    {
        byte[] publicKey = Encoding.UTF8.GetBytes($"{keyId}-public");
        byte[] privateKey = Encoding.UTF8.GetBytes($"{keyId}-private");

        return new LedgerAccountKeyEnvelope(
            SignatureAlgorithmId.EcdsaP256Sha256,
            keyId,
            publicKey,
            privateKey);
    }

    private static Address AddressForKey(string keyId)
        => Address.FromPublicKey(Encoding.UTF8.GetBytes($"{keyId}-public"));

    private static BusinessObjectId RequireResultBusinessObjectId(LedgerSubmitResult result, string label)
        => result.Accepted && result.TxId is { } txId
            ? BusinessObjectId.FromOutPoint(txId, 0)
            : throw new InvalidOperationException(
                $"{label} did not return an accepted transaction: {string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}"))}");

    private static Address AddressOrDefault(Address candidate, Address fallback)
        => string.IsNullOrWhiteSpace(candidate.Value) ? fallback : candidate;

    private static Amount PositiveOrDefault(Amount amount, long fallback)
        => amount.Value > 0m ? amount : Amount.FromInt64(fallback);

    private static string SafeJson(string value)
        => string.IsNullOrWhiteSpace(value) ? "{}" : value;

    private static string SafeText(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? SafeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string MergeJson(string stateJson, Action<JsonObject> update)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(SafeJson(stateJson)) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject { ["previousState"] = stateJson };
        }

        update(root);
        return root.ToJsonString();
    }

    private static string NormalizeTokenSegment(string value, string fallback)
    {
        string candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var builder = new StringBuilder(candidate.Length);

        foreach (char character in candidate)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '_' or '-'
                ? char.ToLowerInvariant(character)
                : '-');
        }

        string normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string LabelFor(BusinessObjectTransactionMode mode)
        => mode switch
        {
            BusinessObjectTransactionMode.TransferObject => "BO transfer",
            BusinessObjectTransactionMode.UpdateObject => "BO update",
            BusinessObjectTransactionMode.CloseObject => "BO close",
            BusinessObjectTransactionMode.CloneObject => "BO clone",
            BusinessObjectTransactionMode.MergeObjects => "BO merge",
            BusinessObjectTransactionMode.SplitBusinessObjectPayment => "Split BO payment",
            BusinessObjectTransactionMode.MainPayment => "Main payment",
            BusinessObjectTransactionMode.TokenTransfer => "Token transfer",
            BusinessObjectTransactionMode.UtxoMerge => "UTXO merge",
            BusinessObjectTransactionMode.UtxoSplit => "UTXO split",
            BusinessObjectTransactionMode.CreateRuleToken => "Rule BO create",
            _ => "BO create"
        };

    private static Address? ResolveOptionalAddress(Address address)
        => Address.TryParse(address.Value, out Address parsed) ? parsed : null;

    private static string BuildSaleStateJson(
        BusinessObjectPriceRequest request,
        Address sellerAddress,
        Amount priceAmount,
        DateTimeOffset listedAtUtc)
    {
        if (request.ClearSale)
        {
            return BusinessObjectCommercePayloadBuilder.ClearSaleTermsStateJson(request.StateJson, "Untitled BusinessObject");
        }

        BusinessObjectSaleTerms terms = BusinessObjectCommercePayloadBuilder.CreateSaleTerms(
            request.SaleMode,
            priceAmount,
            sellerAddress,
            listedAtUtc,
            request.SaleMode == BusinessObjectSaleMode.Copy ? request.MaxClones : null,
            request.TokenId,
            request.SharePath);
        return BusinessObjectCommercePayloadBuilder.WithSaleTermsStateJson(request.StateJson, "Untitled BusinessObject", terms);
    }

    public async Task<LedgerTransactionDetailsModel?> GetTransactionDetailsAsync(
        TxId txId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        LedgerTransaction? transaction = await ledgerStore
            .GetTransactionAsync(SeedContext.LedgerId, SeedContext.ScenarioId, txId, cancellationToken)
            .ConfigureAwait(false);
        if (transaction is null)
        {
            return null;
        }

        var inputs = new List<LedgerTransactionInputDetails>(transaction.Inputs.Count);
        foreach (TransactionInput input in transaction.Inputs.OrderBy(input => input.InputIndex))
        {
            LedgerTransaction? previousTransaction = await ledgerStore
                .GetTransactionAsync(SeedContext.LedgerId, SeedContext.ScenarioId, input.PreviousOutPoint.TxId, cancellationToken)
                .ConfigureAwait(false);
            TransactionOutput? previousOutput = previousTransaction?.Outputs
                .FirstOrDefault(output => output.OutputIndex == input.PreviousOutPoint.OutputIndex);

            inputs.Add(new LedgerTransactionInputDetails(
                input.InputIndex,
                input.PreviousOutPoint,
                input.Address,
                previousOutput?.MainAmount,
                previousOutput?.Token,
                ToTransactionBusinessObjectDetails(previousOutput?.BusinessObject),
                input.SignatureBundle,
                input.SignaturePayloadHash,
                OutPointKey: null));
        }

        IReadOnlyList<LedgerTransactionOutputDetails> outputs = transaction.Outputs
            .OrderBy(output => output.OutputIndex)
            .Select(output => new LedgerTransactionOutputDetails(
                output.OutputIndex,
                output.Address,
                output.MainAmount,
                output.Token,
                ToTransactionBusinessObjectDetails(output.BusinessObject),
                output.MetadataHash))
            .ToArray();

        return new LedgerTransactionDetailsModel(
            transaction.TxId ?? txId,
            transaction.TxHash,
            transaction.Kind,
            transaction.Status,
            transaction.SigningMode,
            transaction.Timestamp,
            transaction.Data,
            inputs,
            outputs);
    }

    private static LedgerTransactionBusinessObjectDetails? ToTransactionBusinessObjectDetails(
        BusinessObjectRef? businessObject)
        => businessObject is null
            ? null
            : new LedgerTransactionBusinessObjectDetails(
                businessObject.Version,
                businessObject.Operation,
                businessObject.DataHash);

    public async Task<LedgerTransactionExecutionResult> SendBusinessObjectAsync(
        BusinessObjectSendRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = mainAccount ?? throw new InvalidOperationException("The development workspace was not initialized.");
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        LedgerSubmitResult result = request.Mode switch
        {
            BusinessObjectSendMode.Clone => await account.CloneBusinessObjectAsync(
                new CloneBusinessObjectRequest(
                    request.ObjectId,
                    SafeText(request.ObjectType, "business-object"),
                    SafeJson(request.StateJson),
                    SafeText(request.SchemaVersion, "1.0"),
                    request.ReceiverAddress),
                cancellationToken).ConfigureAwait(false),

            _ => await account.TransferBusinessObjectAsync(
                new TransferBusinessObjectRequest(
                    request.ObjectId,
                    request.ReceiverAddress,
                    null,
                    SafeJson(request.StateJson),
                    SafeNullable(request.ExternalRef)),
                cancellationToken).ConfigureAwait(false)
        };

        BusinessObjectTransactionMode mode = request.Mode == BusinessObjectSendMode.Clone
            ? BusinessObjectTransactionMode.CloneObject
            : BusinessObjectTransactionMode.TransferObject;
        return ToExecutionResult(mode, result, request.Mode == BusinessObjectSendMode.Clone ? "BO clone send" : "BO send");
    }

    public async Task<LedgerTransactionExecutionResult> SetBusinessObjectPriceAsync(
        BusinessObjectPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = mainAccount ?? throw new InvalidOperationException("The development workspace was not initialized.");
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Amount priceAmount = PositiveOrDefault(request.PriceAmount, 1);
        string nextState = BuildSaleStateJson(request, account.Address, priceAmount, timeProvider.UtcNow);

        LedgerSubmitResult result = await account.SetBusinessObjectPriceAsync(
            new SetBusinessObjectPriceRequest(
                request.ObjectId,
                priceAmount,
                request.TokenId,
                nextState,
                SafeNullable(request.ExternalRef)),
            cancellationToken).ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.UpdateObject, result, "BO sale update");
    }

    public async Task<LedgerTransactionExecutionResult> UpdateProfileAsync(
        ProfileEditRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = mainAccount ?? throw new InvalidOperationException("The development workspace was not initialized.");
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        string nextState = WasmAccountWorkspaceAdapter.BuildProfileStateJson(request, "Operations account");
        LedgerSubmitResult result = request.CreateNew
            ? await account.CreateBusinessObjectAsync(
                new CreateBusinessObjectRequest(
                    "profile",
                    nextState,
                    MainAmount: Amount.Zero,
                    ExternalRef: SafeNullable(request.ExternalRef)),
                cancellationToken).ConfigureAwait(false)
            : await account.UpdateBusinessObjectAsync(
                new UpdateBusinessObjectRequest(
                    request.ObjectId ?? throw new InvalidOperationException("Existing profile BusinessObject id is required for update."),
                    nextState,
                    SafeNullable(request.ExternalRef)),
                cancellationToken).ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.UpdateObject, result, "Profile update");
    }

    public async Task<BusinessObjectTicketVerificationResult> CreateTicketVerificationAsync(
        BusinessObjectTicketVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = mainAccount ?? throw new InvalidOperationException("The development workspace was not initialized.");
        IReadOnlyList<LedgerTransactionSummary> history = await economyClient.QueryTransactionsAsync(
            new LedgerTransactionQuery(
                SeedContext,
                BusinessObjectId: request.ObjectId,
                IncludeData: true,
                Skip: 0,
                Take: 100),
            cancellationToken).ConfigureAwait(false);
        BusinessObjectTicketHistoryValidation validation = BusinessObjectTicketVerification.ValidateHistory(
            request.ObjectType,
            request.ObjectId,
            history);
        BusinessObjectTicketVerificationMessage message = BusinessObjectTicketVerification.CreateVerificationMessage(
            request.ObjectId,
            request.CurrentOutPoint,
            request.OwnerAddress,
            timeProvider.UtcNow);
        if (!validation.IsValid)
        {
            return new BusinessObjectTicketVerificationResult(
                Success: false,
                validation.Summary,
                message.Message,
                message.MessageHash,
                SignatureJson: null,
                message.GeneratedAtUtc,
                message.ExpiresAtUtc,
                validation);
        }

        LedgerMessageSignature signature = await AccountMessageSecurity
            .SignMessageAsync(account.KeySource, message.Message, cancellationToken)
            .ConfigureAwait(false);
        string signatureJson = JsonSerializer.Serialize(signature, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        return new BusinessObjectTicketVerificationResult(
            Success: true,
            "Ticket ownership code was signed for the current unused ticket.",
            message.Message,
            message.MessageHash,
            signatureJson,
            message.GeneratedAtUtc,
            message.ExpiresAtUtc,
            validation);
    }

    public async Task<LedgerTransactionExecutionResult> UseTicketAsync(
        BusinessObjectTicketUseRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = mainAccount ?? throw new InvalidOperationException("The development workspace was not initialized.");
        IReadOnlyList<LedgerTransactionSummary> history = await economyClient.QueryTransactionsAsync(
            new LedgerTransactionQuery(
                SeedContext,
                BusinessObjectId: request.ObjectId,
                IncludeData: true,
                Skip: 0,
                Take: 100),
            cancellationToken).ConfigureAwait(false);
        BusinessObjectTicketHistoryValidation validation = BusinessObjectTicketVerification.ValidateHistory(
            "ticket",
            request.ObjectId,
            history);
        if (!validation.IsValid)
        {
            return new LedgerTransactionExecutionResult(
                BusinessObjectTransactionMode.UpdateObject,
                Accepted: false,
                TxId: null,
                LedgerSequence: null,
                validation.Summary);
        }

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        string nextState = BusinessObjectTicketVerification.BuildUsedStateJson(
            request.StateJson,
            request.ObjectId,
            timeProvider.UtcNow,
            request.VerificationHash);
        LedgerSubmitResult result = await account.UpdateBusinessObjectAsync(
            new UpdateBusinessObjectRequest(
                request.ObjectId,
                nextState,
                SafeNullable(request.ExternalRef)),
            cancellationToken).ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.UpdateObject, result, "Ticket use");
    }

    public async Task<BusinessObjectEventLookupResult> LookupEventBusinessObjectAsync(
        BusinessObjectEventLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        return await BusinessObjectEventLookup
            .LookupAsync(economyClient, castRegistry, SeedContext, request.Query, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LedgerTransactionExecutionResult> CreateTokenAsync(
        LedgerTokenCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = mainAccount ?? throw new InvalidOperationException("The development workspace was not initialized.");
        IReadOnlyList<LedgerTokenCreationAuthority> authorities = await GetTokenCreationAuthoritiesAsync(account, cancellationToken)
            .ConfigureAwait(false);

        string tokenNamespace = TokenCreationAuthority.NormalizeNamespace(request.TokenNamespace);
        if (!authorities.Any(authority => string.Equals(authority.Namespace, tokenNamespace, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"The active account has no token creation authority for namespace {tokenNamespace}.");
        }

        int nonce = Interlocked.Increment(ref operationNonce);
        string tokenSegment = NormalizeTokenSegment(request.TokenName, $"asset-{nonce}");
        var tokenId = new TokenId($"{tokenNamespace}.{tokenSegment}");
        Amount sourceAmount = PositiveOrDefault(request.Amount, 1);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        LedgerSubmitResult mintResult = await account.MintTokenAsync(
            new TokenMintRequest(
                tokenId,
                sourceAmount,
                PermissionSourceOutPoint: request.PermissionSourceOutPoint),
            cancellationToken).ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.Create, mintResult, $"Token {tokenId.Value}");
    }

    public async Task<LedgerTransactionExecutionResult> CreateRuleTokenAsync(
        LedgerRuleTokenCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        BusinessObjectAccount account = mainAccount ?? throw new InvalidOperationException("The development workspace was not initialized.");
        AccountRuleDefinition rule = RuleTokenAuthority.Normalize(request.Rule);
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        LedgerSubmitResult result = await account.CreateRuleTokenAsync(
                new RuleTokenCreateRequest(rule, PermissionSourceOutPoint: request.PermissionSourceOutPoint),
                cancellationToken)
            .ConfigureAwait(false);

        return ToExecutionResult(BusinessObjectTransactionMode.CreateRuleToken, result, $"Rule {rule.RuleId}");
    }

    private static AccountRuleDefinition CreateCashpoolRule(Address receiverAddress)
        => new(
            "cashpool-sweep",
            RuleAuthorityNamespace,
            Enabled: true,
            new AccountRuleTrigger(AccountRuleTriggerKind.AccountSnapshot),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["limit"] = "1000"
            },
            ["account_main > limit"],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.Payment,
                    ReceiverAddress: receiverAddress.Value,
                    AmountExpression: "account_main - limit")
            ],
            new AccountRuleProcessingOptions(
                MarkTriggerProcessed: false,
                ProcessedProperty: "processed",
                KeepProcessedBusinessObject: true,
                MaxActionsPerRefresh: 5));
}

public static class LedgerDevelopmentWorkspaceServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerDevelopmentWorkspaceSession(this IServiceCollection services)
    {
        services.TryAddScoped<ManualTimeProvider>(_ => new ManualTimeProvider(DateTimeOffset.UtcNow));
        services.TryAddScoped<ITimeProvider>(provider => provider.GetRequiredService<ManualTimeProvider>());
        services.TryAddScoped<ITransactionCanonicalizer, DefaultTransactionCanonicalizer>();
        services.TryAddScoped<ITransactionHasher, TransactionHasher>();
        services.TryAddScoped<ISignaturePayloadBuilder, SignaturePayloadBuilder>();
        services.TryAddScoped<ILedgerAdminSigner, DevelopmentLedgerAdminSigner>();
        services.TryAddScoped<ISignatureProviderRegistry>(_ => new SignatureProviderRegistry([new DevelopmentEcdsaSignatureProvider()]));
        services.TryAddScoped<IAccountInputSigner, AccountInputSigner>();
        services.TryAddScoped<ILedgerStore, InMemoryLedgerStore>();
        services.TryAddScoped<ITokenCreationPolicy>(_ => DisabledTokenCreationPolicy.Instance);
        services.TryAddScoped<IMainCurrencyIssuancePolicy>(_ => DisabledMainCurrencyIssuancePolicy.Instance);
        services.TryAddScoped<LedgerExecutor>();
        services.AddBusinessObjectLedgerLayer();
        services.AddLedgerSdk();
        services.TryAddScoped<LedgerDevelopmentWorkspaceSession>();
        return services;
    }
}

internal sealed class DevelopmentLedgerAdminSigner : ILedgerAdminSigner
{
    public SignatureBundle Sign(HashHex payloadHash)
        => new([
            new SignatureEnvelope(
                SignatureAlgorithmId.EcdsaP256Sha256,
                "development-admin",
                Hex.Sha256Hex(Encoding.UTF8.GetBytes("development-admin-public")),
                payloadHash,
                Encoding.UTF8.GetBytes($"development-admin|{payloadHash.Value}"))
        ]);
}

internal sealed class DevelopmentEcdsaSignatureProvider : ISignatureProvider
{
    public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

    public bool IsSupported => true;

    public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
        => Encoding.UTF8.GetBytes($"{Encoding.UTF8.GetString(payload)}|{Encoding.UTF8.GetString(privateKeyMaterial)}");

    public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
        => payload.Length > 0 && signature.Length > 0 && publicKeyMaterial.Length > 0;
}
