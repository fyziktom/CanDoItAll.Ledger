using CanDoItAll.Ledger.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Ledger.Persistence.EFCore;

public static class LedgerEfCoreServiceCollectionExtensions
{
    public const string DefaultInMemoryDatabaseName = "candoitall-ledger-node";

    public static IServiceCollection AddLedgerInMemoryPersistence(
        this IServiceCollection services,
        string? databaseName = null,
        InMemoryDatabaseRoot? databaseRoot = null)
    {
        string resolvedDatabaseName = string.IsNullOrWhiteSpace(databaseName)
            ? DefaultInMemoryDatabaseName
            : databaseName.Trim();

        services.TryAddSingleton(databaseRoot ?? new InMemoryDatabaseRoot());
        services.AddDbContextFactory<LedgerDbContext>((serviceProvider, optionsBuilder) =>
        {
            optionsBuilder.UseInMemoryDatabase(
                resolvedDatabaseName,
                serviceProvider.GetRequiredService<InMemoryDatabaseRoot>());
        });

        services.AddLedgerEfCoreStores();
        return services;
    }

    public static IServiceCollection AddLedgerEfCoreStores(this IServiceCollection services)
    {
        services.TryAddScoped<ILedgerSchemaMigrator, EfLedgerSchemaMigrator>();
        services.AddScoped<ILedgerStore, EfLedgerStore>();
        services.AddScoped<EfLedgerHistoryReader>();
        services.AddScoped<EfLedgerProjectionVerifier>();
        services.AddScoped<EfLedgerProjectionRebuilder>();
        services.AddScoped<EfLedgerSnapshotService>();
        return services;
    }
}
