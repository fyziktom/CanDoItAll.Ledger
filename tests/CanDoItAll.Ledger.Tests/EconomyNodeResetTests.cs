using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Node;
using CanDoItAll.Economy.Persistence.EFCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomyNodeResetTests
{
    [Fact]
    public async Task ResetAsync_RecreatesRelationalDatabaseBeforeRunningSchemaMigration()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"candoitall-economy-reset-{Guid.NewGuid():N}.db");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<EconomyDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            var dbContextFactory = new TestDbContextFactory(dbOptions);

            await using (EconomyDbContext setup = await dbContextFactory.CreateDbContextAsync())
            {
                await setup.Database.EnsureCreatedAsync();
                await setup.Database.ExecuteSqlRawAsync("CREATE TABLE ResetMarker (Id INTEGER PRIMARY KEY)");
            }

            ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
            var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-15T00:00:00Z"));
            var store = new EfLedgerStore(dbContextFactory, hasher);
            var executor = new LedgerExecutor(
                store,
                hasher,
                new SignaturePayloadBuilder(hasher),
                new TestAdminSigner(),
                timeProvider,
                new SignatureProviderRegistry([new TestSignatureProvider()]));
            var schemaMigrator = new DatabaseCreationProbeSchemaMigrator(databasePath);
            var node = new EconomyNodeService(
                dbContextFactory,
                executor,
                timeProvider,
                hostOptions: new EconomyNodeHostOptions
                {
                    AllowLedgerReset = true,
                    BootstrapTreasuryAdmin = false,
                    SchemaManagementMode = EconomyNodeSchemaManagementMode.Migrate
                },
                schemaMigrator: schemaMigrator);

            await node.ResetAsync(CancellationToken.None);

            Assert.True(schemaMigrator.WasCalled);
            Assert.True(schemaMigrator.DatabaseExistedBeforeMigration);
            await using EconomyDbContext verify = await dbContextFactory.CreateDbContextAsync();
            Assert.Empty(await verify.LedgerStates.ToListAsync());
            Assert.Equal(
                0L,
                await CountSqliteTablesAsync(verify, "ResetMarker"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private static async Task<long> CountSqliteTablesAsync(EconomyDbContext db, string tableName)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
            return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private sealed class DatabaseCreationProbeSchemaMigrator(string databasePath) : IEconomySchemaMigrator
    {
        public bool WasCalled { get; private set; }

        public bool DatabaseExistedBeforeMigration { get; private set; }

        public async Task<EconomySchemaMigrationResult> MigrateAsync(
            EconomyDbContext db,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            DatabaseExistedBeforeMigration = File.Exists(databasePath);
            if (!DatabaseExistedBeforeMigration)
            {
                throw new InvalidOperationException("The relational database was not recreated before schema migration.");
            }

            await db.Database.EnsureCreatedAsync(cancellationToken);
            return new EconomySchemaMigrationResult(false, [], []);
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<EconomyDbContext> options)
        : IDbContextFactory<EconomyDbContext>
    {
        public EconomyDbContext CreateDbContext() => new(options);

        public ValueTask<EconomyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(CreateDbContext());
    }

    private sealed class TestAdminSigner : ILedgerAdminSigner
    {
        public SignatureBundle Sign(HashHex payloadHash)
            => new([
                new SignatureEnvelope(
                    SignatureAlgorithmId.EcdsaP256Sha256,
                    "reset-test-admin",
                    new HashHex(new string('a', 64)),
                    payloadHash,
                    Hex.Utf8("reset-test-signature"))
            ]);
    }

    private sealed class TestSignatureProvider : ISignatureProvider
    {
        public SignatureAlgorithmId Algorithm => SignatureAlgorithmId.EcdsaP256Sha256;

        public bool IsSupported => true;

        public byte[] Sign(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> privateKeyMaterial)
            => Hex.Utf8("reset-test-signature");

        public bool Verify(
            ReadOnlySpan<byte> payload,
            ReadOnlySpan<byte> signature,
            ReadOnlySpan<byte> publicKeyMaterial)
            => true;
    }
}
