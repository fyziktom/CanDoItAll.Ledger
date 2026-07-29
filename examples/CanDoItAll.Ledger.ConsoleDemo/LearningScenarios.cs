using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Sdk;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Ledger.ConsoleDemo;

internal sealed class LearningScenarios(
    DemoOptions options,
    DemoKeyStore keyStore,
    DemoAccountDriver alice,
    DemoAccountDriver bob)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = false
    };

    private static readonly IReadOnlyList<LearningLesson> Lessons =
    [
        new("01", "SDK composition and account loading", "Configure the node driver, resolve LedgerClient, and load account keys."),
        new("02", "Account reads", "Read balances, spendable outputs, token types, BO holdings, and recent transactions."),
        new("03", "Main payment", "Mint setup funds, send a main-asset payment, and inspect snapshots."),
        new("04", "Token mint and transfer", "Grant mint permission, mint a reusable token source, issue permission, and transfer tokens."),
        new("05", "BO DataItems and views", "Create and update a post BO with typed DataItems, then cast it to a view."),
        new("06", "BO transfer and transaction details", "Transfer a BO and read typed node transaction details."),
        new("07", "Plain UTXO split and merge plus advanced BO transactions", "Split and merge plain UTXOs, then set price, split payment, clone, merge, and close BOs."),
        new("08", "Subaccounts, exports, and messages", "Create/import/export keys and subaccounts, then sign/encrypt messages."),
        new("09", "Account auto-refresh", "Start an account refresh loop, observe a new transaction event, and stop cleanly."),
        new("10", "Expected failures", "Show rejected or invalid SDK operations without crashing the demo.")
    ];

    private readonly string runId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
    private BusinessObjectId? dataItemObjectId;

    public static void PrintLessonCatalog()
    {
        ConsoleUi.Title("CanDoItAll Ledger SDK learning lessons");
        ConsoleUi.Table(
            ["#", "Lesson", "What it shows"],
            Lessons.Select(lesson => new[] { lesson.Number, lesson.Title, lesson.Description }));
    }

    public async Task Learn01SdkCompositionAndAccountLoadingAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("1. SDK composition and account loading");

        using ServiceProvider provider = new ServiceCollection()
            .AddLedgerSdkWithNodeDriver(
                options.NodeBaseUri,
                node =>
                {
                    node.ApiBasePath = "api/v1";
                    node.ApiKey = Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_API_KEY");
                })
            .BuildServiceProvider(validateScopes: true);

        using IServiceScope scope = provider.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<LedgerClient>();
        LedgerNodeLedgerClient driver = scope.ServiceProvider.GetRequiredService<LedgerNodeLedgerClient>();

        ConsoleUi.Success($"Resolved LedgerClient with node driver at {driver.NodeBaseUri} using /api/v1.");
        ConsoleUi.Table(
            ["Account", "Key file", "Address"],
            [
                [alice.Name, alice.KeyPath, alice.Address.Value],
                [bob.Name, bob.KeyPath, bob.Address.Value]
            ]);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task Learn02AccountReadsAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("2. Account reads");
        await PrintSnapshotsAsync("Balances and holdings", cancellationToken).ConfigureAwait(false);

        IReadOnlyList<LedgerSpendableOutput> aliceOutputs = await alice.GetSpendableOutputsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LedgerTransactionSummary> aliceTransactions = await alice
            .GetTransactions(maxCount: 10, includeData: true, cancellationToken)
            .ConfigureAwait(false);

        ConsoleUi.Info($"Alice has {aliceOutputs.Count} spendable output(s) and {aliceTransactions.Count} recent transaction(s) with inline data requested.");
        await PrintTokenTypesAsync("Alice token types", alice, cancellationToken).ConfigureAwait(false);
    }

    public async Task Learn03MainPaymentAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("3. Main payment");
        await EnsureMainBalanceAsync(alice, Amount.FromInt64(100), cancellationToken).ConfigureAwait(false);

        LedgerSubmitResult payment = await alice.SendPaymentAsync(bob, Amount.FromInt64(25), cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt("Alice pays Bob 25 main units", payment);
        await PrintSnapshotsAsync("After main payment", cancellationToken).ConfigureAwait(false);
    }

    public async Task Learn04TokenMintAndTransferAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("4. Token mint and transfer");
        TokenId tokenId = new($"console.demo.token.{runId}");
        string tokenNamespace = GetTokenNamespace(tokenId);

        LedgerPermissionSource permission = await EnsureMintPermissionAsync(tokenId, cancellationToken).ConfigureAwait(false);
        ConsoleUi.Info($"Alice mints {tokenId.Value} with permission source {permission.OutPoint}.");

        ConsoleUi.PrintReceipt(
            $"Mint reusable token source {tokenId.Value} x5 to Alice",
            await alice.MintTokenAsync(tokenId, Amount.FromInt64(5), cancellationToken, permission.OutPoint).ConfigureAwait(false));

        ConsoleUi.PrintReceipt(
            $"Alice transfers {tokenId.Value} x2 to Bob",
            await alice.SendTokenTransferAsync(bob, tokenId, Amount.FromInt64(2), cancellationToken).ConfigureAwait(false));

        IReadOnlyList<LedgerPermissionSource> alicePermissions = await alice.GetPermissionSourcesAsync(cancellationToken).ConfigureAwait(false);
        LedgerPermissionSource? delegationSource = alicePermissions.FirstOrDefault(source => source.Grant.Namespace == tokenNamespace);
        if (delegationSource is not null)
        {
            ConsoleUi.PrintReceipt(
                $"Alice issues repeatable mint permission for {tokenNamespace} to Bob",
                await alice.IssuePermissionAsync(
                    bob,
                    tokenNamespace,
                    Amount.FromInt64(1),
                    cancellationToken,
                    sourceOutPoint: delegationSource.OutPoint).ConfigureAwait(false));
        }

        await PrintTokenTypesAsync("Bob token types after transfer", bob, cancellationToken).ConfigureAwait(false);
    }

    public async Task Learn05BusinessObjectDataItemsAndViewsAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("5. BusinessObject DataItems and cast views");
        await EnsureMainBalanceAsync(alice, Amount.FromInt64(50), cancellationToken).ConfigureAwait(false);

        string initialJson = SerializePayload(CreatePostPayload(
            "Console SDK post",
            "Created with typed DataItems from the console learning kit.",
            [
                new BusinessObjectDataItem(
                    "readme",
                    BusinessObjectDataItemType.Text,
                    BusinessObjectDataItemStorage.Inline,
                    $"inline-readme-{runId}",
                    ContentType: "text/plain",
                    FileName: "readme.txt",
                    Size: 128,
                    IsMain: true,
                    Description: "Inline text item for learning.")
            ]));

        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            "post",
            initialJson,
            Amount.FromInt64(3),
            cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt("Create post BO with one DataItem", create);
        BusinessObjectId objectId = RequireBusinessObjectId(create, "DataItem BO create");

        string updatedJson = SerializePayload(CreatePostPayload(
            "Console SDK post",
            "Updated with a second URL-backed DataItem.",
            [
                new BusinessObjectDataItem(
                    "readme",
                    BusinessObjectDataItemType.Text,
                    BusinessObjectDataItemStorage.Inline,
                    $"inline-readme-{runId}",
                    ContentType: "text/plain",
                    FileName: "readme.txt",
                    Size: 128,
                    IsMain: true,
                    Description: "Inline text item for learning."),
                new BusinessObjectDataItem(
                    "roadmap",
                    BusinessObjectDataItemType.Json,
                    BusinessObjectDataItemStorage.Url,
                    "https://example.local/candoitall/roadmap.json",
                    new Uri("https://example.local/candoitall/roadmap.json"),
                    "application/json",
                    "roadmap.json",
                    Description: "URL DataItem reference for learning.")
            ]));

        LedgerSubmitResult update = await alice.UpdateBusinessObjectAsync(objectId, updatedJson, cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt($"Update BO {objectId.Value} with a second DataItem", update);
        objectId = RequireBusinessObjectId(update, "DataItem BO update");
        dataItemObjectId = objectId;

        IReadOnlyList<IBusinessObjectView> views = await alice
            .GetBusinessObjectViewsAsync(CreateCastRegistry(), cancellationToken)
            .ConfigureAwait(false);
        IMediaBusinessObjectView? view = views.OfType<IMediaBusinessObjectView>()
            .FirstOrDefault(x => x.ObjectId == objectId);
        if (view is not null)
        {
            ConsoleUi.Success($"Cast BO view '{view.Name}' with {view.DataItems.Count} DataItem(s).");
        }
    }

    public async Task Learn06BusinessObjectTransferAndTransactionDetailsAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("6. BusinessObject transfer and transaction details");
        BusinessObjectId objectId = dataItemObjectId
            ?? throw new InvalidOperationException("Lesson 05 must create the DataItem BO before lesson 06 runs.");

        LedgerSubmitResult transfer = await alice.TransferBusinessObjectAsync(objectId, bob, cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt($"Alice transfers BO {objectId.Value} to Bob", transfer);
        BusinessObjectId transferredObjectId = RequireBusinessObjectId(transfer, "BusinessObject transfer");

        if (transfer.Accepted && transfer.TxId is { } txId)
        {
            LedgerNodeTransactionDetails? details = await alice.GetTransactionDetailsAsync(txId, cancellationToken).ConfigureAwait(false);
            if (details is not null)
            {
                ConsoleUi.Success(
                    $"Transaction details: kind {details.Kind}, outputs {details.Outputs.Count}, " +
                    $"ledger sequence {details.LedgerSequence}.");
            }
        }

        bool bobReceived = (await bob.GetBusinessObjectsAsync(cancellationToken).ConfigureAwait(false))
            .Any(holding => holding.ObjectId == transferredObjectId);
        ConsoleUi.Info(bobReceived
            ? $"Bob now owns {transferredObjectId.Value}."
            : $"Bob does not show {transferredObjectId.Value} yet.");
    }

    public async Task Learn07AdvancedBusinessObjectTransactionsAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("7. Plain UTXO split and merge");
        await EnsureMainBalanceAsync(alice, Amount.FromInt64(75), cancellationToken).ConfigureAwait(false);

        LedgerSubmitResult setupMint = await alice.MintMainAsync(Amount.FromInt64(75), cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt("Treasury setup: mint a dedicated 75 main-unit UTXO to Alice", setupMint);
        TxId setupTxId = RequireTxId(setupMint, "UTXO setup mint");

        LedgerSubmitResult split = await alice.SplitUtxosAsync(
            [new OutPoint(setupTxId, 0)],
            [
                new UtxoSplitOutput(alice.Address, Amount.FromInt64(25)),
                new UtxoSplitOutput(alice.Address, Amount.FromInt64(35))
            ],
            cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt("Plain UTXO split: one selected vin creates two requested vouts plus change", split);
        TxId splitTxId = RequireTxId(split, "Plain UTXO split");

        LedgerSubmitResult merge = await alice.MergeUtxosAsync(
            [new OutPoint(splitTxId, 0), new OutPoint(splitTxId, 1)],
            cancellationToken,
            bob.Address).ConfigureAwait(false);
        ConsoleUi.PrintReceipt("Plain UTXO merge: two selected vin create one Bob vout", merge);
        await PrintTransactionShapeAsync("Plain UTXO merge details", alice, merge, cancellationToken).ConfigureAwait(false);

        ConsoleUi.Section("7b. Advanced BusinessObject transactions");

        BusinessObjectId priceId = await CreateSimpleBusinessObjectAsync("price", Amount.FromInt64(2), cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt(
            $"Set price on {priceId.Value}",
            await alice.SetBusinessObjectPriceAsync(
                priceId,
                Amount.FromInt64(9),
                cancellationToken,
                stateJson: $$"""{"title":"Priced BO","price":9,"runId":"{{runId}}"}""").ConfigureAwait(false));

        BusinessObjectId splitId = await CreateSimpleBusinessObjectAsync("split", Amount.FromInt64(8), cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt(
            $"Split payment from {splitId.Value}",
            await alice.SplitBusinessObjectPaymentAsync(
                splitId,
                [new BusinessObjectPaymentOutput(bob.Address, Amount.FromInt64(3))],
                cancellationToken,
                $$"""{"title":"Split payment BO","runId":"{{runId}}"}""").ConfigureAwait(false));

        BusinessObjectId cloneSourceId = await CreateSimpleBusinessObjectAsync("clone-source", Amount.FromInt64(1), cancellationToken).ConfigureAwait(false);
        LedgerSubmitResult clone = await alice.CloneBusinessObjectAsync(
            cloneSourceId,
            "demo-ticket",
            $$"""{"title":"Cloned BO","runId":"{{runId}}"}""",
            cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt($"Clone {cloneSourceId.Value}; result ID is assigned by the confirmed output", clone);

        BusinessObjectId mergeLeftId = await CreateSimpleBusinessObjectAsync("merge-left", Amount.FromInt64(1), cancellationToken).ConfigureAwait(false);
        BusinessObjectId mergeRightId = await CreateSimpleBusinessObjectAsync("merge-right", Amount.FromInt64(1), cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt(
            $"Merge {mergeLeftId.Value} and {mergeRightId.Value}",
            await alice.MergeBusinessObjectsAsync(
                mergeLeftId,
                mergeRightId,
                "demo-ticket",
                $$"""{"title":"Merged BO","runId":"{{runId}}"}""",
                cancellationToken).ConfigureAwait(false));

        BusinessObjectId closeId = await CreateSimpleBusinessObjectAsync("close", Amount.FromInt64(1), cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt(
            $"Close {closeId.Value}",
            await alice.CloseBusinessObjectAsync(closeId, cancellationToken, $$"""{"closed":true,"runId":"{{runId}}"}""").ConfigureAwait(false));
    }

    public async Task Learn08SubAccountsExportsAndMessageSecurityAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("8. Subaccounts, exports, and message security");

        StoredDemoKey analystKey = await keyStore.LoadOrCreateAsync("alice-analyst", cancellationToken).ConfigureAwait(false);
        BusinessObjectSubAccount analyst = await alice.Client
            .CreateSubAccountAsync(alice.Account, "analyst", analystKey.Envelope, cancellationToken)
            .ConfigureAwait(false);
        LedgerAccountKeyEnvelope exportedAlice = await alice.Client
            .ExportAccountKeyAsync(alice.Account.KeySource, cancellationToken)
            .ConfigureAwait(false);
        LedgerSubAccountEnvelope exportedAnalyst = await alice.Client
            .ExportSubAccountAsync(analyst, cancellationToken)
            .ConfigureAwait(false);
        BusinessObjectSubAccount importedAnalyst = await alice.Client
            .ImportSubAccountAsync(alice.Account, exportedAnalyst, cancellationToken)
            .ConfigureAwait(false);

        ConsoleUi.Success(
            $"Subaccount '{importedAnalyst.Name}' exported/imported for {ConsoleUi.Short(importedAnalyst.Address.Value)}. " +
            $"Parent export key id: {exportedAlice.KeyId}.");

        LedgerAccountKeyEnvelope exportedBob = await bob.Client
            .ExportAccountKeyAsync(bob.Account.KeySource, cancellationToken)
            .ConfigureAwait(false);
        string message = $"hello from console demo {runId}";
        LedgerMessageSignature signature = await alice.Client
            .SignMessageAsync(exportedAlice, message, cancellationToken)
            .ConfigureAwait(false);
        bool verified = alice.Client.VerifyMessage(message, signature);
        HashHex sharedSecret = await alice.Client
            .DeriveSharedSecretHashAsync(exportedAlice, exportedBob.PublicKeyMaterial, cancellationToken)
            .ConfigureAwait(false);
        LedgerEncryptedMessage encrypted = await alice.Client
            .EncryptMessageAsync(exportedAlice, message, exportedBob.Address, exportedBob.PublicKeyMaterial, cancellationToken)
            .ConfigureAwait(false);
        string decrypted = await bob.Client
            .DecryptMessageAsync(exportedBob, encrypted, cancellationToken)
            .ConfigureAwait(false);

        ConsoleUi.Success(
            $"Message signature verified={verified}; shared secret {ConsoleUi.Short(sharedSecret.Value)}; " +
            $"decrypted='{decrypted}'.");
    }

    public async Task Learn10ExpectedFailuresAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("10. Expected failures");
        LedgerAccountBalance bobBalance = await bob.GetBalanceAsync(cancellationToken).ConfigureAwait(false);
        await ConsoleUi.ExpectFailureAsync(
            $"Bob tries to pay {bobBalance.MainBalance.Value + 1m} with balance {bobBalance.MainBalance}",
            () => bob.SendPaymentAsync(alice, new Amount(bobBalance.MainBalance.Value + 1m), cancellationToken)).ConfigureAwait(false);

        if (dataItemObjectId is { } objectId)
        {
            await ConsoleUi.ExpectFailureAsync(
                "Alice tries to send the transferred BO again after Bob owns it",
                () => alice.BuildBusinessObjectTransferAsync(objectId, bob, cancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task Learn09AccountAutoRefreshAsync(CancellationToken cancellationToken)
    {
        ConsoleUi.Section("9. Account auto-refresh observer");
        await EnsureMainBalanceAsync(alice, Amount.FromInt64(15), cancellationToken).ConfigureAwait(false);

        ILedgerAccountHandler handler = alice.Client.AccountHandler;
        var observed = new TaskCompletionSource<AccountTransactionsObservedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        void CaptureBobTransaction(object? sender, AccountTransactionsObservedEventArgs args)
        {
            if (args.Account.Address == bob.Address && args.NewTransactions.Count > 0)
            {
                observed.TrySetResult(args);
            }
        }

        handler.NewTransactionsObserved += CaptureBobTransaction;
        await using IAccountRefreshHandle refresh = await handler
            .StartAutoRefreshAsync(
                account: bob.Account,
                castRegistry: null,
                options: new AccountRefreshOptions
                {
                    Interval = TimeSpan.FromMilliseconds(250),
                    MaxTransactionCount = 20,
                    IncludeTransactionData = true
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ConsoleUi.Info("Bob refresh loop started with a 250 ms interval.");
            LedgerSubmitResult payment = await alice.SendPaymentAsync(bob, Amount.FromInt64(3), cancellationToken).ConfigureAwait(false);
            ConsoleUi.PrintReceipt("Alice sends Bob 3 main units while Bob refreshes", payment);

            AccountTransactionsObservedEventArgs observedEvent = await observed.Task
                .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
            LedgerTransactionSummary transaction = observedEvent.NewTransactions[0];
            ConsoleUi.Success(
                $"Auto-refresh observed {observedEvent.NewTransactions.Count} new transaction(s) for Bob; " +
                $"newest {ConsoleUi.Short(transaction.TxId.Value)}.");

            AccountRefreshSnapshot? snapshot = refresh.Account.CurrentRefreshSnapshot;
            if (snapshot is not null)
            {
                ConsoleUi.Info(
                    $"Bob snapshot now has {snapshot.Transactions.Count} recent transaction(s), " +
                    $"{snapshot.BusinessObjects.Count} BO(s), and main balance {snapshot.Balance.MainBalance}.");
            }
        }
        finally
        {
            handler.NewTransactionsObserved -= CaptureBobTransaction;
            await refresh.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task EnsureMainBalanceAsync(
        DemoAccountDriver account,
        Amount minimum,
        CancellationToken cancellationToken)
    {
        LedgerAccountBalance balance = await account.GetBalanceAsync(cancellationToken).ConfigureAwait(false);
        if (balance.MainBalance.Value >= minimum.Value)
        {
            return;
        }

        Amount mintAmount = new(minimum.Value - balance.MainBalance.Value + 25m);
        ConsoleUi.PrintReceipt(
            $"Treasury setup: mint {mintAmount} main units to {account.Name}",
            await account.MintMainAsync(mintAmount, cancellationToken).ConfigureAwait(false));
    }

    private async Task<LedgerPermissionSource> EnsureMintPermissionAsync(
        TokenId tokenId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerPermissionSource> permissions = await alice
            .GetMintPermissionSourcesAsync(tokenId, cancellationToken)
            .ConfigureAwait(false);
        if (permissions.Count > 0)
        {
            return permissions[0];
        }

        await GrantMintPermissionAsync(GetTokenNamespace(tokenId), alice.Address, cancellationToken).ConfigureAwait(false);
        permissions = await alice
            .GetMintPermissionSourcesAsync(tokenId, cancellationToken)
            .ConfigureAwait(false);

        return permissions.FirstOrDefault()
            ?? throw new InvalidOperationException($"Alice does not have a permission source for {tokenId.Value}.");
    }

    private async Task GrantMintPermissionAsync(
        string tokenNamespace,
        Address address,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { BaseAddress = options.NodeBaseUri };
        using HttpResponseMessage response = await httpClient
            .PostAsJsonAsync(
                "api/v1/permissions/grants",
                new DemoPermissionGrantRequest(
                    tokenNamespace,
                    address.Value,
                    Amount: 1m,
                    Repeatable: true,
                    Action: PermissionTokenAuthority.MintNewTokenTypeAction,
                    options.Context.ScenarioId?.Value),
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);
        string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Node permission grant failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {content}");
        }

        DemoPermissionGrantReceipt receipt = JsonSerializer.Deserialize<DemoPermissionGrantReceipt>(content, JsonOptions)
            ?? throw new InvalidOperationException("Node permission grant returned an empty receipt.");
        ConsoleUi.Success(
            $"Granted permission {receipt.PermissionObjectId} for namespace {receipt.TokenNamespace} " +
            $"to {ConsoleUi.Short(receipt.Address)}.");
    }

    private async Task<BusinessObjectId> CreateSimpleBusinessObjectAsync(
        string label,
        Amount mainAmount,
        CancellationToken cancellationToken)
    {
        LedgerSubmitResult create = await alice.CreateBusinessObjectAsync(
            "demo-ticket",
            $$"""{"title":"{{label}} BO","runId":"{{runId}}"}""",
            mainAmount,
            cancellationToken).ConfigureAwait(false);
        ConsoleUi.PrintReceipt($"Create {label} BO; result ID is assigned by the confirmed output", create);
        return RequireBusinessObjectId(create, $"Create {label} BO");
    }

    private static TxId RequireTxId(LedgerSubmitResult result, string label)
        => result.Accepted && result.TxId is { } txId
            ? txId
            : throw new InvalidOperationException($"{label} did not return an accepted transaction id: {string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}"))}");

    private static BusinessObjectId RequireBusinessObjectId(LedgerSubmitResult result, string label)
        => BusinessObjectId.FromOutPoint(RequireTxId(result, label), 0);

    private static async Task PrintTransactionShapeAsync(
        string label,
        DemoAccountDriver account,
        LedgerSubmitResult result,
        CancellationToken cancellationToken)
    {
        if (!result.Accepted || result.TxId is not { } txId)
        {
            return;
        }

        LedgerNodeTransactionDetails? details = await account.GetTransactionDetailsAsync(txId, cancellationToken)
            .ConfigureAwait(false);
        if (details is null)
        {
            return;
        }

        ConsoleUi.Success($"{label}: vin {details.Inputs.Count}, vout {details.Outputs.Count}.");
    }

    private async Task PrintSnapshotsAsync(string label, CancellationToken cancellationToken)
    {
        ConsoleUi.Section(label);
        AccountSnapshot[] snapshots =
        [
            await alice.GetSnapshotAsync(cancellationToken).ConfigureAwait(false),
            await bob.GetSnapshotAsync(cancellationToken).ConfigureAwait(false)
        ];

        ConsoleUi.Table(
            ["Account", "Main", "UTXOs", "BOs", "Recent txs"],
            snapshots.Select(x => new[]
            {
                x.Name,
                x.MainBalance,
                x.UtxoCount.ToString(),
                x.BusinessObjectCount.ToString(),
                x.TransactionCount.ToString()
            }));
    }

    private static async Task<IReadOnlyList<LedgerTokenType>> PrintTokenTypesAsync(
        string label,
        DemoAccountDriver account,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerTokenType> tokenTypes = await account.GetTokenTypesAsync(cancellationToken).ConfigureAwait(false);
        ConsoleUi.Section(label);
        if (tokenTypes.Count == 0)
        {
            ConsoleUi.Info($"{account.Name} has no token UTXOs.");
            return tokenTypes;
        }

        ConsoleUi.Table(
            ["Token", "Total", "Source", "Unique", "Source UTXOs", "Unique UTXOs", "All UTXOs"],
            tokenTypes.Select(token => new[]
            {
                token.TokenId.Value,
                token.TotalAmount.ToString(),
                token.SourceAmount.ToString(),
                token.UniqueAmount.ToString(),
                token.SourceUtxoCount.ToString(),
                token.UniqueUtxoCount.ToString(),
                token.TotalUtxoCount.ToString()
            }));

        return tokenTypes;
    }

    private static IBusinessObjectCastRegistry CreateCastRegistry()
    {
        var clock = new SystemClock();
        return new BusinessObjectCastRegistry(
        [
            new PostBusinessObjectCaster(clock),
            new GenericBusinessObjectCaster(clock)
        ]);
    }

    private static BusinessObjectMediaPayload CreatePostPayload(
        string name,
        string text,
        IReadOnlyList<BusinessObjectDataItem> dataItems)
        => new(
            name,
            "Console demo post with typed SDK DataItems.",
            text,
            "#sdk #learning #dataitems",
            "console-demo",
            "https://example.local/candoitall/sdk",
            null,
            dataItems);

    private static string SerializePayload(BusinessObjectMediaPayload payload)
        => JsonSerializer.Serialize(payload, JsonOptions);

    private static string GetTokenNamespace(TokenId tokenId)
    {
        string value = tokenId.Value.Trim();
        int lastDot = value.LastIndexOf('.');
        return lastDot <= 0 ? value : value[..lastDot];
    }

    private sealed record LearningLesson(string Number, string Title, string Description);

    private sealed record DemoPermissionGrantRequest(
        string TokenNamespace,
        string Address,
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? Amount,
        bool? Repeatable,
        string? Action,
        string? ScenarioId);

    private sealed record DemoPermissionGrantReceipt(
        string PermissionObjectId,
        string Action,
        string TokenNamespace,
        string Address,
        string TxId,
        long LedgerSequence);
}
