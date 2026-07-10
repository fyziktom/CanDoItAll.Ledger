using System.ComponentModel.DataAnnotations;
using CanDoItAll.Economy.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Economy.Persistence.PostgreSql;

public sealed class EconomyPostgreSqlOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string? MigrationsAssembly { get; set; }

    public int? CommandTimeoutSeconds { get; set; } = 30;

    public bool EnableRetryOnFailure { get; set; }

    public int MaxRetryCount { get; set; } = 3;

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}

public static class EconomyPostgreSqlServiceCollectionExtensions
{
    public static string DefaultMigrationsAssembly { get; } = typeof(EconomyPostgreSqlServiceCollectionExtensions).Assembly.GetName().Name!;

    public static IServiceCollection AddEconomyPostgreSqlPersistence(
        this IServiceCollection services,
        string connectionString,
        string? migrationsAssembly = null,
        Action<EconomyPostgreSqlOptions>? configure = null)
    {
        services.AddOptions<EconomyPostgreSqlOptions>()
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

        AddEconomyPostgreSqlServices(services);
        return services;
    }

    public static IServiceCollection AddEconomyPostgreSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Economy:PostgreSql")
    {
        services.AddOptions<EconomyPostgreSqlOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "ConnectionString is required.")
            .ValidateOnStart();

        AddEconomyPostgreSqlServices(services);
        return services;
    }

    private static void AddEconomyPostgreSqlServices(IServiceCollection services)
    {
        services.PostConfigure<EconomyPostgreSqlOptions>(options =>
            options.MigrationsAssembly = string.IsNullOrWhiteSpace(options.MigrationsAssembly)
                ? DefaultMigrationsAssembly
                : options.MigrationsAssembly);
        services.AddDbContextFactory<EconomyDbContext>((serviceProvider, optionsBuilder) =>
        {
            EconomyPostgreSqlOptions options = serviceProvider.GetRequiredService<IOptions<EconomyPostgreSqlOptions>>().Value;
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

        services.AddEconomyEfCoreStores();
        services.Replace(ServiceDescriptor.Scoped<IEconomySchemaMigrator, PostgreSqlEconomySchemaMigrator>());
    }
}
