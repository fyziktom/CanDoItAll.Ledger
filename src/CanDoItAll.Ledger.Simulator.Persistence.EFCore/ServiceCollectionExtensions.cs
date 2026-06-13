using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Economy.Simulator.Persistence.EFCore;

public sealed class EconomySimulatorPostgreSqlProjectionOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string? MigrationsAssembly { get; set; }

    public int? CommandTimeoutSeconds { get; set; } = 30;

    public bool EnableRetryOnFailure { get; set; }
}

public static class EconomySimulatorProjectionPersistenceServiceCollectionExtensions
{
    public const string DefaultInMemoryDatabaseName = "candoitall-economy-simulator";

    public static IServiceCollection AddEconomySimulatorInMemoryProjectionPersistence(
        this IServiceCollection services,
        string? databaseName = null,
        InMemoryDatabaseRoot? databaseRoot = null)
    {
        string resolvedDatabaseName = string.IsNullOrWhiteSpace(databaseName)
            ? DefaultInMemoryDatabaseName
            : databaseName.Trim();

        services.AddSingleton(databaseRoot ?? new InMemoryDatabaseRoot());
        services.AddDbContextFactory<EconomySimulatorProjectionDbContext>((serviceProvider, optionsBuilder) =>
        {
            optionsBuilder.UseInMemoryDatabase(
                resolvedDatabaseName,
                serviceProvider.GetRequiredService<InMemoryDatabaseRoot>());
        });
        AddGraphServices(services);

        return services;
    }

    public static IServiceCollection AddEconomySimulatorPostgreSqlProjectionPersistence(
        this IServiceCollection services,
        string connectionString,
        string? migrationsAssembly = null,
        Action<EconomySimulatorPostgreSqlProjectionOptions>? configure = null)
    {
        services.AddOptions<EconomySimulatorPostgreSqlProjectionOptions>()
            .Configure(options =>
            {
                options.ConnectionString = connectionString;
                options.MigrationsAssembly = migrationsAssembly;
                configure?.Invoke(options);
            })
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "ConnectionString is required.")
            .Validate(options => options.CommandTimeoutSeconds is null or > 0, "CommandTimeoutSeconds must be positive when configured.")
            .ValidateOnStart();

        services.AddDbContextFactory<EconomySimulatorProjectionDbContext>((serviceProvider, optionsBuilder) =>
        {
            EconomySimulatorPostgreSqlProjectionOptions options = serviceProvider.GetRequiredService<IOptions<EconomySimulatorPostgreSqlProjectionOptions>>().Value;
            optionsBuilder.UseNpgsql(options.ConnectionString, npgsql =>
            {
                if (!string.IsNullOrWhiteSpace(options.MigrationsAssembly))
                {
                    npgsql.MigrationsAssembly(options.MigrationsAssembly);
                }

                if (options.CommandTimeoutSeconds is { } commandTimeout)
                {
                    npgsql.CommandTimeout(commandTimeout);
                }

                if (options.EnableRetryOnFailure)
                {
                    npgsql.EnableRetryOnFailure();
                }
            });
        });
        AddGraphServices(services);

        return services;
    }

    public static IServiceCollection AddEconomySimulatorPostgreSqlProjectionPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "EconomySimulator:PostgreSql")
    {
        services.AddOptions<EconomySimulatorPostgreSqlProjectionOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "ConnectionString is required.")
            .ValidateOnStart();

        services.AddDbContextFactory<EconomySimulatorProjectionDbContext>((serviceProvider, optionsBuilder) =>
        {
            EconomySimulatorPostgreSqlProjectionOptions options = serviceProvider.GetRequiredService<IOptions<EconomySimulatorPostgreSqlProjectionOptions>>().Value;
            optionsBuilder.UseNpgsql(options.ConnectionString, npgsql =>
            {
                if (!string.IsNullOrWhiteSpace(options.MigrationsAssembly))
                {
                    npgsql.MigrationsAssembly(options.MigrationsAssembly);
                }

                if (options.CommandTimeoutSeconds is { } commandTimeout)
                {
                    npgsql.CommandTimeout(commandTimeout);
                }
            });
        });
        AddGraphServices(services);

        return services;
    }

    private static void AddGraphServices(IServiceCollection services)
    {
        services.TryAddScoped<IEconomySimulatorGraphProjectionWriter, EconomySimulatorGraphProjectionWriter>();
        services.TryAddScoped<IEconomySimulatorGraphQueryService, EconomySimulatorGraphQueryService>();
    }
}
