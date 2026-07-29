using CanDoItAll.Ledger.Persistence.EFCore;
using CanDoItAll.Ledger.Persistence.EFCore.Records;
using CanDoItAll.Ledger.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CanDoItAll.Ledger.Tests;

public sealed class PostgreSqlMigrationTests
{
    private const string TestConnectionEnvironmentVariable = "CANDOITALL_LEDGER_TEST_CONNECTION";

    [Fact]
    public void PostgreSqlRegistration_DefaultsMigrationAssemblyAndProviderMigrator()
    {
        var services = new ServiceCollection();
        services.AddLedgerPostgreSqlPersistence("Host=localhost;Database=contract;Username=user;Password=password");

        using ServiceProvider provider = services.BuildServiceProvider();
        LedgerPostgreSqlOptions options = provider.GetRequiredService<IOptions<LedgerPostgreSqlOptions>>().Value;

        Assert.Equal(typeof(PostgreSqlLedgerSchemaMigrator).Assembly.GetName().Name, options.MigrationsAssembly);
        Assert.IsType<PostgreSqlLedgerSchemaMigrator>(provider.GetRequiredService<ILedgerSchemaMigrator>());
    }

    [Fact]
    [Trait("Category", "PostgreSqlProof")]
    public async Task PostgreSqlMigrator_EmptyAndEnsureCreatedSchemasReachCurrentMigrationSet()
    {
        await using PostgreSqlMigrationDatabase? empty = await PostgreSqlMigrationDatabase.TryCreateAsync("migration_empty");
        if (empty is null)
        {
            return;
        }

        await using PostgreSqlMigrationDatabase legacy = await PostgreSqlMigrationDatabase.CreateAsync("migration_legacy", empty.AdminConnectionString);
        var migrator = new PostgreSqlLedgerSchemaMigrator();

        await using (LedgerDbContext db = empty.CreateContext())
        {
            LedgerSchemaMigrationResult result = await migrator.MigrateAsync(db, CancellationToken.None);
            Assert.False(result.BaselineAdopted);
            Assert.NotEmpty(result.AppliedMigrations);
        }

        await using (LedgerDbContext db = legacy.CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
            LedgerSchemaMigrationResult result = await migrator.MigrateAsync(db, CancellationToken.None);
            Assert.True(result.BaselineAdopted);
            Assert.Equal(result.AvailableMigrations, result.AppliedMigrations);
        }
    }

    [Fact]
    [Trait("Category", "PostgreSqlProof")]
    public async Task PostgreSqlMigrator_InvalidLegacyRowsBlockBaselineAdoption()
    {
        await using PostgreSqlMigrationDatabase? database = await PostgreSqlMigrationDatabase.TryCreateAsync("migration_invalid");
        if (database is null)
        {
            return;
        }

        await using LedgerDbContext db = database.CreateContext();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Economy_LedgerTransactionOutputs\" DROP CONSTRAINT \"CK_Economy_TxOutput_AssetSeparation\"");
        db.TransactionOutputs.Add(new LedgerTransactionOutputRecord
        {
            TxId = "invalid-mixed-output",
            OutputIndex = 0,
            Address = "invalid-address-is-sufficient-for-schema-audit",
            MainAmount = 1m,
            TokenId = "legacy.bad",
            TokenAmount = 1m
        });
        await db.SaveChangesAsync();

        var migrator = new PostgreSqlLedgerSchemaMigrator();
        LedgerSchemaMigrationException error = await Assert.ThrowsAsync<LedgerSchemaMigrationException>(
            () => migrator.MigrateAsync(db, CancellationToken.None));

        Assert.Contains("mixed main/token", error.Message, StringComparison.OrdinalIgnoreCase);
        await using NpgsqlCommand historyCheck = database.Connection.CreateCommand();
        historyCheck.CommandText = "SELECT to_regclass('\"__EFMigrationsHistory\"') IS NOT NULL";
        Assert.False((bool)(await historyCheck.ExecuteScalarAsync())!);
    }

    [Fact]
    [Trait("Category", "PostgreSqlProof")]
    public async Task PostgreSqlMigrator_ConcurrentLegacyStartupIsSerializedAcrossAdoptionAndMigration()
    {
        await using PostgreSqlMigrationDatabase? database = await PostgreSqlMigrationDatabase.TryCreateAsync("migration_concurrent");
        if (database is null)
        {
            return;
        }

        await using (LedgerDbContext setup = database.CreateContext())
        {
            await setup.Database.EnsureCreatedAsync();
        }

        await using LedgerDbContext first = database.CreateContext();
        await using LedgerDbContext second = database.CreateContext();
        var migrator = new PostgreSqlLedgerSchemaMigrator();

        LedgerSchemaMigrationResult[] results = await Task.WhenAll(
            migrator.MigrateAsync(first, CancellationToken.None),
            migrator.MigrateAsync(second, CancellationToken.None));

        Assert.Equal(1, results.Count(result => result.BaselineAdopted));
        Assert.All(results, result => Assert.Equal(result.AvailableMigrations, result.AppliedMigrations));
        await using NpgsqlCommand historyCheck = database.Connection.CreateCommand();
        historyCheck.CommandText = "SELECT count(*) FROM \"__EFMigrationsHistory\"";
        Assert.Equal(2L, Convert.ToInt64(await historyCheck.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed class PostgreSqlMigrationDatabase : IAsyncDisposable
    {
        private readonly string databaseName;
        private readonly string databaseConnectionString;

        private PostgreSqlMigrationDatabase(
            string databaseName,
            string databaseConnectionString,
            string adminConnectionString,
            NpgsqlConnection connection)
        {
            this.databaseName = databaseName;
            this.databaseConnectionString = databaseConnectionString;
            AdminConnectionString = adminConnectionString;
            Connection = connection;
        }

        public string AdminConnectionString { get; }

        public NpgsqlConnection Connection { get; }

        public static async Task<PostgreSqlMigrationDatabase?> TryCreateAsync(string suffix)
        {
            string configured = Environment.GetEnvironmentVariable(TestConnectionEnvironmentVariable)
                ?? "Host=localhost;Port=5432;Database=postgres;Username=cdaledger;Password=cdaledger;Timeout=2;Command Timeout=5";
            try
            {
                return await CreateAsync(suffix, configured);
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                return null;
            }
        }

        public static async Task<PostgreSqlMigrationDatabase> CreateAsync(string suffix, string configured)
        {
            var adminBuilder = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" };
            string databaseName = PostgreSqlTestDatabase
                .CreateConnectionString(adminBuilder.ConnectionString, suffix)
                .Let(value => new NpgsqlConnectionStringBuilder(value).Database!);

            await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString))
            {
                await admin.OpenAsync();
                await using NpgsqlCommand create = admin.CreateCommand();
                create.CommandText = $"CREATE DATABASE \"{databaseName.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
                await create.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString) { Database = databaseName };
            var connection = new NpgsqlConnection(databaseBuilder.ConnectionString);
            await connection.OpenAsync();
            return new PostgreSqlMigrationDatabase(databaseName, databaseBuilder.ConnectionString, adminBuilder.ConnectionString, connection);
        }

        public LedgerDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<LedgerDbContext>()
                .UseNpgsql(
                    databaseConnectionString,
                    npgsql => npgsql.MigrationsAssembly(typeof(PostgreSqlLedgerSchemaMigrator).Assembly.GetName().Name))
                .Options;
            return new LedgerDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(AdminConnectionString);
            await admin.OpenAsync();
            await using NpgsqlCommand drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName.Replace("\"", "\"\"", StringComparison.Ordinal)}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
    }
}

internal static class PostgreSqlMigrationTestExtensions
{
    public static TResult Let<T, TResult>(this T value, Func<T, TResult> select) => select(value);
}
