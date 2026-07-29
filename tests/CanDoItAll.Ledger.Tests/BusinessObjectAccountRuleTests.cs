using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Rules.NCalc;
using CanDoItAll.Ledger.Storage.InMemory;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class BusinessObjectAccountRuleTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("account-rule-ledger"));
    private static readonly Address Treasury = TokenCreationPolicyDefaults.TreasuryAdminAddress;
    private static readonly TokenId CarrierToken = new("bo.carrier");

    [Fact]
    public async Task Account_CreatesRuleTokenFromRuleCreationPermission()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-14T10:00:00Z"));
        BusinessObjectAccount admin = rig.CreateAccount("treasury-admin");
        BusinessObjectAccount alice = rig.CreateAccount("alice");

        LedgerSubmitResult createRule = await IssuePermissionAndCreateRuleAsync(
            admin,
            alice,
            CreateCashpoolRule(rig.CashpoolAddress));

        Assert.True(createRule.Accepted, string.Join("; ", createRule.Errors));
        BusinessObjectId ruleObjectId = CreatedObjectId(createRule);
        IReadOnlyList<LedgerSpendableOutput> outputs = await alice.GetSpendableOutputsAsync(CancellationToken.None);
        Assert.Contains(outputs, output =>
            RuleTokenAuthority.IsRuleToken(output.Token)
            && output.BusinessObject is not null
            && BusinessObjectId.FromOutPoint(output.OutPoint) == ruleObjectId);
        Assert.Contains(outputs, output =>
            PermissionTokenAuthority.IsPermissionToken(output.Token)
            && output.BusinessObject is not null
            && output.Token!.Amount.Value == 1m);
    }

    [Fact]
    public async Task RefreshRuleExecution_IsOptInAndSweepsCashpool()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-14T11:00:00Z"));
        BusinessObjectAccount admin = rig.CreateAccount("treasury-admin");
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount cashpool = rig.CreateAccount("cashpool");
        await IssuePermissionAndCreateRuleAsync(admin, alice, CreateCashpoolRule(cashpool.Address));

        AccountRefreshResult passive = await alice.RefreshAsync(
            new AccountRefreshOptions { MaxTransactionCount = 50 },
            CancellationToken.None);
        LedgerAccountBalance passiveCashpool = await cashpool.GetBalanceAsync(CancellationToken.None);

        AccountRefreshResult active = await alice.RefreshAsync(
            new AccountRefreshOptions
            {
                MaxTransactionCount = 50,
                RuleExecution = new AccountRuleExecutionOptions { Enabled = true, MaxActionsPerRefresh = 5 }
            },
            CancellationToken.None);
        LedgerAccountBalance activeCashpool = await cashpool.GetBalanceAsync(CancellationToken.None);

        Assert.Empty(passive.RuleExecutions);
        Assert.Equal(0m, passiveCashpool.MainBalance.Value);
        AccountRuleExecutionResult execution = Assert.Single(active.RuleExecutions);
        Assert.Equal(AccountRuleExecutionStatus.Executed, execution.Status);
        Assert.Equal(AccountRuleActionKind.Payment, execution.ActionKind);
        Assert.Equal(900m, activeCashpool.MainBalance.Value);
    }

    [Fact]
    public async Task RefreshRuleExecution_UsesInjectedEvaluatorContractWithoutNCalcConstruction()
    {
        var evaluator = new AlwaysFalseEvaluator();
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-14T11:30:00Z"), evaluator);
        BusinessObjectAccount admin = rig.CreateAccount("treasury-admin");
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount cashpool = rig.CreateAccount("cashpool");
        await IssuePermissionAndCreateRuleAsync(admin, alice, CreateCashpoolRule(cashpool.Address));

        AccountRefreshResult result = await alice.RefreshAsync(
            new AccountRefreshOptions
            {
                MaxTransactionCount = 50,
                RuleExecution = new AccountRuleExecutionOptions { Enabled = true, MaxActionsPerRefresh = 5 }
            },
            CancellationToken.None);

        Assert.True(evaluator.BooleanEvaluationCount > 0);
        Assert.Equal(AccountRuleExecutionStatus.Skipped, Assert.Single(result.RuleExecutions).Status);
        Assert.Equal(0m, (await cashpool.GetBalanceAsync(CancellationToken.None)).MainBalance.Value);
    }

    [Fact]
    public async Task RefreshRuleExecution_MarksTriggerBusinessObjectProcessedAndSkipsItLater()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-14T12:00:00Z"));
        BusinessObjectAccount admin = rig.CreateAccount("treasury-admin");
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        await IssuePermissionAndCreateRuleAsync(admin, alice, CreatePurchaseApprovalRule());
        LedgerSubmitResult createRequest = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "purchase-request",
                "{\"price\":150,\"processed\":false}",
                MainAmount: Amount.Zero),
            CancellationToken.None);
        Assert.True(createRequest.Accepted, string.Join("; ", createRequest.Errors));
        BusinessObjectId requestId = CreatedObjectId(createRequest);

        var options = new AccountRefreshOptions
        {
            MaxTransactionCount = 50,
            RuleExecution = new AccountRuleExecutionOptions { Enabled = true, MaxActionsPerRefresh = 5 }
        };
        AccountRefreshResult firstRefresh = await alice.RefreshAsync(options, CancellationToken.None);
        IReadOnlyList<LedgerBusinessObjectHolding> afterFirst = await alice.GetBusinessObjectsAsync(CancellationToken.None);
        LedgerBusinessObjectHolding requestAfterFirst = afterFirst.Single(holding => holding.ObjectType == "purchase-request");

        AccountRefreshResult secondRefresh = await alice.RefreshAsync(options, CancellationToken.None);

        AccountRuleExecutionResult execution = Assert.Single(firstRefresh.RuleExecutions);
        Assert.Equal(AccountRuleExecutionStatus.Executed, execution.Status);
        Assert.Equal(AccountRuleActionKind.MarkProcessed, execution.ActionKind);
        Assert.NotEqual(requestId, requestAfterFirst.ObjectId);
        Assert.Contains("\"processed\":true", requestAfterFirst.StateJson);
        Assert.Empty(secondRefresh.RuleExecutions);
    }

    [Fact]
    public async Task RefreshRuleExecution_CanCloseProcessedTriggerBusinessObject()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-14T13:00:00Z"));
        BusinessObjectAccount admin = rig.CreateAccount("treasury-admin");
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        await IssuePermissionAndCreateRuleAsync(admin, alice, CreatePurchaseApprovalRule());
        LedgerSubmitResult createRequest = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "purchase-request",
                "{\"price\":150,\"processed\":false}",
                MainAmount: Amount.Zero),
            CancellationToken.None);
        Assert.True(createRequest.Accepted, string.Join("; ", createRequest.Errors));
        BusinessObjectId requestId = CreatedObjectId(createRequest);

        AccountRefreshResult refresh = await alice.RefreshAsync(
            new AccountRefreshOptions
            {
                MaxTransactionCount = 50,
                RuleExecution = new AccountRuleExecutionOptions
                {
                    Enabled = true,
                    MaxActionsPerRefresh = 5,
                    KeepProcessedBusinessObject = false
                }
            },
            CancellationToken.None);
        IReadOnlyList<LedgerBusinessObjectHolding> holdings = await alice.GetBusinessObjectsAsync(CancellationToken.None);

        AccountRuleExecutionResult execution = Assert.Single(refresh.RuleExecutions);
        Assert.Equal(AccountRuleExecutionStatus.Executed, execution.Status);
        Assert.DoesNotContain(holdings, holding => holding.ObjectId == requestId);
    }

    [Fact]
    public async Task RefreshRuleExecution_ResolvesDynamicReceiverAndMergesTemplatedState()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-14T14:00:00Z"));
        BusinessObjectAccount admin = rig.CreateAccount("treasury-admin");
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount supplier = rig.CreateAccount("bob");
        await IssuePermissionAndCreateRuleAsync(admin, alice, CreatePayablePaymentRule());
        LedgerSubmitResult createPayable = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "payable",
                $"{{\"amount\":125,\"approved\":true,\"paid\":false,\"supplierAddress\":\"{supplier.Address.Value}\",\"processed\":false,\"source\":\"invoice-1\"}}",
                MainAmount: Amount.Zero),
            CancellationToken.None);
        Assert.True(createPayable.Accepted, string.Join("; ", createPayable.Errors));
        BusinessObjectId payableId = CreatedObjectId(createPayable);

        AccountRefreshOptions options = CreateActiveRuleOptions(maxActions: 5);
        AccountRefreshResult firstRefresh = await alice.RefreshAsync(options, CancellationToken.None);
        LedgerAccountBalance supplierBalance = await supplier.GetBalanceAsync(CancellationToken.None);
        LedgerBusinessObjectHolding payableAfterFirst = (await alice.GetBusinessObjectsAsync(CancellationToken.None))
            .Single(holding => holding.ObjectType == "payable");
        AccountRefreshResult secondRefresh = await alice.RefreshAsync(options, CancellationToken.None);

        Assert.Collection(
            firstRefresh.RuleExecutions,
            payment =>
            {
                Assert.Equal(AccountRuleActionKind.Payment, payment.ActionKind);
                Assert.Equal(AccountRuleExecutionStatus.Executed, payment.Status);
            },
            update =>
            {
                Assert.Equal(AccountRuleActionKind.UpdateBusinessObject, update.ActionKind);
                Assert.Equal(AccountRuleExecutionStatus.Executed, update.Status);
            });
        Assert.Equal(126m, supplierBalance.MainBalance.Value);
        Assert.Contains("\"amount\":125", payableAfterFirst.StateJson);
        Assert.Contains("\"source\":\"invoice-1\"", payableAfterFirst.StateJson);
        Assert.Contains("\"paid\":true", payableAfterFirst.StateJson);
        Assert.Contains("\"processed\":true", payableAfterFirst.StateJson);
        Assert.Contains("\"processedByRuleId\":\"payable-payment\"", payableAfterFirst.StateJson);
        Assert.Contains($"\"ruleExecutionId\":\"payable-payment:{payableId.Value}:", payableAfterFirst.StateJson);
        Assert.Empty(secondRefresh.RuleExecutions);
    }

    [Fact]
    public async Task RefreshRuleExecution_ReportsMissingDynamicReceiverWithoutUpdatingTrigger()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-14T15:00:00Z"));
        BusinessObjectAccount admin = rig.CreateAccount("treasury-admin");
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        await IssuePermissionAndCreateRuleAsync(admin, alice, CreatePayablePaymentRule());
        LedgerSubmitResult createPayable = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "payable",
                "{\"amount\":125,\"approved\":true,\"paid\":false,\"processed\":false}",
                MainAmount: Amount.Zero),
            CancellationToken.None);
        Assert.True(createPayable.Accepted, string.Join("; ", createPayable.Errors));

        AccountRefreshResult refresh = await alice.RefreshAsync(CreateActiveRuleOptions(maxActions: 5), CancellationToken.None);
        LedgerBusinessObjectHolding payableAfterRefresh = (await alice.GetBusinessObjectsAsync(CancellationToken.None))
            .Single(holding => holding.ObjectType == "payable");

        AccountRuleExecutionResult execution = Assert.Single(refresh.RuleExecutions);
        Assert.Equal(AccountRuleExecutionStatus.Failed, execution.Status);
        Assert.Equal(AccountRuleActionKind.Payment, execution.ActionKind);
        Assert.Contains("bo_supplier_address", execution.Message, StringComparison.Ordinal);
        Assert.Contains("\"paid\":false", payableAfterRefresh.StateJson);
        Assert.DoesNotContain("\"processed\":true", payableAfterRefresh.StateJson);
    }

    [Fact]
    public async Task RefreshRuleExecution_SupportsNewTransactionTriggers()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-14T16:00:00Z"));
        BusinessObjectAccount admin = rig.CreateAccount("treasury-admin");
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        BusinessObjectAccount cashpool = rig.CreateAccount("cashpool");
        await IssuePermissionAndCreateRuleAsync(admin, alice, CreateIncomingPaymentSweepRule(cashpool.Address));
        AccountRefreshOptions options = CreateActiveRuleOptions(maxActions: 5);
        await alice.RefreshAsync(options, CancellationToken.None);

        LedgerSubmitResult inbound = await admin.SendPaymentAsync(
            new PaymentRequest(alice.Address, Amount.FromInt64(25)),
            CancellationToken.None);
        Assert.True(inbound.Accepted, string.Join("; ", inbound.Errors));

        AccountRefreshResult refresh = await alice.RefreshAsync(options, CancellationToken.None);
        LedgerAccountBalance cashpoolBalance = await cashpool.GetBalanceAsync(CancellationToken.None);

        AccountRuleExecutionResult execution = Assert.Single(refresh.RuleExecutions);
        Assert.Equal(AccountRuleExecutionStatus.Executed, execution.Status);
        Assert.Equal(AccountRuleActionKind.Payment, execution.ActionKind);
        Assert.Equal(inbound.TxId, execution.TriggerTransactionId);
        Assert.Equal(25m, cashpoolBalance.MainBalance.Value);
    }

    [Fact]
    public async Task RefreshRuleExecution_UsesObjectIdExpressionForSnapshotStateUpdates()
    {
        TestRig rig = CreateRig(DateTimeOffset.Parse("2026-05-14T17:00:00Z"));
        BusinessObjectAccount admin = rig.CreateAccount("treasury-admin");
        BusinessObjectAccount alice = rig.CreateAccount("alice");
        LedgerSubmitResult createState = await alice.CreateBusinessObjectAsync(
            new CreateBusinessObjectRequest(
                "rule-state",
                "{\"lastSweepUnix\":0,\"note\":\"cadence\"}",
                MainAmount: Amount.Zero),
            CancellationToken.None);
        Assert.True(createState.Accepted, string.Join("; ", createState.Errors));
        BusinessObjectId stateObjectId = CreatedObjectId(createState);
        await IssuePermissionAndCreateRuleAsync(admin, alice, CreateSnapshotStateUpdateRule(stateObjectId));

        AccountRefreshResult refresh = await alice.RefreshAsync(CreateActiveRuleOptions(maxActions: 5), CancellationToken.None);
        LedgerBusinessObjectHolding stateAfterRefresh = (await alice.GetBusinessObjectsAsync(CancellationToken.None))
            .Single(holding => holding.ObjectType == "rule-state");

        AccountRuleExecutionResult execution = Assert.Single(refresh.RuleExecutions);
        Assert.Equal(AccountRuleExecutionStatus.Executed, execution.Status);
        Assert.Equal(AccountRuleActionKind.UpdateBusinessObject, execution.ActionKind);
        Assert.Contains("\"note\":\"cadence\"", stateAfterRefresh.StateJson);
        Assert.Contains("\"lastSweepUnix\":", stateAfterRefresh.StateJson);
        Assert.DoesNotContain("\"lastSweepUnix\":0", stateAfterRefresh.StateJson);
    }

    private static AccountRuleDefinition CreateCashpoolRule(Address cashpoolAddress)
        => new(
            "cashpool-sweep",
            "rules.company",
            Enabled: true,
            new AccountRuleTrigger(AccountRuleTriggerKind.AccountSnapshot),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["limit"] = "100"
            },
            new[] { "account_main > limit" },
            new[]
            {
                new AccountRuleAction(
                    AccountRuleActionKind.Payment,
                    ReceiverAddress: cashpoolAddress.Value,
                    AmountExpression: "account_main - limit")
            },
            AccountRuleProcessingOptions.Default);

    private static AccountRuleDefinition CreatePurchaseApprovalRule()
        => new(
            "purchase-approval",
            "rules.company",
            Enabled: true,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, ObjectType: "purchase-request"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["limit"] = "500"
            },
            new[] { "bo_price <= limit" },
            new[]
            {
                new AccountRuleAction(AccountRuleActionKind.MarkProcessed)
            },
            AccountRuleProcessingOptions.Default);

    private static AccountRuleDefinition CreatePayablePaymentRule()
        => new(
            "payable-payment",
            "rules.company",
            Enabled: true,
            new AccountRuleTrigger(AccountRuleTriggerKind.BusinessObject, ObjectType: "payable"),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reserve_floor"] = "100"
            },
            new[]
            {
                "bo_approved == true",
                "bo_paid == false",
                "account_main >= bo_amount + reserve_floor"
            },
            new[]
            {
                new AccountRuleAction(
                    AccountRuleActionKind.Payment,
                    ReceiverAddress: "bo_supplier_address",
                    AmountExpression: "bo_amount"),
                new AccountRuleAction(
                    AccountRuleActionKind.UpdateBusinessObject,
                    StateJson: "{\"paid\":true,\"paidAtUnix\":{{now_unix}},\"processed\":true,\"processedAtUnix\":{{now_unix}},\"processedByRuleId\":{{rule_id}},\"ruleExecutionId\":{{rule_execution_id}}}")
            },
            AccountRuleProcessingOptions.Default with { MarkTriggerProcessed = false });

    private static AccountRuleDefinition CreateIncomingPaymentSweepRule(Address cashpoolAddress)
        => new(
            "incoming-payment-sweep",
            "rules.company",
            Enabled: true,
            new AccountRuleTrigger(AccountRuleTriggerKind.NewTransaction),
            new Dictionary<string, string>(StringComparer.Ordinal),
            new[] { "tx_account_output_main >= 25" },
            new[]
            {
                new AccountRuleAction(
                    AccountRuleActionKind.Payment,
                    ReceiverAddress: cashpoolAddress.Value,
                    AmountExpression: "tx_account_output_main")
            },
            AccountRuleProcessingOptions.Default);

    private static AccountRuleDefinition CreateSnapshotStateUpdateRule(BusinessObjectId stateObjectId)
        => new(
            "snapshot-state-update",
            "rules.company",
            Enabled: true,
            new AccountRuleTrigger(AccountRuleTriggerKind.AccountSnapshot),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["state_object_id"] = stateObjectId.Value
            },
            new[] { "account_main > 100" },
            new[]
            {
                new AccountRuleAction(
                    AccountRuleActionKind.UpdateBusinessObject,
                    StateJson: "{\"lastSweepUnix\":{{now_unix}},\"updatedByRuleId\":{{rule_id}}}",
                    ObjectIdExpression: "state_object_id")
            },
            AccountRuleProcessingOptions.Default);

    private static AccountRefreshOptions CreateActiveRuleOptions(int maxActions)
        => new()
        {
            MaxTransactionCount = 50,
            IncludeTransactionData = true,
            RuleExecution = new AccountRuleExecutionOptions { Enabled = true, MaxActionsPerRefresh = maxActions }
        };

    private static BusinessObjectId CreatedObjectId(LedgerSubmitResult result)
        => BusinessObjectId.FromOutPoint(
            result.TxId ?? throw new InvalidOperationException("Accepted BusinessObject transaction must have a transaction id."),
            0);

    private static async Task<LedgerSubmitResult> IssuePermissionAndCreateRuleAsync(
        BusinessObjectAccount admin,
        BusinessObjectAccount account,
        AccountRuleDefinition rule)
    {
        LedgerSubmitResult grant = await admin.IssuePermissionAsync(
            new PermissionIssueRequest(
                account.Address,
                rule.RuleNamespace,
                Amount.FromInt64(2),
                Repeatable: true,
                Action: RuleTokenAuthority.CreateRuleTokenAction),
            CancellationToken.None);
        Assert.True(grant.Accepted, string.Join("; ", grant.Errors));

        LedgerSubmitResult createRule = await account.CreateRuleTokenAsync(
            new RuleTokenCreateRequest(rule),
            CancellationToken.None);
        Assert.True(createRule.Accepted, string.Join("; ", createRule.Errors));
        return createRule;
    }

    private static TestRig CreateRig(
        DateTimeOffset utcNow,
        IAccountRuleExpressionEvaluator? evaluator = null)
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(utcNow);
        var signatureProvider = new EchoSignatureProvider();
        var signatureRegistry = new SignatureProviderRegistry(new ISignatureProvider[] { signatureProvider });
        var gateway = new InProcessLedgerGateway(
            new LedgerExecutor(
                store,
                hasher,
                new SignaturePayloadBuilder(hasher),
                new TestAdminSigner(),
                timeProvider,
                signatureRegistry,
                new TokenCreationPolicy(new TokenCreationPolicyOptions { TreasuryAdminAddress = Treasury })),
            hasher,
            timeProvider);
        LedgerSubmitResult genesis = gateway.SubmitTransactionAsync(
                CreateBootstrapGenesis(timeProvider.UtcNow),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));

        var builder = new BusinessObjectTransactionBuilder(
            gateway,
            hasher,
            new SignaturePayloadBuilder(hasher),
            new AccountInputSigner(signatureRegistry),
            timeProvider);

        return new TestRig(
            gateway,
            builder,
            timeProvider,
            CreateKeySource("cashpool").Address,
            evaluator ?? new NCalcAccountRuleExpressionEvaluator());
    }

    private static LedgerTransaction CreateBootstrapGenesis(DateTimeOffset timestamp)
    {
        IAccountKeySource aliceKey = CreateKeySource("alice");
        IAccountKeySource bobKey = CreateKeySource("bob");
        return new LedgerTransaction(
            Context.LedgerId,
            Context.ScenarioId,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            timestamp,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(0, Treasury, Amount.FromInt64(1_000)),
                new TransactionOutput(1, Treasury, Amount.Zero, new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(10_000))),
                new TransactionOutput(2, aliceKey.Address, Amount.FromInt64(1_000)),
                new TransactionOutput(3, bobKey.Address, Amount.FromInt64(1)),
                new TransactionOutput(4, aliceKey.Address, Amount.Zero, new TokenTransfer(CarrierToken, Amount.FromInt64(100)))
            },
            null);
    }

    private static IAccountKeySource CreateKeySource(string keyId)
    {
        byte[] publicKey = keyId == "treasury-admin"
            ? Hex.Utf8("treasury-admin-public")
            : Hex.Utf8($"{keyId}-public");
        return new InMemoryAccountKeySource(
            SignatureAlgorithmId.EcdsaP256Sha256,
            keyId,
            publicKey,
            Hex.Utf8($"{keyId}-private"));
    }

    private sealed record TestRig(
        InProcessLedgerGateway Gateway,
        BusinessObjectTransactionBuilder Builder,
        ManualTimeProvider Time,
        Address CashpoolAddress,
        IAccountRuleExpressionEvaluator RuleEvaluator)
    {
        public BusinessObjectAccount CreateAccount(string keyId)
        {
            IAccountKeySource keySource = CreateKeySource(keyId);
            return new BusinessObjectAccount(
                Context,
                keySource,
                Gateway,
                Gateway,
                Builder,
                RuleEvaluator);
        }
    }

    private sealed class AlwaysFalseEvaluator : IAccountRuleExpressionEvaluator
    {
        public int BooleanEvaluationCount { get; private set; }

        public AccountRuleBooleanEvaluation EvaluateBoolean(
            string expressionText,
            IReadOnlyDictionary<string, object?> variables)
        {
            BooleanEvaluationCount++;
            return AccountRuleBooleanEvaluation.Success(false);
        }

        public AccountRuleNumberEvaluation EvaluateNumber(
            string expressionText,
            IReadOnlyDictionary<string, object?> variables)
            => AccountRuleNumberEvaluation.Failed("Number evaluation should not be reached when the condition is false.");
    }

    private sealed class EchoSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

        public bool IsSupported => true;

        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
            => System.Text.Encoding.UTF8.GetBytes($"{System.Text.Encoding.UTF8.GetString(payload)}|{System.Text.Encoding.UTF8.GetString(privateKeyMaterial)}");

        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
            => signature.Length > 0 && payload.Length > 0 && publicKeyMaterial.Length > 0;
    }

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new(new[]
            {
                new SignatureEnvelope(
                    SignatureAlgorithmId.VefSecp256K1Sha256,
                    "test-admin",
                    Hex.Sha256Hex(Hex.Utf8("admin-public")),
                    payloadHash,
                    Hex.Utf8("admin-signature"))
            });
    }
}
