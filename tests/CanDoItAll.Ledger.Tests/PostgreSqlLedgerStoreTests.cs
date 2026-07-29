using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class PostgreSqlLedgerStoreTests
{
    private const string TestConnectionEnvironmentVariable = "CANDOITALL_LEDGER_TEST_CONNECTION";
    private static readonly LedgerId LedgerId = new("default");
    private static readonly Address Treasury = Address.FromPublicKey(Hex.Utf8("treasury-public"));
    private static readonly Address Ops = Address.FromPublicKey(Hex.Utf8("ops-public"));

    [Fact]
    [Trait("Category", "PostgreSqlProof")]
    public async Task PostgreSqlLedgerStore_ConfirmsTokenTransactionsWithTypedDataAndUtxoState()
    {
        string databaseName = $"candoitall_ledger_test_{Guid.NewGuid():N}";
        string? connectionString = await TryCreateConnectionStringAsync(databaseName);
        if (connectionString is null)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var factory = new TestDbContextFactory(options);

        if (!await TryResetDatabaseAsync(factory))
        {
            return;
        }

        try
        {
            ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
            var store = new EfLedgerStore(factory, hasher);
            var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
            LedgerExecutor executor = new(store, hasher, new SignaturePayloadBuilder(hasher), new TestAdminSigner(), timeProvider, new SignatureProviderRegistry(new ISignatureProvider[] { new TestSignatureProvider() }));

            var mintData = new TransactionData(
                "application/json",
                Hex.Sha256Hex("{\"token\":\"CDA-ACCESS\"}"u8.ToArray()),
                "{\"token\":\"CDA-ACCESS\"}",
                null,
                TransactionDataItemType.TokenMetadata);
            LedgerSubmitResult mint = await executor.SubmitAsync(
                new LedgerTransaction(
                    LedgerId,
                    null,
                    Version: 1,
                    TransactionKind.Genesis,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    Array.Empty<TransactionInput>(),
                    new[] { new TransactionOutput(0, Treasury, Amount.Zero, new TokenTransfer(new TokenId("CDA-ACCESS"), Amount.FromInt64(10))) },
                    mintData),
                CancellationToken.None);
            Assert.True(mint.Accepted, string.Join("; ", mint.Errors));

            OutPoint mintedOutPoint = new(mint.TxId!.Value, 0);
            timeProvider.Advance(TimeSpan.FromHours(4));
            var settingsData = new TransactionData(
                "application/json",
                Hex.Sha256Hex("{\"settingsFor\":\"ops\"}"u8.ToArray()),
                "{\"settingsFor\":\"ops\"}",
                null,
                TransactionDataItemType.AccountSettings);
            LedgerSubmitResult transfer = await executor.SubmitAsync(
                Sign(
                    new LedgerTransaction(
                    LedgerId,
                    null,
                    Version: 1,
                    TransactionKind.Transfer,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    new[] { new TransactionInput(0, mintedOutPoint, Treasury, SignatureBundle.Empty) },
                    new[]
                    {
                        new TransactionOutput(
                            0,
                            Ops,
                            Amount.Zero,
                            new TokenTransfer(new TokenId("CDA-ACCESS"), Amount.FromInt64(1)),
                            new BusinessObjectRef(new BusinessObjectLineageKey("bo_ops_settings"), 1, BusinessObjectOperation.Create, settingsData.DataHash!.Value)),
                        new TransactionOutput(1, Treasury, Amount.Zero, new TokenTransfer(new TokenId("CDA-ACCESS"), Amount.FromInt64(9)))
                    },
                    settingsData),
                    hasher),
                CancellationToken.None);
            Assert.True(transfer.Accepted, string.Join("; ", transfer.Errors));

            LedgerTransaction? storedTransfer = await store.GetTransactionAsync(LedgerId, null, transfer.TxId!.Value, CancellationToken.None);
            Assert.NotNull(storedTransfer);
            Assert.Equal(TransactionDataItemType.AccountSettings, storedTransfer!.Data!.ItemType);

            await using LedgerDbContext verifyDb = await factory.CreateDbContextAsync(CancellationToken.None);
            Assert.Contains(verifyDb.Transactions, x => x.TxId == transfer.TxId!.Value.Value && x.DataItemType == (int)TransactionDataItemType.AccountSettings);
            Assert.Contains(verifyDb.SpentOutputs, x => x.SpentInTxId == transfer.TxId!.Value.Value && x.Address == Treasury.Value);
            Assert.Contains(verifyDb.UnspentOutputs, x => x.TxId == transfer.TxId!.Value.Value && x.Address == Ops.Value && x.TokenId == "CDA-ACCESS" && x.TokenAmount == 1m);
            Assert.Contains(verifyDb.UnspentOutputs, x => x.TxId == transfer.TxId!.Value.Value && x.Address == Treasury.Value && x.TokenId == "CDA-ACCESS" && x.TokenAmount == 9m);
        }
        finally
        {
            await TryDeleteDatabaseAsync(factory, connectionString);
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlProof")]
    public async Task PostgreSqlLedgerStore_ConcurrentDoubleSpendConfirmsOnlyOneTransaction()
    {
        string databaseName = $"candoitall_ledger_test_{Guid.NewGuid():N}";
        string? connectionString = await TryCreateConnectionStringAsync(databaseName);
        if (connectionString is null)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var factory = new TestDbContextFactory(options);

        if (!await TryResetDatabaseAsync(factory))
        {
            return;
        }

        try
        {
            ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
            var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-02T00:00:00Z"));
            LedgerExecutor seedExecutor = CreateExecutor(factory, hasher, timeProvider);
            LedgerSubmitResult genesis = await seedExecutor.SubmitAsync(
                new LedgerTransaction(
                    LedgerId,
                    null,
                    Version: 1,
                    TransactionKind.Genesis,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    Array.Empty<TransactionInput>(),
                    new[] { new TransactionOutput(0, Treasury, Amount.FromInt64(100)) },
                    null),
                CancellationToken.None);
            Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));

            OutPoint source = new(genesis.TxId!.Value, 0);
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            LedgerTransaction first = Sign(
                new LedgerTransaction(
                    LedgerId,
                    null,
                    Version: 1,
                    TransactionKind.Transfer,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    new[] { new TransactionInput(0, source, Treasury, SignatureBundle.Empty) },
                    new[] { new TransactionOutput(0, Ops, Amount.FromInt64(100)) },
                    null),
                hasher);

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            LedgerTransaction second = Sign(
                first with
                {
                    Timestamp = timeProvider.UtcNow,
                    Outputs = new[]
                    {
                        new TransactionOutput(0, Ops, Amount.FromInt64(90)),
                        new TransactionOutput(1, Treasury, Amount.FromInt64(10))
                    },
                    TxId = null,
                    TxHash = null
                },
                hasher);

            LedgerExecutor executorA = CreateExecutor(factory, hasher, timeProvider);
            LedgerExecutor executorB = CreateExecutor(factory, hasher, timeProvider);
            LedgerSubmitResult[] results = await Task.WhenAll(
                executorA.SubmitAsync(first, CancellationToken.None),
                executorB.SubmitAsync(second, CancellationToken.None));

            Assert.Equal(1, results.Count(x => x.Accepted));
            LedgerSubmitResult rejected = Assert.Single(results, x => !x.Accepted);
            Assert.Contains(rejected.Errors, x => x.Code == ValidationErrorCodes.SpentInput || x.Code == ValidationErrorCodes.UnknownInput);
        }
        finally
        {
            await TryDeleteDatabaseAsync(factory, connectionString);
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlProof")]
    public async Task PostgreSqlLedgerStore_ConcurrentDuplicateTokenMintConfirmsOnlyOneTokenType()
    {
        string databaseName = $"candoitall_ledger_test_{Guid.NewGuid():N}";
        string? connectionString = await TryCreateConnectionStringAsync(databaseName);
        if (connectionString is null)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var factory = new TestDbContextFactory(options);

        if (!await TryResetDatabaseAsync(factory))
        {
            return;
        }

        try
        {
            ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
            var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-03T00:00:00Z"));
            LedgerExecutor seedExecutor = CreateExecutor(factory, hasher, timeProvider);
            LedgerSubmitResult genesis = await seedExecutor.SubmitAsync(
                new LedgerTransaction(
                    LedgerId,
                    null,
                    Version: 1,
                    TransactionKind.Genesis,
                    SigningMode.Independent,
                    timeProvider.UtcNow,
                    Array.Empty<TransactionInput>(),
                    new[]
                    {
                        new TransactionOutput(0, Treasury, Amount.FromInt64(100)),
                        new TransactionOutput(1, Treasury, Amount.FromInt64(100))
                    },
                    null),
                CancellationToken.None);
            Assert.True(genesis.Accepted, string.Join("; ", genesis.Errors));

            var tokenId = new TokenId("ops.energy.kwh");
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            LedgerTransaction first = Sign(CreateTokenMint(timeProvider.UtcNow, new OutPoint(genesis.TxId!.Value, 0), tokenId), hasher);
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            LedgerTransaction second = Sign(CreateTokenMint(timeProvider.UtcNow, new OutPoint(genesis.TxId!.Value, 1), tokenId), hasher);

            LedgerExecutor executorA = CreateExecutor(factory, hasher, timeProvider, DisabledTokenCreationPolicy.Instance);
            LedgerExecutor executorB = CreateExecutor(factory, hasher, timeProvider, DisabledTokenCreationPolicy.Instance);
            LedgerSubmitResult[] results = await Task.WhenAll(
                executorA.SubmitAsync(first, CancellationToken.None),
                executorB.SubmitAsync(second, CancellationToken.None));

            Assert.True(
                results.Count(x => x.Accepted) == 1,
                string.Join(" | ", results.SelectMany(result => result.Errors).Select(error => $"{error.Code}: {error.Message}")));
            LedgerSubmitResult rejected = Assert.Single(results, x => !x.Accepted);
            Assert.Contains(rejected.Errors, x => x.Code == ValidationErrorCodes.DuplicateTokenType);

            await using LedgerDbContext verifyDb = await factory.CreateDbContextAsync(CancellationToken.None);
            Assert.Single(verifyDb.LedgerTokenTypes.Where(x => x.TokenId == tokenId.Value));
        }
        finally
        {
            await TryDeleteDatabaseAsync(factory, connectionString);
        }
    }

    private static async Task<string?> TryCreateConnectionStringAsync(string databaseName)
    {
        string? configuredConnectionString = Environment.GetEnvironmentVariable(TestConnectionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return PostgreSqlTestDatabase.CreateConnectionString(configuredConnectionString, databaseName);
        }

        var testConnection = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Username = "cdaledger",
            Password = "cdaledger",
            Database = databaseName,
            Timeout = 2,
            CommandTimeout = 5
        };

        var availabilityConnection = new NpgsqlConnectionStringBuilder(testConnection.ConnectionString)
        {
            Database = "postgres"
        };

        try
        {
            await using var connection = new NpgsqlConnection(availabilityConnection.ConnectionString);
            await connection.OpenAsync();
        }
        catch (NpgsqlException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }

        return testConnection.ConnectionString;
    }

    private static async Task<bool> TryResetDatabaseAsync(IDbContextFactory<LedgerDbContext> factory)
    {
        try
        {
            await using LedgerDbContext setupDb = await factory.CreateDbContextAsync(CancellationToken.None);
            await setupDb.Database.EnsureDeletedAsync();
            await setupDb.Database.EnsureCreatedAsync();
            return true;
        }
        catch (Exception ex) when (IsPostgreSqlUnavailable(ex))
        {
            return false;
        }
    }

    private static async Task TryDeleteDatabaseAsync(
        IDbContextFactory<LedgerDbContext> factory,
        string connectionString)
    {
        using var pooledConnection = new NpgsqlConnection(connectionString);
        NpgsqlConnection.ClearPool(pooledConnection);
        try
        {
            await using LedgerDbContext cleanupDb = await factory.CreateDbContextAsync(CancellationToken.None);
            await cleanupDb.Database.EnsureDeletedAsync();
        }
        catch (Exception ex) when (IsPostgreSqlUnavailable(ex))
        {
        }
    }

    private static bool IsPostgreSqlUnavailable(Exception exception)
        => exception is NpgsqlException or TimeoutException ||
           exception.InnerException is not null && IsPostgreSqlUnavailable(exception.InnerException);

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new(new[]
            {
                new SignatureEnvelope(
                    SignatureAlgorithmId.VefSecp256K1Sha256,
                    "postgres-test-admin",
                    Hex.Sha256Hex("postgres-admin-public"u8.ToArray()),
                    payloadHash,
                    "postgres-admin-signature"u8.ToArray())
            });
    }

    private static LedgerExecutor CreateExecutor(
        IDbContextFactory<LedgerDbContext> factory,
        ITransactionHasher hasher,
        ITimeProvider timeProvider,
        ITokenCreationPolicy? tokenCreationPolicy = null)
        => new(
            new EfLedgerStore(factory, hasher),
            hasher,
            new SignaturePayloadBuilder(hasher),
            new TestAdminSigner(),
            timeProvider,
            new SignatureProviderRegistry(new ISignatureProvider[] { new TestSignatureProvider() }),
            tokenCreationPolicy);

    private static LedgerTransaction CreateTokenMint(DateTimeOffset timestamp, OutPoint source, TokenId tokenId)
        => new(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Mint,
            SigningMode.Independent,
            timestamp,
            new[] { new TransactionInput(0, source, Treasury, SignatureBundle.Empty) },
            new[]
            {
                new TransactionOutput(0, Ops, Amount.Zero, new TokenTransfer(tokenId, Amount.FromInt64(1))),
                new TransactionOutput(1, Treasury, Amount.FromInt64(100))
            },
            null);

    private static LedgerTransaction Sign(LedgerTransaction transaction, ITransactionHasher hasher)
    {
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        LedgerTransaction withTxId = transaction with { TxId = hasher.CalculateTxId(transaction) };
        TransactionInput[] inputs = withTxId.Inputs
            .Select(input =>
            {
                HashHex payloadHash = payloadBuilder.BuildIndependentInputPayloadHash(withTxId, input);
                byte[] publicKey = Hex.Utf8("treasury-public");
                return input with
                {
                    SignaturePayloadHash = payloadHash,
                    SignatureBundle = new SignatureBundle(new[]
                    {
                        new SignatureEnvelope(
                            SignatureAlgorithmId.EcdsaP256Sha256,
                            "treasury",
                            Address.CalculatePublicKeyHash(publicKey),
                            payloadHash,
                            Hex.Utf8("treasury-signature"),
                            publicKey)
                    })
                };
            })
            .ToArray();

        return withTxId with { Inputs = inputs };
    }

    private sealed class TestSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

        public bool IsSupported => true;

        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
            => Hex.Utf8("treasury-signature");

        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKeyMaterial)
            => payload.Length > 0 && signature.Length > 0 && publicKeyMaterial.Length > 0;
    }

    private sealed class TestDbContextFactory(DbContextOptions<LedgerDbContext> options) : IDbContextFactory<LedgerDbContext>
    {
        public LedgerDbContext CreateDbContext()
            => new(options);

        public ValueTask<LedgerDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }
}
