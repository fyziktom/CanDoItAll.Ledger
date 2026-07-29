using CanDoItAll.Ledger.Simulator.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class LedgerSimulatorPersistenceRegistrationTests
{
    [Fact]
    public async Task AddProjectionPersistence_InMemoryConfiguration_RegistersResolvedSelectionAndFactory()
    {
        IConfiguration configuration = Configuration(
            ("LedgerSimulator:StorageMode", "InMemory"),
            ("LedgerSimulator:InMemoryDatabaseName", "simulator-registration-test"));
        var services = new ServiceCollection();

        services.AddLedgerSimulatorProjectionPersistence(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        LedgerSimulatorProjectionPersistenceSelection selection = provider
            .GetRequiredService<LedgerSimulatorProjectionPersistenceSelection>();
        IDbContextFactory<LedgerSimulatorProjectionDbContext> factory = provider
            .GetRequiredService<IDbContextFactory<LedgerSimulatorProjectionDbContext>>();
        await using LedgerSimulatorProjectionDbContext db = await factory
            .CreateDbContextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LedgerSimulatorProjectionStorageMode.InMemory, selection.StorageMode);
        Assert.Equal("InMemory", selection.StorageModeName);
        Assert.Equal("simulator-registration-test-projections", selection.InMemoryDatabaseName);
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", db.Database.ProviderName);
    }

    [Fact]
    public void AddProjectionPersistence_UnknownMode_FailsInsteadOfFallingBackToInMemory()
    {
        IConfiguration configuration = Configuration(("LedgerSimulator:StorageMode", "SqlLiteMaybe"));
        var services = new ServiceCollection();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddLedgerSimulatorProjectionPersistence(configuration));

        Assert.Contains("Unsupported simulator projection storage mode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddProjectionPersistence_PostgreSqlWithoutConnectionString_FailsFast()
    {
        IConfiguration configuration = Configuration(("LedgerSimulator:StorageMode", "PostgreSql"));
        var services = new ServiceCollection();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddLedgerSimulatorProjectionPersistence(configuration));

        Assert.Contains("ConnectionString is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddProjectionPersistence_ColonOnlySectionName_FailsFast()
    {
        var services = new ServiceCollection();

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => services.AddLedgerSimulatorProjectionPersistence(Configuration(), "::"));

        Assert.Contains("must contain a name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddProjectionPersistence_PostgreSqlConfiguration_RegistersProviderAndNormalizedOptions()
    {
        IConfiguration configuration = Configuration(
            ("LedgerSimulator:StorageMode", "postgresql"),
            ("LedgerSimulator:PostgreSql:ConnectionString", "Host=localhost;Database=simulator;Username=test;Password=test"),
            ("LedgerSimulator:PostgreSql:CommandTimeoutSeconds", "17"),
            ("LedgerSimulator:PostgreSql:EnableRetryOnFailure", "true"));
        var services = new ServiceCollection();

        services.AddLedgerSimulatorProjectionPersistence(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        LedgerSimulatorProjectionPersistenceSelection selection = provider
            .GetRequiredService<LedgerSimulatorProjectionPersistenceSelection>();
        LedgerSimulatorPostgreSqlProjectionOptions options = provider
            .GetRequiredService<IOptions<LedgerSimulatorPostgreSqlProjectionOptions>>()
            .Value;
        IDbContextFactory<LedgerSimulatorProjectionDbContext> factory = provider
            .GetRequiredService<IDbContextFactory<LedgerSimulatorProjectionDbContext>>();
        await using LedgerSimulatorProjectionDbContext db = await factory
            .CreateDbContextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LedgerSimulatorProjectionStorageMode.PostgreSql, selection.StorageMode);
        Assert.Equal("PostgreSql", selection.StorageModeName);
        Assert.Null(selection.InMemoryDatabaseName);
        Assert.Equal(17, options.CommandTimeoutSeconds);
        Assert.True(options.EnableRetryOnFailure);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
        Assert.Equal(17, db.Database.GetCommandTimeout());
        Assert.True(db.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal))
            .Build();
}
