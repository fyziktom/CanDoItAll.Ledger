using CanDoItAll.Economy.Simulator.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomySimulatorPersistenceRegistrationTests
{
    [Fact]
    public async Task AddProjectionPersistence_InMemoryConfiguration_RegistersResolvedSelectionAndFactory()
    {
        IConfiguration configuration = Configuration(
            ("EconomySimulator:StorageMode", "InMemory"),
            ("EconomySimulator:InMemoryDatabaseName", "simulator-registration-test"));
        var services = new ServiceCollection();

        services.AddEconomySimulatorProjectionPersistence(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        EconomySimulatorProjectionPersistenceSelection selection = provider
            .GetRequiredService<EconomySimulatorProjectionPersistenceSelection>();
        IDbContextFactory<EconomySimulatorProjectionDbContext> factory = provider
            .GetRequiredService<IDbContextFactory<EconomySimulatorProjectionDbContext>>();
        await using EconomySimulatorProjectionDbContext db = await factory
            .CreateDbContextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EconomySimulatorProjectionStorageMode.InMemory, selection.StorageMode);
        Assert.Equal("InMemory", selection.StorageModeName);
        Assert.Equal("simulator-registration-test-projections", selection.InMemoryDatabaseName);
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", db.Database.ProviderName);
    }

    [Fact]
    public void AddProjectionPersistence_UnknownMode_FailsInsteadOfFallingBackToInMemory()
    {
        IConfiguration configuration = Configuration(("EconomySimulator:StorageMode", "SqlLiteMaybe"));
        var services = new ServiceCollection();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddEconomySimulatorProjectionPersistence(configuration));

        Assert.Contains("Unsupported simulator projection storage mode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddProjectionPersistence_PostgreSqlWithoutConnectionString_FailsFast()
    {
        IConfiguration configuration = Configuration(("EconomySimulator:StorageMode", "PostgreSql"));
        var services = new ServiceCollection();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => services.AddEconomySimulatorProjectionPersistence(configuration));

        Assert.Contains("ConnectionString is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddProjectionPersistence_ColonOnlySectionName_FailsFast()
    {
        var services = new ServiceCollection();

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => services.AddEconomySimulatorProjectionPersistence(Configuration(), "::"));

        Assert.Contains("must contain a name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddProjectionPersistence_PostgreSqlConfiguration_RegistersProviderAndNormalizedOptions()
    {
        IConfiguration configuration = Configuration(
            ("EconomySimulator:StorageMode", "postgresql"),
            ("EconomySimulator:PostgreSql:ConnectionString", "Host=localhost;Database=simulator;Username=test;Password=test"),
            ("EconomySimulator:PostgreSql:CommandTimeoutSeconds", "17"),
            ("EconomySimulator:PostgreSql:EnableRetryOnFailure", "true"));
        var services = new ServiceCollection();

        services.AddEconomySimulatorProjectionPersistence(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        EconomySimulatorProjectionPersistenceSelection selection = provider
            .GetRequiredService<EconomySimulatorProjectionPersistenceSelection>();
        EconomySimulatorPostgreSqlProjectionOptions options = provider
            .GetRequiredService<IOptions<EconomySimulatorPostgreSqlProjectionOptions>>()
            .Value;
        IDbContextFactory<EconomySimulatorProjectionDbContext> factory = provider
            .GetRequiredService<IDbContextFactory<EconomySimulatorProjectionDbContext>>();
        await using EconomySimulatorProjectionDbContext db = await factory
            .CreateDbContextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EconomySimulatorProjectionStorageMode.PostgreSql, selection.StorageMode);
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
