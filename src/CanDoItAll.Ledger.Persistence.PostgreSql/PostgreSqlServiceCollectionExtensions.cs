using System.ComponentModel.DataAnnotations;
using CanDoItAll.Ledger.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Ledger.Persistence.PostgreSql;

public sealed class LedgerPostgreSqlOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string? MigrationsAssembly { get; set; }

    public int? CommandTimeoutSeconds { get; set; } = 30;

    public bool EnableRetryOnFailure { get; set; }

    public int MaxRetryCount { get; set; } = 3;

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}

public static class LedgerPostgreSqlServiceCollectionExtensions
{
    public static string DefaultMigrationsAssembly { get; } = typeof(LedgerPostgreSqlServiceCollectionExtensions).Assembly.GetName().Name!;

    public static IServiceCollection AddLedgerPostgreSqlPersistence(
        this IServiceCollection services,
        string connectionString,
        string? migrationsAssembly = null,
        Action<LedgerPostgreSqlOptions>? configure = null)
    {
        services.AddOptions<LedgerPostgreSqlOptions>()
            .Configure(options =>
            {
                options.ConnectionString = connectionString;
                options.MigrationsAssembly = string.IsNullOrWhiteSpace(migrationsAssembly)
                    ? DefaultMigrationsAssembly
                    : migrationsAssembly;
                configure?.Invoke(options);
            })
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "ConnectionString is required.")
            .Validate(options => options.CommandTimeoutSeconds is null or > 0, "CommandTimeoutSeconds must be positive when configured.")
            .Validate(options => options.MaxRetryCount >= 0, "MaxRetryCount must be non-negative.")
            .Validate(options => options.MaxRetryDelay >= TimeSpan.Zero, "MaxRetryDelay must be non-negative.")
            .ValidateOnStart();

        AddLedgerPostgreSqlServices(services);
        return services;
    }

    public static IServiceCollection AddLedgerPostgreSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Ledger:PostgreSql")
    {
        services.AddOptions<LedgerPostgreSqlOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "ConnectionString is required.")
            .ValidateOnStart();

        AddLedgerPostgreSqlServices(services);
        return services;
    }

    private static void AddLedgerPostgreSqlServices(IServiceCollection services)
    {
        services.PostConfigure<LedgerPostgreSqlOptions>(options =>
            options.MigrationsAssembly = string.IsNullOrWhiteSpace(options.MigrationsAssembly)
                ? DefaultMigrationsAssembly
                : options.MigrationsAssembly);
        services.AddDbContextFactory<LedgerDbContext>((serviceProvider, optionsBuilder) =>
        {
            LedgerPostgreSqlOptions options = serviceProvider.GetRequiredService<IOptions<LedgerPostgreSqlOptions>>().Value;
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
                    npgsql.EnableRetryOnFailure(options.MaxRetryCount, options.MaxRetryDelay, null);
                }
            });
        });

        services.AddLedgerEfCoreStores();
        services.Replace(ServiceDescriptor.Scoped<ILedgerSchemaMigrator, PostgreSqlLedgerSchemaMigrator>());
    }
}
