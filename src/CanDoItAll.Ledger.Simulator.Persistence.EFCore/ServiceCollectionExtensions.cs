using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Ledger.Simulator.Persistence.EFCore;

public enum LedgerSimulatorProjectionStorageMode
{
    InMemory = 0,
    PostgreSql = 1
}

public sealed record LedgerSimulatorProjectionPersistenceSelection(
    LedgerSimulatorProjectionStorageMode StorageMode,
    string? InMemoryDatabaseName)
{
    public string StorageModeName => StorageMode.ToString();
}

public sealed class LedgerSimulatorPostgreSqlProjectionOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string? MigrationsAssembly { get; set; }

    public int? CommandTimeoutSeconds { get; set; } = 30;

    public bool EnableRetryOnFailure { get; set; }
}

public static class LedgerSimulatorProjectionPersistenceServiceCollectionExtensions
{
    public const string DefaultInMemoryDatabaseName = "candoitall-ledger-simulator";

    public static IServiceCollection AddLedgerSimulatorProjectionPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "LedgerSimulator")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(sectionName))
        {
            throw new ArgumentException("The simulator persistence configuration section name cannot be empty.", nameof(sectionName));
        }

        string normalizedSectionName = sectionName.Trim().TrimEnd(':');
        if (string.IsNullOrWhiteSpace(normalizedSectionName))
        {
            throw new ArgumentException(
                "The simulator persistence configuration section name must contain a name.",
                nameof(sectionName));
        }

        string configuredMode = configuration[$"{normalizedSectionName}:StorageMode"]?.Trim()
            ?? nameof(LedgerSimulatorProjectionStorageMode.InMemory);
        if (!Enum.TryParse(configuredMode, ignoreCase: true, out LedgerSimulatorProjectionStorageMode storageMode)
            || !Enum.IsDefined(storageMode))
        {
            throw new InvalidOperationException(
                $"Unsupported simulator projection storage mode '{configuredMode}'. "
                + $"Supported modes are {nameof(LedgerSimulatorProjectionStorageMode.InMemory)} and {nameof(LedgerSimulatorProjectionStorageMode.PostgreSql)}.");
        }

        switch (storageMode)
        {
            case LedgerSimulatorProjectionStorageMode.InMemory:
                string databaseName = configuration[$"{normalizedSectionName}:InMemoryDatabaseName"]?.Trim()
                    ?? DefaultInMemoryDatabaseName;
                if (string.IsNullOrWhiteSpace(databaseName))
                {
                    throw new InvalidOperationException("The simulator in-memory database name cannot be empty.");
                }

                string projectionDatabaseName = $"{databaseName}-projections";
                services.AddSingleton(new LedgerSimulatorProjectionPersistenceSelection(storageMode, projectionDatabaseName));
                return services.AddLedgerSimulatorInMemoryProjectionPersistence(projectionDatabaseName);

            case LedgerSimulatorProjectionStorageMode.PostgreSql:
                string postgreSqlSectionName = $"{normalizedSectionName}:PostgreSql";
                if (string.IsNullOrWhiteSpace(configuration[$"{postgreSqlSectionName}:ConnectionString"]))
                {
                    throw new InvalidOperationException(
                        $"{postgreSqlSectionName}:ConnectionString is required when simulator projection storage mode is PostgreSql.");
                }

                services.AddSingleton(new LedgerSimulatorProjectionPersistenceSelection(storageMode, null));
                return services.AddLedgerSimulatorPostgreSqlProjectionPersistence(configuration, postgreSqlSectionName);

            default:
                throw new InvalidOperationException($"Unsupported simulator projection storage mode: {storageMode}.");
        }
    }

    public static IServiceCollection AddLedgerSimulatorInMemoryProjectionPersistence(
        this IServiceCollection services,
        string? databaseName = null,
        InMemoryDatabaseRoot? databaseRoot = null)
    {
        string resolvedDatabaseName = string.IsNullOrWhiteSpace(databaseName)
            ? DefaultInMemoryDatabaseName
            : databaseName.Trim();

        services.AddSingleton(databaseRoot ?? new InMemoryDatabaseRoot());
        services.AddDbContextFactory<LedgerSimulatorProjectionDbContext>((serviceProvider, optionsBuilder) =>
        {
            optionsBuilder.UseInMemoryDatabase(
                resolvedDatabaseName,
                serviceProvider.GetRequiredService<InMemoryDatabaseRoot>());
        });
        AddGraphServices(services);

        return services;
    }

    public static IServiceCollection AddLedgerSimulatorPostgreSqlProjectionPersistence(
        this IServiceCollection services,
        string connectionString,
        string? migrationsAssembly = null,
        Action<LedgerSimulatorPostgreSqlProjectionOptions>? configure = null)
    {
        services.AddOptions<LedgerSimulatorPostgreSqlProjectionOptions>()
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

        services.AddDbContextFactory<LedgerSimulatorProjectionDbContext>((serviceProvider, optionsBuilder) =>
        {
            LedgerSimulatorPostgreSqlProjectionOptions options = serviceProvider.GetRequiredService<IOptions<LedgerSimulatorPostgreSqlProjectionOptions>>().Value;
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

    public static IServiceCollection AddLedgerSimulatorPostgreSqlProjectionPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "LedgerSimulator:PostgreSql")
    {
        services.AddOptions<LedgerSimulatorPostgreSqlProjectionOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "ConnectionString is required.")
            .Validate(options => options.CommandTimeoutSeconds is null or > 0, "CommandTimeoutSeconds must be positive when configured.")
            .ValidateOnStart();

        services.AddDbContextFactory<LedgerSimulatorProjectionDbContext>((serviceProvider, optionsBuilder) =>
        {
            LedgerSimulatorPostgreSqlProjectionOptions options = serviceProvider.GetRequiredService<IOptions<LedgerSimulatorPostgreSqlProjectionOptions>>().Value;
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

    private static void AddGraphServices(IServiceCollection services)
    {
        services.TryAddScoped<ILedgerSimulatorGraphProjectionWriter, LedgerSimulatorGraphProjectionWriter>();
        services.TryAddScoped<ILedgerSimulatorGraphQueryService, LedgerSimulatorGraphQueryService>();
    }
}
