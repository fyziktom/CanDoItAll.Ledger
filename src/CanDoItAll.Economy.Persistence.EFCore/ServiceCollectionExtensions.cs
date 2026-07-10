using CanDoItAll.Economy.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Economy.Persistence.EFCore;

public static class EconomyEfCoreServiceCollectionExtensions
{
    public const string DefaultInMemoryDatabaseName = "candoitall-economy-node";

    public static IServiceCollection AddEconomyInMemoryPersistence(
        this IServiceCollection services,
        string? databaseName = null,
        InMemoryDatabaseRoot? databaseRoot = null)
    {
        string resolvedDatabaseName = string.IsNullOrWhiteSpace(databaseName)
            ? DefaultInMemoryDatabaseName
            : databaseName.Trim();

        services.TryAddSingleton(databaseRoot ?? new InMemoryDatabaseRoot());
        services.AddDbContextFactory<EconomyDbContext>((serviceProvider, optionsBuilder) =>
        {
            optionsBuilder.UseInMemoryDatabase(
                resolvedDatabaseName,
                serviceProvider.GetRequiredService<InMemoryDatabaseRoot>());
        });

        services.AddEconomyEfCoreStores();
        return services;
    }

    public static IServiceCollection AddEconomyEfCoreStores(this IServiceCollection services)
    {
        services.TryAddScoped<IEconomySchemaMigrator, EfEconomySchemaMigrator>();
        services.AddScoped<ILedgerStore, EfLedgerStore>();
        services.AddScoped<EfLedgerHistoryReader>();
        services.AddScoped<EfLedgerProjectionVerifier>();
        services.AddScoped<EfLedgerProjectionRebuilder>();
        services.AddScoped<EfLedgerSnapshotService>();
        return services;
    }
}
