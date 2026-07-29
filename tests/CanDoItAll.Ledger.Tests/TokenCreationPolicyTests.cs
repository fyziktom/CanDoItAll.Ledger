using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Storage.InMemory;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class TokenCreationPolicyTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly Address Treasury = TokenCreationPolicyDefaults.TreasuryAdminAddress;
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));

    [Fact]
    public async Task TokenCreationPolicy_RejectsNewTokenWithoutPermissionInput()
    {
        TestRig rig = await CreateSeededRigAsync();
        LedgerTransaction transaction = await CreateTokenMintWithRegularInputAsync(rig, Alice, "messages.production");

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(transaction, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedTokenCreation);
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsTreasuryBypassForNewTokenType()
    {
        TestRig rig = await CreateSeededRigAsync();
        LedgerTransaction transaction = await CreateTokenMintWithRegularInputAsync(rig, Treasury, "messages.production");

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(transaction, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedTokenCreation);
    }

    [Fact]
    public async Task TokenCreationPolicy_AllowsAdminOriginPermissionInsideNamespace()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(rig, Alice, "messages", amount: 1, repeatable: true);

        LedgerSubmitResult allowed = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Alice, "messages.production", returnPermission: true),
            CancellationToken.None);

        LedgerSubmitResult wrongNamespace = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Alice, "materials.iron", returnPermission: true),
            CancellationToken.None);

        Assert.True(allowed.Accepted, string.Join("; ", allowed.Errors));
        Assert.False(wrongNamespace.Accepted);
        Assert.Contains(wrongNamespace.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedTokenCreation);
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsDuplicateNewTokenType()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(rig, Alice, "messages", amount: 1, repeatable: true);

        LedgerSubmitResult first = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Alice, "messages.production", returnPermission: true),
            CancellationToken.None);

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult duplicate = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Alice, "messages.production", returnPermission: true),
            CancellationToken.None);

        Assert.True(first.Accepted, string.Join("; ", first.Errors));
        Assert.False(duplicate.Accepted);
        Assert.Contains(duplicate.Errors, x => x.Code == ValidationErrorCodes.DuplicateTokenType);
    }

    [Fact]
    public async Task TokenCreationPolicy_RequiresRepeatablePermissionReturn()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(rig, Alice, "messages", amount: 1, repeatable: true);

        LedgerSubmitResult missingReturn = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Alice, "messages.production", returnPermission: false),
            CancellationToken.None);

        Assert.False(missingReturn.Accepted);
        Assert.Contains(missingReturn.Errors, x => x.Message.Contains("must be returned", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsNonRepeatablePermissionReuseByReturningSameGrant()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(rig, Alice, "messages", amount: 1, repeatable: false);

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Alice, "messages.one_time", returnPermission: true),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedTokenCreation
            && x.Message.Contains("Non-repeatable permission", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TokenCreationPolicy_AllowsNonRepeatablePermissionConsumedOnMint()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(rig, Alice, "messages", amount: 1, repeatable: false);

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Alice, "messages.one_time", returnPermission: false),
            CancellationToken.None);

        Assert.True(result.Accepted, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsNonRepeatablePermissionOveruseWithinSingleMintTransaction()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(rig, Alice, "messages", amount: 1, repeatable: false);

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderMultiTokenMintAsync(rig, Alice, "messages.one", "messages.two"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedTokenCreation
            && x.Message.Contains("only authorizes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsFullPermissionTransferAsPreservedOutput()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(rig, Alice, "messages", amount: 1, repeatable: true);

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            await CreateFullPermissionTransferAsync(rig, Alice, Bob),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedTokenAuthorityGrant);
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsPermissionWithoutAdminOrigin()
    {
        TestRig rig = CreateRig();
        PermissionGrant grant = PermissionTokenAuthority.CreateMintNewTokenTypeGrant("messages", repeatable: true);
        TransactionData data = PermissionTokenAuthority.CreateData(grant);
        LedgerSubmitResult genesis = await rig.Executor.SubmitAsync(new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            rig.Time.UtcNow,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(
                    0,
                    Alice,
                    Amount.Zero,
                    new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(1)),
                    new BusinessObjectRef(
                        PermissionTokenAuthority.CreatePermissionLineageKey(grant.Action, grant.Namespace, "forged"),
                        1,
                        BusinessObjectOperation.Create,
                        data.DataHash!.Value))
            },
            data), CancellationToken.None);
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult mint = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Alice, "messages.production", returnPermission: true),
            CancellationToken.None);

        Assert.False(mint.Accepted);
        Assert.Contains(mint.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedTokenCreation);
    }

    [Fact]
    public async Task TokenCreationPolicy_AllowsNamespaceBoundSubpermissionDelegation()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(rig, Alice, "messages", amount: 1000, repeatable: true);

        LedgerSubmitResult delegated = await rig.Executor.SubmitAsync(
            await CreateSubpermissionAsync(rig, Alice, Bob, "messages.production", amount: 1, repeatable: true),
            CancellationToken.None);
        Assert.True(delegated.Accepted, string.Join("; ", delegated.Errors));

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult bobMint = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Bob, "messages.production.alert", returnPermission: true),
            CancellationToken.None);

        rig.Time.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult wrongNamespace = await rig.Executor.SubmitAsync(
            await CreatePermissionHolderTokenMintAsync(rig, Bob, "materials.iron", returnPermission: true),
            CancellationToken.None);

        Assert.True(bobMint.Accepted, string.Join("; ", bobMint.Errors));
        Assert.False(wrongNamespace.Accepted);
        Assert.Contains(wrongNamespace.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedTokenCreation);
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsSubpermissionOutsideSourceNamespace()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(rig, Alice, "messages", amount: 1000, repeatable: true);

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            await CreateSubpermissionAsync(rig, Alice, Bob, "materials", amount: 1, repeatable: true),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedTokenAuthorityGrant);
    }

    [Fact]
    public async Task TokenCreationPolicy_AllowsRuleTokenCreationFromRulePermissionAndReturnsChange()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(
            rig,
            Alice,
            "rules.company",
            amount: 2,
            repeatable: true,
            RuleTokenAuthority.CreateRuleTokenAction);

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            await CreateRuleTokenMintAsync(rig, Alice, CreateCashpoolRule(), RuleTokenOutputMode.Valid),
            CancellationToken.None);

        Assert.True(result.Accepted, string.Join("; ", result.Errors));
        IReadOnlyList<UnspentOutput> outputs = await rig.Store.GetUnspentOutputsByAddressAsync(LedgerId, null, Alice, CancellationToken.None);
        Assert.Contains(outputs, output =>
            RuleTokenAuthority.IsRuleToken(output.Token)
            && output.BusinessObject?.LineageKey == RuleTokenAuthority.CreateRuleLineageKey("cashpool-sweep"));
        Assert.Contains(outputs, output =>
            PermissionTokenAuthority.IsPermissionToken(output.Token)
            && output.BusinessObject is not null
            && output.Token!.Amount.Value == 1m);
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsRuleTokenWithoutRulePermission()
    {
        TestRig rig = await CreateSeededRigAsync();

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            await CreateRuleTokenMintWithRegularInputAsync(rig, Alice, CreateCashpoolRule()),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedRuleTokenCreation);
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsRuleTokenAtNonZeroOutput()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(
            rig,
            Alice,
            "rules.company",
            amount: 2,
            repeatable: true,
            RuleTokenAuthority.CreateRuleTokenAction);

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            await CreateRuleTokenMintAsync(rig, Alice, CreateCashpoolRule(), RuleTokenOutputMode.RuleAtOutputOne),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedRuleTokenCreation
            && x.Message.Contains("output 0", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TokenCreationPolicy_RejectsRuleTokenWithMalformedRuleData()
    {
        TestRig rig = await CreateSeededRigAsync();
        await IssuePermissionFromTreasuryAsync(
            rig,
            Alice,
            "rules.company",
            amount: 2,
            repeatable: true,
            RuleTokenAuthority.CreateRuleTokenAction);

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            await CreateRuleTokenMintAsync(rig, Alice, CreateCashpoolRule(), RuleTokenOutputMode.MalformedData),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.UnauthorizedRuleTokenCreation
            && x.Message.Contains("valid rule data", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task IssuePermissionFromTreasuryAsync(
        TestRig rig,
        Address receiver,
        string tokenNamespace,
        int amount,
        bool repeatable,
        string action = PermissionTokenAuthority.MintNewTokenTypeAction)
    {
        UnspentOutput source = await GetFirstSpendableAsync(
            rig,
            Treasury,
            output => output.Token?.TokenId == PermissionTokenAuthority.PermissionTokenId && output.BusinessObject is null);
        PermissionGrant grant = new(
            PermissionTokenAuthority.NormalizeAction(action),
            PermissionTokenAuthority.NormalizeNamespace(tokenNamespace),
            repeatable);
        TransactionData data = PermissionTokenAuthority.CreateData(grant);
        decimal change = source.Token!.Amount.Value - amount;
        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                receiver,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(amount)),
                new BusinessObjectRef(
                    PermissionTokenAuthority.CreatePermissionLineageKey(grant.Action, grant.Namespace, Guid.NewGuid().ToString("N")),
                    1,
                    BusinessObjectOperation.Create,
                    data.DataHash!.Value))
        };

        if (change > 0m)
        {
            outputs.Add(new TransactionOutput(
                1,
                Treasury,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(change))));
        }

        LedgerSubmitResult result = await rig.Executor.SubmitAsync(
            Sign(new LedgerTransaction(
                LedgerId,
                ScenarioId: null,
                Version: 1,
                TransactionKind.BusinessObject,
                SigningMode.Independent,
                rig.Time.UtcNow,
                new[] { new TransactionInput(0, source.OutPoint, Treasury, SignatureBundle.Empty) },
                outputs,
                data), rig.Hasher),
            CancellationToken.None);
        Assert.True(result.Accepted, string.Join("; ", result.Errors));
        rig.Time.Advance(TimeSpan.FromMinutes(1));
    }

    private static async Task<LedgerTransaction> CreateSubpermissionAsync(
        TestRig rig,
        Address sender,
        Address receiver,
        string tokenNamespace,
        int amount,
        bool repeatable)
    {
        UnspentOutput source = await GetFirstSpendableAsync(
            rig,
            sender,
            output => output.Token?.TokenId == PermissionTokenAuthority.PermissionTokenId && output.BusinessObject is not null);
        PermissionGrant grant = PermissionTokenAuthority.CreateMintNewTokenTypeGrant(tokenNamespace, repeatable);
        TransactionData data = PermissionTokenAuthority.CreateData(grant);
        BusinessObjectRef sourceRef = source.BusinessObject!;
        decimal change = source.Token!.Amount.Value - amount;
        var outputs = new List<TransactionOutput>
        {
            new(
                0,
                receiver,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(amount)),
                new BusinessObjectRef(
                    PermissionTokenAuthority.CreatePermissionLineageKey(grant.Action, grant.Namespace, Guid.NewGuid().ToString("N")),
                    1,
                    BusinessObjectOperation.Create,
                    data.DataHash!.Value))
        };

        if (change > 0m)
        {
            outputs.Add(new TransactionOutput(
                1,
                sender,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(change)),
                sourceRef with
                {
                    Version = sourceRef.Version + 1,
                    Operation = BusinessObjectOperation.Update
                }));
        }

        return Sign(new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            rig.Time.UtcNow,
            new[] { new TransactionInput(0, source.OutPoint, sender, SignatureBundle.Empty) },
            outputs,
            data), rig.Hasher);
    }

    private static async Task<LedgerTransaction> CreateFullPermissionTransferAsync(
        TestRig rig,
        Address sender,
        Address receiver)
    {
        UnspentOutput source = await GetFirstSpendableAsync(
            rig,
            sender,
            output => output.Token?.TokenId == PermissionTokenAuthority.PermissionTokenId && output.BusinessObject is not null);
        PermissionGrant grant = PermissionTokenAuthority.CreateMintNewTokenTypeGrant("messages", repeatable: true);
        TransactionData data = PermissionTokenAuthority.CreateData(grant);

        return Sign(new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.BusinessObject,
            SigningMode.Independent,
            rig.Time.UtcNow,
            new[] { new TransactionInput(0, source.OutPoint, sender, SignatureBundle.Empty) },
            new[]
            {
                new TransactionOutput(
                    0,
                    receiver,
                    Amount.Zero,
                    new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, source.Token!.Amount),
                    new BusinessObjectRef(
                        PermissionTokenAuthority.CreatePermissionLineageKey(grant.Action, grant.Namespace, Guid.NewGuid().ToString("N")),
                        1,
                        BusinessObjectOperation.Create,
                        data.DataHash!.Value))
            },
            data), rig.Hasher);
    }

    private static async Task<LedgerTransaction> CreatePermissionHolderMultiTokenMintAsync(
        TestRig rig,
        Address owner,
        params string[] tokenIds)
    {
        UnspentOutput permission = await GetFirstSpendableAsync(
            rig,
            owner,
            output => output.Token?.TokenId == PermissionTokenAuthority.PermissionTokenId && output.BusinessObject is not null);
        TransactionOutput[] outputs = tokenIds
            .Select((tokenId, index) => new TransactionOutput(
                index,
                owner,
                Amount.Zero,
                new TokenTransfer(new TokenId(tokenId), Amount.FromInt64(1))))
            .ToArray();

        return Sign(new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            rig.Time.UtcNow,
            new[] { new TransactionInput(0, permission.OutPoint, owner, SignatureBundle.Empty) },
            outputs,
            Data: null), rig.Hasher);
    }

    private static async Task<LedgerTransaction> CreatePermissionHolderTokenMintAsync(
        TestRig rig,
        Address owner,
        string tokenId,
        bool returnPermission)
    {
        UnspentOutput permission = await GetFirstSpendableAsync(
            rig,
            owner,
            output => output.Token?.TokenId == PermissionTokenAuthority.PermissionTokenId && output.BusinessObject is not null);
        var outputs = new List<TransactionOutput>
        {
            new(0, owner, Amount.Zero, new TokenTransfer(new TokenId(tokenId), Amount.FromInt64(1)))
        };

        if (returnPermission)
        {
            BusinessObjectRef permissionRef = permission.BusinessObject!;
            outputs.Add(new TransactionOutput(
                1,
                owner,
                permission.MainAmount,
                permission.Token,
                permissionRef with
                {
                    Version = permissionRef.Version + 1,
                    Operation = BusinessObjectOperation.Update
                }));
        }

        return Sign(new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            rig.Time.UtcNow,
            new[] { new TransactionInput(0, permission.OutPoint, owner, SignatureBundle.Empty) },
            outputs,
            Data: null), rig.Hasher);
    }

    private static async Task<LedgerTransaction> CreateTokenMintWithRegularInputAsync(TestRig rig, Address owner, string tokenId)
    {
        UnspentOutput input = await GetFirstSpendableAsync(rig, owner, output => output.BusinessObject is null && output.Token is null);
        return Sign(new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            rig.Time.UtcNow,
            new[] { new TransactionInput(0, input.OutPoint, owner, SignatureBundle.Empty) },
            new[] { new TransactionOutput(0, owner, Amount.Zero, new TokenTransfer(new TokenId(tokenId), Amount.FromInt64(1))) },
            Data: null), rig.Hasher);
    }

    private static async Task<LedgerTransaction> CreateRuleTokenMintWithRegularInputAsync(
        TestRig rig,
        Address owner,
        AccountRuleDefinition rule)
    {
        UnspentOutput input = await GetFirstSpendableAsync(rig, owner, output => output.BusinessObject is null && output.Token is null);
        TransactionData data = RuleTokenAuthority.CreateData(rule);
        return Sign(new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            rig.Time.UtcNow,
            new[] { new TransactionInput(0, input.OutPoint, owner, SignatureBundle.Empty) },
            new[]
            {
                new TransactionOutput(
                    0,
                    owner,
                    Amount.Zero,
                    new TokenTransfer(RuleTokenAuthority.RuleTokenId, Amount.FromInt64(1)),
                    new BusinessObjectRef(
                        RuleTokenAuthority.CreateRuleLineageKey(rule.RuleId),
                        1,
                        BusinessObjectOperation.Create,
                        data.DataHash!.Value))
            },
            data), rig.Hasher);
    }

    private static async Task<LedgerTransaction> CreateRuleTokenMintAsync(
        TestRig rig,
        Address owner,
        AccountRuleDefinition rule,
        RuleTokenOutputMode mode)
    {
        UnspentOutput permission = await GetFirstSpendableAsync(
            rig,
            owner,
            output => output.Token?.TokenId == PermissionTokenAuthority.PermissionTokenId && output.BusinessObject is not null);
        TransactionData data = mode == RuleTokenOutputMode.MalformedData
            ? PermissionTokenAuthority.CreateData(PermissionTokenAuthority.CreateMintNewTokenTypeGrant("rules.company", repeatable: true))
            : RuleTokenAuthority.CreateData(rule);
        BusinessObjectRef ruleRef = new(
            RuleTokenAuthority.CreateRuleLineageKey(rule.RuleId),
            1,
            BusinessObjectOperation.Create,
            data.DataHash!.Value);
        BusinessObjectRef permissionRef = permission.BusinessObject!;
        decimal change = permission.Token!.Amount.Value - 1m;

        var outputs = new List<TransactionOutput>();
        if (mode == RuleTokenOutputMode.RuleAtOutputOne)
        {
            outputs.Add(new TransactionOutput(
                0,
                owner,
                permission.MainAmount,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(change)),
                permissionRef with
                {
                    Version = permissionRef.Version + 1,
                    Operation = BusinessObjectOperation.Update
                }));
            outputs.Add(new TransactionOutput(
                1,
                owner,
                Amount.Zero,
                new TokenTransfer(RuleTokenAuthority.RuleTokenId, Amount.FromInt64(1)),
                ruleRef));
        }
        else
        {
            outputs.Add(new TransactionOutput(
                0,
                owner,
                Amount.Zero,
                new TokenTransfer(RuleTokenAuthority.RuleTokenId, Amount.FromInt64(1)),
                ruleRef));
            if (change > 0m)
            {
                outputs.Add(new TransactionOutput(
                    1,
                    owner,
                    permission.MainAmount,
                    new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(change)),
                    permissionRef with
                    {
                        Version = permissionRef.Version + 1,
                        Operation = BusinessObjectOperation.Update
                    }));
            }
        }

        return Sign(new LedgerTransaction(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            rig.Time.UtcNow,
            new[] { new TransactionInput(0, permission.OutPoint, owner, SignatureBundle.Empty) },
            outputs,
            data), rig.Hasher);
    }

    private static AccountRuleDefinition CreateCashpoolRule()
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
                    ReceiverAddress: Bob.Value,
                    AmountExpression: "account_main - limit")
            },
            AccountRuleProcessingOptions.Default);

    private enum RuleTokenOutputMode
    {
        Valid,
        RuleAtOutputOne,
        MalformedData
    }

    private static async Task<UnspentOutput> GetFirstSpendableAsync(
        TestRig rig,
        Address address,
        Func<UnspentOutput, bool> predicate)
    {
        IReadOnlyList<UnspentOutput> outputs = await rig.Store
            .GetUnspentOutputsByAddressAsync(LedgerId, null, address, CancellationToken.None)
            .ConfigureAwait(false);

        return outputs.First(predicate);
    }

    private static LedgerTransaction CreateGenesis(DateTimeOffset timestamp)
        => new(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            timestamp,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(0, Treasury, Amount.FromInt64(100)),
                new TransactionOutput(1, Treasury, Amount.Zero, new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(10_000))),
                new TransactionOutput(2, Alice, Amount.FromInt64(1)),
                new TransactionOutput(3, Bob, Amount.FromInt64(1))
            },
            Data: null);

    private static TestRig CreateRig()
    {
        ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-07T00:00:00Z"));
        var policy = new TokenCreationPolicy(new TokenCreationPolicyOptions { TreasuryAdminAddress = Treasury });
        var executor = new LedgerExecutor(
            store,
            hasher,
            new SignaturePayloadBuilder(hasher),
            new TestAdminSigner(),
            timeProvider,
            new SignatureProviderRegistry(new ISignatureProvider[] { new TestSignatureProvider() }),
            policy);
        return new TestRig(executor, store, timeProvider, hasher);
    }

    private static async Task<TestRig> CreateSeededRigAsync()
    {
        TestRig rig = CreateRig();
        LedgerSubmitResult genesis = await rig.Executor.SubmitAsync(CreateGenesis(rig.Time.UtcNow), CancellationToken.None);
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));
        rig.Time.Advance(TimeSpan.FromMinutes(1));
        return rig;
    }

    private static LedgerTransaction Sign(LedgerTransaction transaction, ITransactionHasher hasher)
    {
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        LedgerTransaction withTxId = transaction with { TxId = hasher.CalculateTxId(transaction) };
        TransactionInput[] inputs = withTxId.Inputs
            .Select(input =>
            {
                HashHex payloadHash = payloadBuilder.BuildIndependentInputPayloadHash(withTxId, input);
                byte[] publicKey = PublicKeyFor(input.Address);
                return input with
                {
                    SignaturePayloadHash = payloadHash,
                    SignatureBundle = new SignatureBundle(new[]
                    {
                        new SignatureEnvelope(
                            SignatureAlgorithmId.EcdsaP256Sha256,
                            KeyIdFor(input.Address),
                            Address.CalculatePublicKeyHash(publicKey),
                            payloadHash,
                            Hex.Utf8($"{KeyIdFor(input.Address)}-signature"),
                            publicKey)
                    })
                };
            })
            .ToArray();

        return withTxId with { Inputs = inputs };
    }

    private static byte[] PublicKeyFor(Address address)
    {
        if (address == Treasury)
        {
            return Hex.Utf8("treasury-admin-public");
        }

        if (address == Alice)
        {
            return Hex.Utf8("alice-public");
        }

        if (address == Bob)
        {
            return Hex.Utf8("bob-public");
        }

        throw new InvalidOperationException($"Unknown test signer address: {address.Value}.");
    }

    private static string KeyIdFor(Address address)
    {
        if (address == Treasury)
        {
            return "treasury-admin";
        }

        if (address == Alice)
        {
            return "alice";
        }

        if (address == Bob)
        {
            return "bob";
        }

        throw new InvalidOperationException($"Unknown test signer address: {address.Value}.");
    }

    private sealed record TestRig(
        LedgerExecutor Executor,
        ILedgerStore Store,
        ManualTimeProvider Time,
        ITransactionHasher Hasher);

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

    private sealed class TestSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

        public bool IsSupported => true;

        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
            => Hex.Utf8("test-signature");

        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
            => payload.Length > 0 && signature.Length > 0 && publicKeyMaterial.Length > 0;
    }
}
