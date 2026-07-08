using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Node;
using CanDoItAll.Economy.Persistence.EFCore;
using CanDoItAll.Economy.Persistence.EFCore.Records;
using CanDoItAll.Economy.Sdk;
using CanDoItAll.Economy.Storage.InMemory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class LedgerSimulationAndPersistenceTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));

    [Fact]
    public void ManualTimeProvider_CanSetAndFastForwardUtcNow()
    {
        var provider = new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        provider.SetUtcNow(DateTimeOffset.Parse("2026-02-01T00:00:00-04:00"));
        DateTimeOffset advanced = provider.Advance(TimeSpan.FromDays(10));

        Assert.Equal(DateTimeOffset.Parse("2026-02-11T04:00:00Z"), provider.UtcNow);
        Assert.Equal(provider.UtcNow, advanced);
    }

    [Fact]
    public void TransactionHash_IncludesDataItemType()
    {
        ITransactionHasher hasher = CreateHasher();
        LedgerTransaction businessObject = CreateGenesisTransaction(
            DateTimeOffset.Parse("2026-03-01T00:00:00Z"),
            data: new TransactionData(
                "application/json",
                Hex.Sha256Hex("{}"u8.ToArray()),
                "{}",
                null,
                TransactionDataItemType.BusinessObject));

        LedgerTransaction accountSettings = businessObject with
        {
            Data = businessObject.Data! with { ItemType = TransactionDataItemType.AccountSettings }
        };

        Assert.NotEqual(hasher.CalculateTxId(businessObject), hasher.CalculateTxId(accountSettings));
        Assert.NotEqual(hasher.CalculateTxHash(businessObject), hasher.CalculateTxHash(accountSettings));
    }

    [Fact]
    public void TransactionId_IsDerivedFromCanonicalCoreContent()
    {
        ITransactionHasher hasher = CreateHasher();
        LedgerTransaction transaction = CreateTransferTransaction(
            DateTimeOffset.Parse("2026-03-15T00:00:00Z"),
            new OutPoint(new TxId("previous-tx"), 0));

        TxId txId = hasher.CalculateTxId(transaction);
        LedgerTransaction changedCore = transaction with
        {
            Outputs = new[]
            {
                transaction.Outputs[0] with { MainAmount = Amount.FromInt64(99) }
            }
        };
        LedgerTransaction changedEnvelope = transaction with
        {
            TxId = new TxId("stale-client-id"),
            TxHash = Hex.Sha256Hex("stale-client-hash"u8.ToArray()),
            Status = TransactionStatus.Confirmed,
            Inputs = new[]
            {
                transaction.Inputs[0] with
                {
                    SignaturePayloadHash = Hex.Sha256Hex("payload"u8.ToArray()),
                    SignatureBundle = new SignatureBundle(new[]
                    {
                        new SignatureEnvelope(
                            SignatureAlgorithmId.EcdsaP256Sha256,
                            "key-1",
                            Address.CalculatePublicKeyHash(Hex.Utf8("alice-public")),
                            Hex.Sha256Hex("payload"u8.ToArray()),
                            Hex.Utf8("signature"),
                            Hex.Utf8("alice-public"))
                    })
                }
            }
        };

        Assert.Equal(txId, hasher.CalculateTxId(changedEnvelope));
        Assert.NotEqual(txId, hasher.CalculateTxId(changedCore));
        Assert.NotEqual(hasher.CalculateTxHash(transaction), hasher.CalculateTxHash(changedEnvelope));
    }

    [Fact]
    public async Task LedgerExecutor_UsesManualTimeProvider_AndInMemoryStoreMovesUtxos()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerSubmitResult genesisResult = await executor.SubmitAsync(CreateGenesisTransaction(timeProvider.UtcNow), CancellationToken.None);
        Assert.True(genesisResult.Accepted);
        Assert.NotNull(genesisResult.TxId);
        TxId genesisTxId = genesisResult.TxId.Value;

        LedgerState? initialState = await store.GetLedgerStateAsync(LedgerId, null, CancellationToken.None);
        Assert.Equal(DateTimeOffset.Parse("2026-04-01T00:00:00Z"), initialState!.UpdatedAt);

        OutPoint initialOutPoint = new(genesisTxId, 0);
        OutPointKey initialKey = hasher.CalculateOutPointKey(LedgerId, null, initialOutPoint, Alice);
        Assert.NotEmpty(await store.GetUnspentOutputsAsync(LedgerId, null, [initialKey], CancellationToken.None));

        timeProvider.Advance(TimeSpan.FromDays(30));
        LedgerTransaction transfer = Sign(CreateTransferTransaction(timeProvider.UtcNow, initialOutPoint), hasher);
        LedgerSubmitResult transferResult = await executor.SubmitAsync(transfer, CancellationToken.None);

        Assert.True(transferResult.Accepted);
        TxId transferTxId = transferResult.TxId!.Value;
        LedgerState? finalState = await store.GetLedgerStateAsync(LedgerId, null, CancellationToken.None);
        Assert.Equal(DateTimeOffset.Parse("2026-05-01T00:00:00Z"), finalState!.UpdatedAt);
        Assert.Empty(await store.GetUnspentOutputsAsync(LedgerId, null, [initialKey], CancellationToken.None));
        Assert.NotEmpty(await store.GetSpentOutputsAsync(LedgerId, null, [initialKey], CancellationToken.None));

        OutPointKey bobKey = hasher.CalculateOutPointKey(LedgerId, null, new OutPoint(transferTxId, 0), Bob);
        Assert.NotEmpty(await store.GetUnspentOutputsAsync(LedgerId, null, [bobKey], CancellationToken.None));
    }

    [Fact]
    public async Task LedgerExecutor_RejectsTransferWithoutValidInputSignature()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerSubmitResult genesisResult = await executor.SubmitAsync(CreateGenesisTransaction(timeProvider.UtcNow), CancellationToken.None);
        Assert.True(genesisResult.Accepted);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerTransaction transfer = CreateTransferTransaction(timeProvider.UtcNow, new OutPoint(genesisResult.TxId!.Value, 0));
        LedgerSubmitResult result = await executor.SubmitAsync(transfer, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidSignature);
    }

    [Fact]
    public async Task LedgerExecutor_RejectsTransferWithTamperedInputSignature()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-01T00:00:00Z"));
        var registry = SignatureProviderRegistry.CreateDefault();
        var executor = new LedgerExecutor(store, hasher, new SignaturePayloadBuilder(hasher), new TestAdminSigner(), timeProvider, registry);
        (byte[] publicKey, byte[] privateKey) = EcdsaP256KeyMaterial.CreateKeyPair();
        var keySource = new InMemoryAccountKeySource(SignatureAlgorithmId.EcdsaP256Sha256, "alice-real", publicKey, privateKey);
        Address alice = keySource.Address;

        LedgerSubmitResult genesisResult = await executor.SubmitAsync(
            new LedgerTransaction(
                LedgerId,
                null,
                Version: 1,
                TransactionKind.Genesis,
                SigningMode.Independent,
                timeProvider.UtcNow,
                Array.Empty<TransactionInput>(),
                new[] { new TransactionOutput(0, alice, Amount.FromInt64(100)) },
                null),
            CancellationToken.None);
        Assert.True(genesisResult.Accepted);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerTransaction unsigned = new(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            timeProvider.UtcNow,
            new[] { new TransactionInput(0, new OutPoint(genesisResult.TxId!.Value, 0), alice, SignatureBundle.Empty) },
            new[] { new TransactionOutput(0, Bob, Amount.FromInt64(100)) },
            null);
        LedgerTransaction signed = await SignWithAccountKeyAsync(unsigned, hasher, keySource);
        SignatureEnvelope signature = signed.Inputs[0].SignatureBundle.Signatures[0];
        byte[] tamperedBytes = signature.SignatureBytes.ToArray();
        tamperedBytes[0] ^= 0x01;
        LedgerTransaction tampered = signed with
        {
            Inputs = new[]
            {
                signed.Inputs[0] with
                {
                    SignatureBundle = new SignatureBundle(new[] { signature with { SignatureBytes = tamperedBytes } })
                }
            }
        };

        LedgerSubmitResult rejected = await executor.SubmitAsync(tampered, CancellationToken.None);
        LedgerSubmitResult accepted = await executor.SubmitAsync(signed, CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Contains(rejected.Errors, x => x.Code == ValidationErrorCodes.InvalidSignature);
        Assert.True(accepted.Accepted);
    }

    [Fact]
    public async Task LedgerExecutor_RejectsDuplicateTransactionBeforeAppend()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);
        LedgerTransaction transaction = CreateGenesisTransaction(timeProvider.UtcNow);

        LedgerSubmitResult first = await executor.SubmitAsync(transaction, CancellationToken.None);
        LedgerSubmitResult duplicate = await executor.SubmitAsync(transaction, CancellationToken.None);

        Assert.True(first.Accepted);
        Assert.False(duplicate.Accepted);
        Assert.Contains(duplicate.Errors, x => x.Code == ValidationErrorCodes.DuplicateTransaction);
    }

    [Fact]
    public async Task LedgerExecutor_ReturnsRejectedResultWhenStoreReportsAppendConflict()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new ConflictingLedgerStore();
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerSubmitResult result = await executor.SubmitAsync(CreateGenesisTransaction(timeProvider.UtcNow), CancellationToken.None);

        Assert.False(result.Accepted);
        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.LedgerStateConflict, error.Code);
        Assert.Contains("changed during confirmation", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LedgerExecutor_RejectsNonGenesisAsFirstTransaction()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerSubmitResult result = await executor.SubmitAsync(
            new LedgerTransaction(
                LedgerId,
                null,
                Version: 1,
                TransactionKind.Mint,
                SigningMode.Independent,
                timeProvider.UtcNow,
                new[]
                {
                    new TransactionInput(0, new OutPoint(new TxId("previous"), 0), Alice, SignatureBundle.Empty)
                },
                new[] { new TransactionOutput(0, Alice, Amount.FromInt64(1)) },
                null),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidGenesisSequence);
    }

    [Fact]
    public async Task LedgerExecutor_RejectsInputlessNonGenesisAfterGenesis()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerSubmitResult genesis = await executor.SubmitAsync(CreateGenesisTransaction(timeProvider.UtcNow), CancellationToken.None);
        Assert.True(genesis.Accepted);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult result = await executor.SubmitAsync(
            new LedgerTransaction(
                LedgerId,
                null,
                Version: 1,
                TransactionKind.Mint,
                SigningMode.Independent,
                timeProvider.UtcNow,
                Array.Empty<TransactionInput>(),
                new[] { new TransactionOutput(0, Alice, Amount.FromInt64(1)) },
                null),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.MissingInput);
    }

    [Fact]
    public async Task LedgerExecutor_RejectsLaterGenesisTransaction()
    {
        ITransactionHasher hasher = CreateHasher();
        var store = new InMemoryLedgerStore(hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-04-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerSubmitResult first = await executor.SubmitAsync(CreateGenesisTransaction(timeProvider.UtcNow), CancellationToken.None);
        Assert.True(first.Accepted);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult second = await executor.SubmitAsync(
            CreateGenesisTransaction(timeProvider.UtcNow) with
            {
                Outputs = new[] { new TransactionOutput(0, Bob, Amount.FromInt64(1)) }
            },
            CancellationToken.None);

        Assert.False(second.Accepted);
        Assert.Contains(second.Errors, x => x.Code == ValidationErrorCodes.InvalidGenesisSequence);
    }

    [Fact]
    public void TransactionShapeValidator_RejectsAmbiguousIndexes()
    {
        var validator = new TransactionShapeValidator();
        LedgerTransaction transaction = CreateGenesisTransaction(DateTimeOffset.Parse("2026-04-01T00:00:00Z")) with
        {
            Inputs = new[]
            {
                new TransactionInput(-1, new OutPoint(new TxId("previous"), 0), Alice, SignatureBundle.Empty),
                new TransactionInput(-1, new OutPoint(new TxId("previous-other"), 0), Alice, SignatureBundle.Empty)
            },
            Outputs = new[]
            {
                new TransactionOutput(0, Alice, Amount.FromInt64(50)),
                new TransactionOutput(0, Bob, Amount.FromInt64(50))
            }
        };

        ValidationResult result = validator.ValidateShape(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidTransactionShape && x.Message.Contains("Input index", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidTransactionShape && x.Message.Contains("Duplicate input index", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidTransactionShape && x.Message.Contains("Duplicate output index", StringComparison.Ordinal));
    }

    [Fact]
    public void TransactionShapeValidator_RejectsNonDerivedAddresses()
    {
        var validator = new TransactionShapeValidator();
        LedgerTransaction transaction = CreateGenesisTransaction(DateTimeOffset.Parse("2026-04-01T00:00:00Z")) with
        {
            Inputs = new[]
            {
                new TransactionInput(0, new OutPoint(new TxId("previous"), 0), new Address("ops"), SignatureBundle.Empty)
            },
            Outputs = new[]
            {
                new TransactionOutput(0, new Address("treasury"), Amount.FromInt64(50))
            }
        };

        ValidationResult result = validator.ValidateShape(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidAddress && x.Message.Contains("Input 0", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidAddress && x.Message.Contains("Output 0", StringComparison.Ordinal));
    }

    [Fact]
    public void TransactionShapeValidator_RejectsUnsafeIdentifiersHashesAndOversizedData()
    {
        var validator = new TransactionShapeValidator();
        string oversizedJson = new('x', LedgerValidationLimits.MaxInlineJsonBytes + 1);
        LedgerTransaction transaction = CreateGenesisTransaction(DateTimeOffset.Parse("2026-04-01T00:00:00Z")) with
        {
            LedgerId = new LedgerId("default|ambiguous"),
            ScenarioId = new ScenarioId("scenario|ambiguous"),
            Outputs = new[]
            {
                new TransactionOutput(
                    0,
                    Alice,
                    Amount.FromInt64(50),
                    new TokenTransfer(new TokenId("token|ambiguous"), Amount.FromInt64(1)),
                    new BusinessObjectRef(new BusinessObjectId("bo|ambiguous"), 1, BusinessObjectOperation.Create, new HashHex("not-hex")))
            },
            Data = new TransactionData("application/json", Hex.Sha256Hex("oversized"u8.ToArray()), oversizedJson, null)
        };

        ValidationResult result = validator.ValidateShape(transaction);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidIdentifier && x.Message.Contains("ledger id", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidIdentifier && x.Message.Contains("scenario id", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidIdentifier && x.Message.Contains("token id", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidIdentifier && x.Message.Contains("business object id", StringComparison.Ordinal));
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.InvalidHash);
        Assert.Contains(result.Errors, x => x.Code == ValidationErrorCodes.ResourceLimitExceeded);
    }

    [Fact]
    public async Task EfLedgerStore_PersistsTypedTransactionDataScenarioAndUtxoMovement()
    {
        ITransactionHasher hasher = CreateHasher();
        string databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new TestDbContextFactory(options);
        var store = new EfLedgerStore(factory, hasher);
        var scenarioId = new ScenarioId("simulated-q2");
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-06-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerTransaction genesis = CreateGenesisTransaction(
            timeProvider.UtcNow,
            scenarioId,
            new TransactionData(
                "application/vnd.candoitall.account-settings+json",
                Hex.Sha256Hex("{\"theme\":\"dense\"}"u8.ToArray()),
                "{\"theme\":\"dense\"}",
                null,
                TransactionDataItemType.AccountSettings));
        LedgerSubmitResult genesisResult = await executor.SubmitAsync(genesis, CancellationToken.None);
        Assert.True(genesisResult.Accepted);
        TxId genesisTxId = genesisResult.TxId!.Value;

        LedgerTransaction? persistedGenesis = await store.GetTransactionAsync(LedgerId, scenarioId, genesisTxId, CancellationToken.None);
        Assert.NotNull(persistedGenesis);
        Assert.Equal(TransactionDataItemType.AccountSettings, persistedGenesis!.Data!.ItemType);
        Assert.Equal(scenarioId, persistedGenesis.ScenarioId);

        OutPoint firstOutPoint = new(genesisTxId, 0);
        OutPointKey firstKey = hasher.CalculateOutPointKey(LedgerId, scenarioId, firstOutPoint, Alice);

        timeProvider.Advance(TimeSpan.FromDays(7));
        LedgerSubmitResult transferResult = await executor.SubmitAsync(Sign(CreateTransferTransaction(timeProvider.UtcNow, firstOutPoint, scenarioId), hasher), CancellationToken.None);
        Assert.True(transferResult.Accepted);

        Assert.Empty(await store.GetUnspentOutputsAsync(LedgerId, scenarioId, [firstKey], CancellationToken.None));
        Assert.NotEmpty(await store.GetSpentOutputsAsync(LedgerId, scenarioId, [firstKey], CancellationToken.None));

        await using EconomyDbContext db = await factory.CreateDbContextAsync(CancellationToken.None);
        Assert.Contains(db.Transactions, x => x.TxId == genesisTxId.Value && x.DataItemType == (int)TransactionDataItemType.AccountSettings);
    }

    [Fact]
    public async Task EfLedgerStore_UpdatesAddressProjectionsAfterUtxoMovement()
    {
        ITransactionHasher hasher = CreateHasher();
        string databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new TestDbContextFactory(options);
        var store = new EfLedgerStore(factory, hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-08-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerSubmitResult genesisResult = await executor.SubmitAsync(CreateGenesisTransaction(timeProvider.UtcNow), CancellationToken.None);
        Assert.True(genesisResult.Accepted);

        await using EconomyDbContext afterGenesis = await factory.CreateDbContextAsync(CancellationToken.None);
        AddressStateProjectionRecord aliceInitial = Assert.Single(afterGenesis.AddressStateProjections, x => x.Address == Alice.Value);
        Assert.Equal(100m, aliceInitial.MainBalance);
        Assert.Equal("{}", aliceInitial.TokenBalancesJson);
        Assert.Equal(1, aliceInitial.UtxoCount);
        Assert.Equal(1, aliceInitial.LastLedgerSequence);

        OutPoint firstOutPoint = new(genesisResult.TxId!.Value, 0);
        timeProvider.Advance(TimeSpan.FromDays(1));
        LedgerSubmitResult transferResult = await executor.SubmitAsync(Sign(CreateTransferTransaction(timeProvider.UtcNow, firstOutPoint), hasher), CancellationToken.None);
        Assert.True(transferResult.Accepted);

        await using EconomyDbContext afterTransfer = await factory.CreateDbContextAsync(CancellationToken.None);
        AddressStateProjectionRecord aliceFinal = Assert.Single(afterTransfer.AddressStateProjections, x => x.Address == Alice.Value);
        AddressStateProjectionRecord bobFinal = Assert.Single(afterTransfer.AddressStateProjections, x => x.Address == Bob.Value);
        Assert.Equal(0m, aliceFinal.MainBalance);
        Assert.Equal(0, aliceFinal.UtxoCount);
        Assert.Equal(2, aliceFinal.LastLedgerSequence);
        Assert.Equal(100m, bobFinal.MainBalance);
        Assert.Equal(1, bobFinal.UtxoCount);
        Assert.Equal(2, bobFinal.LastLedgerSequence);
        Assert.False(string.IsNullOrWhiteSpace(bobFinal.StateHash));
    }

    [Fact]
    public async Task EfLedgerStore_ScopesOutPointReadsByLedgerAndScenario()
    {
        ITransactionHasher hasher = CreateHasher();
        string databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new TestDbContextFactory(options);
        var store = new EfLedgerStore(factory, hasher);
        var scenarioA = new ScenarioId("scenario-a");
        var scenarioB = new ScenarioId("scenario-b");
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerSubmitResult result = await executor.SubmitAsync(CreateGenesisTransaction(timeProvider.UtcNow, scenarioA), CancellationToken.None);
        Assert.True(result.Accepted);

        OutPointKey scenarioAKey = hasher.CalculateOutPointKey(LedgerId, scenarioA, new OutPoint(result.TxId!.Value, 0), Alice);

        Assert.NotEmpty(await store.GetUnspentOutputsAsync(LedgerId, scenarioA, [scenarioAKey], CancellationToken.None));
        Assert.Empty(await store.GetUnspentOutputsAsync(LedgerId, scenarioB, [scenarioAKey], CancellationToken.None));
    }

    [Fact]
    public async Task EfLedgerStore_TransactionalConfirmationRejectsDuplicateSpendAsResult()
    {
        ITransactionHasher hasher = CreateHasher();
        string databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new TestDbContextFactory(options);
        var store = new EfLedgerStore(factory, hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-09-15T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);

        LedgerSubmitResult genesis = await executor.SubmitAsync(CreateGenesisTransaction(timeProvider.UtcNow), CancellationToken.None);
        Assert.True(genesis.Accepted);
        OutPoint firstOutPoint = new(genesis.TxId!.Value, 0);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult first = await executor.SubmitAsync(
            Sign(CreateTransferTransaction(timeProvider.UtcNow, firstOutPoint), hasher),
            CancellationToken.None);
        Assert.True(first.Accepted);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerSubmitResult duplicateSpend = await executor.SubmitAsync(
            Sign(CreateTransferTransaction(timeProvider.UtcNow, firstOutPoint), hasher),
            CancellationToken.None);

        Assert.False(duplicateSpend.Accepted);
        Assert.Contains(duplicateSpend.Errors, x => x.Code == ValidationErrorCodes.SpentInput);
    }

    [Fact]
    public async Task EfLedgerStore_RegistersTokenTypesAndRejectsDuplicateMint()
    {
        ITransactionHasher hasher = CreateHasher();
        string databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new TestDbContextFactory(options);
        var store = new EfLedgerStore(factory, hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-09-20T00:00:00Z"));
        var tokenId = new TokenId("ops.energy.kwh");
        var tokenPolicy = new TokenCreationPolicy(new TokenCreationPolicyOptions
        {
            Enabled = true,
            TreasuryAdminAddress = Alice
        });
        var executor = new LedgerExecutor(
            store,
            hasher,
            new SignaturePayloadBuilder(hasher),
            new TestAdminSigner(),
            timeProvider,
            new SignatureProviderRegistry(new ISignatureProvider[] { new TestSignatureProvider() }),
            tokenPolicy);

        LedgerSubmitResult genesis = await executor.SubmitAsync(
            CreateGenesisTransaction(timeProvider.UtcNow) with
            {
                Outputs = new[]
                {
                    new TransactionOutput(0, Alice, Amount.FromInt64(100)),
                    new TransactionOutput(1, Alice, Amount.Zero, new TokenTransfer(tokenId, Amount.FromInt64(10)))
                }
            },
            CancellationToken.None);
        Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));

        await using (EconomyDbContext db = await factory.CreateDbContextAsync(CancellationToken.None))
        {
            Assert.Contains(db.LedgerTokenTypes, x => x.TokenId == tokenId.Value && x.CreatedTxId == genesis.TxId!.Value.Value);
        }

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        LedgerTransaction duplicateMint = new(
            LedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            timeProvider.UtcNow,
            new[] { new TransactionInput(0, new OutPoint(genesis.TxId!.Value, 0), Alice, SignatureBundle.Empty) },
            new[]
            {
                new TransactionOutput(0, Bob, Amount.Zero, new TokenTransfer(tokenId, Amount.FromInt64(1)))
            },
            Data: null);

        LedgerSubmitResult duplicate = await executor.SubmitAsync(Sign(duplicateMint, hasher), CancellationToken.None);

        Assert.False(duplicate.Accepted);
        Assert.Contains(duplicate.Errors, x => x.Code == ValidationErrorCodes.DuplicateTokenType);
    }

    [Fact]
    public async Task EfLedgerStore_RejectsAppendWhenLedgerStateExpectationIsStale()
    {
        ITransactionHasher hasher = CreateHasher();
        string databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new TestDbContextFactory(options);
        var store = new EfLedgerStore(factory, hasher);
        LedgerTransaction transaction = CreateGenesisTransaction(DateTimeOffset.Parse("2026-10-01T00:00:00Z"));
        TxId txId = hasher.CalculateTxId(transaction);
        HashHex txHash = hasher.CalculateTxHash(transaction);
        var staleConfirmed = new ConfirmedTransaction(
            transaction with { TxId = txId, TxHash = txHash, Status = TransactionStatus.Confirmed },
            LedgerSequence: 2,
            PreviousLedgerHash: new HashHex(new string('0', 64)),
            LedgerEntryHash: Hex.Sha256Hex("stale-ledger-entry"u8.ToArray()),
            AdminSignature: new TestAdminSigner().Sign(Hex.Sha256Hex("stale-ledger-entry"u8.ToArray())),
            ConfirmedAt: DateTimeOffset.Parse("2026-10-01T00:00:00Z"));

        LedgerStoreConflictException error = await Assert.ThrowsAsync<LedgerStoreConflictException>(
            () => store.AppendConfirmedTransactionAsync(staleConfirmed, CancellationToken.None));
        Assert.Equal(ValidationErrorCodes.LedgerStateConflict, error.Code);
        Assert.Contains("Ledger state changed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EconomyNodeService_UsesProjectionBackedAccountSummariesAfterMintAndTransfer()
    {
        ITransactionHasher hasher = CreateHasher();
        string databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<EconomyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new TestDbContextFactory(options);
        var store = new EfLedgerStore(factory, hasher);
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-11-01T00:00:00Z"));
        LedgerExecutor executor = CreateExecutor(store, hasher, timeProvider);
        var node = new EconomyNodeService(factory, executor, timeProvider);

        LedgerSubmitResult genesis = await executor.SubmitAsync(
            CreateGenesisTransaction(timeProvider.UtcNow) with
            {
                Outputs = new[] { new TransactionOutput(0, Alice, Amount.FromDecimal(100.25m)) }
            },
            CancellationToken.None);
        Assert.True(genesis.Accepted);

        IReadOnlyList<AccountSummary> afterMint = await node.GetAccountsAsync(CancellationToken.None);
        AccountSummary aliceInitial = Assert.Single(afterMint);
        Assert.Equal(Alice.Value, aliceInitial.Address);
        Assert.Equal(100.25m, aliceInitial.MainBalance);
        Assert.Equal(1, aliceInitial.UtxoCount);

        LedgerSubmitResult transfer = await node.SubmitLedgerTransactionAsync(
            Sign(
                new LedgerTransaction(
                    LedgerId,
                    null,
                    Version: 1,
                    TransactionKind.Transfer,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    new[] { new TransactionInput(0, new OutPoint(genesis.TxId!.Value, 0), Alice, SignatureBundle.Empty) },
                    new[]
                    {
                        new TransactionOutput(0, Bob, Amount.FromDecimal(40.10m)),
                        new TransactionOutput(1, Alice, Amount.FromDecimal(60.15m))
                    },
                    null),
                hasher),
            CancellationToken.None);
        Assert.True(transfer.Accepted);

        IReadOnlyList<AccountSummary> afterTransfer = await node.GetAccountsAsync(CancellationToken.None);
        AccountSummary alice = Assert.Single(afterTransfer, x => x.Address == Alice.Value);
        AccountSummary bob = Assert.Single(afterTransfer, x => x.Address == Bob.Value);
        Assert.Equal(60.15m, alice.MainBalance);
        Assert.Equal(1, alice.UtxoCount);
        Assert.Equal(40.10m, bob.MainBalance);
        Assert.Equal(1, bob.UtxoCount);

        await using EconomyDbContext db = await factory.CreateDbContextAsync(CancellationToken.None);
        Assert.Contains(db.AddressStateProjections, x => x.Address == Alice.Value);
        Assert.Contains(db.AddressStateProjections, x => x.Address == Bob.Value);
    }

    private static LedgerTransaction CreateGenesisTransaction(
        DateTimeOffset timestamp,
        ScenarioId? scenarioId = null,
        TransactionData? data = null)
        => new(
            LedgerId,
            scenarioId,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            timestamp,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(0, Alice, Amount.FromInt64(100))
            },
            data);

    private static LedgerTransaction CreateTransferTransaction(DateTimeOffset timestamp, OutPoint previousOutPoint, ScenarioId? scenarioId = null)
        => new(
            LedgerId,
            scenarioId,
            Version: 1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            timestamp,
            new[]
            {
                new TransactionInput(0, previousOutPoint, Alice, SignatureBundle.Empty)
            },
            new[]
            {
                new TransactionOutput(0, Bob, Amount.FromInt64(100))
            },
            null);

    private static LedgerExecutor CreateExecutor(ILedgerStore store, ITransactionHasher hasher, ITimeProvider timeProvider)
        => new(store, hasher, new SignaturePayloadBuilder(hasher), new TestAdminSigner(), timeProvider, new SignatureProviderRegistry(new ISignatureProvider[] { new TestSignatureProvider() }));

    private static ITransactionHasher CreateHasher()
        => new TransactionHasher(new DefaultTransactionCanonicalizer());

    private static LedgerTransaction Sign(LedgerTransaction transaction, ITransactionHasher hasher)
    {
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        LedgerTransaction withTxId = transaction with { TxId = hasher.CalculateTxId(transaction) };
        TransactionInput[] inputs = withTxId.Inputs
            .Select(input =>
            {
                HashHex payloadHash = payloadBuilder.BuildIndependentInputPayloadHash(withTxId, input);
                byte[] publicKey = Hex.Utf8("alice-public");
                return input with
                {
                    SignaturePayloadHash = payloadHash,
                    SignatureBundle = new SignatureBundle(new[]
                    {
                        new SignatureEnvelope(
                            SignatureAlgorithmId.EcdsaP256Sha256,
                            "alice",
                            Address.CalculatePublicKeyHash(publicKey),
                            payloadHash,
                            Hex.Utf8("alice-signature"),
                            publicKey)
                    })
                };
            })
            .ToArray();

        return withTxId with { Inputs = inputs };
    }

    private static async Task<LedgerTransaction> SignWithAccountKeyAsync(
        LedgerTransaction transaction,
        ITransactionHasher hasher,
        IAccountKeySource keySource)
    {
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        var signer = new AccountInputSigner(SignatureProviderRegistry.CreateDefault());
        LedgerTransaction withTxId = transaction with { TxId = hasher.CalculateTxId(transaction) };
        var signedInputs = new List<TransactionInput>();

        foreach (TransactionInput input in withTxId.Inputs.OrderBy(x => x.InputIndex))
        {
            HashHex payloadHash = payloadBuilder.BuildIndependentInputPayloadHash(withTxId, input);
            SignatureEnvelope signature = await signer.SignAsync(keySource, payloadHash, CancellationToken.None);
            signedInputs.Add(input with
            {
                SignaturePayloadHash = payloadHash,
                SignatureBundle = new SignatureBundle(new[] { signature })
            });
        }

        return withTxId with { Inputs = signedInputs };
    }

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new(new[]
            {
                new SignatureEnvelope(
                    SignatureAlgorithmId.VefSecp256K1Sha256,
                    "test-admin",
                    Hex.Sha256Hex("admin-public"u8.ToArray()),
                    payloadHash,
                    "admin-signature"u8.ToArray())
            });
    }

    private sealed class TestSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

        public bool IsSupported => true;

        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
            => Hex.Utf8("alice-signature");

        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
            => payload.Length > 0 && signature.Length > 0 && publicKeyMaterial.Length > 0;
    }

    private sealed class ConflictingLedgerStore : ILedgerStore
    {
        public Task<LedgerState?> GetLedgerStateAsync(LedgerId ledgerId, ScenarioId? scenarioId, CancellationToken cancellationToken)
            => Task.FromResult<LedgerState?>(null);

        public Task<RegisteredAddress?> GetRegisteredAddressAsync(LedgerId ledgerId, Address address, CancellationToken cancellationToken)
            => Task.FromResult<RegisteredAddress?>(null);

        public Task<LedgerTransaction?> GetTransactionAsync(LedgerId ledgerId, ScenarioId? scenarioId, TxId txId, CancellationToken cancellationToken)
            => Task.FromResult<LedgerTransaction?>(null);

        public Task<IReadOnlyDictionary<OutPointKey, UnspentOutput>> GetUnspentOutputsAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            IReadOnlyList<OutPointKey> outPointKeys,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<OutPointKey, UnspentOutput>>(new Dictionary<OutPointKey, UnspentOutput>());

        public Task<IReadOnlyDictionary<OutPointKey, SpentOutput>> GetSpentOutputsAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            IReadOnlyList<OutPointKey> outPointKeys,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<OutPointKey, SpentOutput>>(new Dictionary<OutPointKey, SpentOutput>());

        public Task<IReadOnlyList<UnspentOutput>> GetUnspentOutputsByAddressAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            Address address,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UnspentOutput>>(Array.Empty<UnspentOutput>());

        public Task<bool> TokenExistsAsync(
            LedgerId ledgerId,
            ScenarioId? scenarioId,
            TokenId tokenId,
            CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task AppendConfirmedTransactionAsync(ConfirmedTransaction confirmedTransaction, CancellationToken cancellationToken)
            => throw new LedgerStoreConflictException(
                ValidationErrorCodes.LedgerStateConflict,
                "Ledger state changed during confirmation.");
    }

    private sealed class TestDbContextFactory(DbContextOptions<EconomyDbContext> options) : IDbContextFactory<EconomyDbContext>
    {
        public EconomyDbContext CreateDbContext()
            => new(options);

        public ValueTask<EconomyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}
