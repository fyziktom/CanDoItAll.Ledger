using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Rules.NCalc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Ledger.Sdk;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerSdk(this IServiceCollection services)
    {
        services.AddNCalcAccountRuleExpressionDriver();
        services.TryAddScoped<ITransactionCanonicalizer, DefaultTransactionCanonicalizer>();
        services.TryAddScoped<ITransactionHasher, TransactionHasher>();
        services.TryAddScoped<ISignaturePayloadBuilder, SignaturePayloadBuilder>();
        services.TryAddSingleton<ISignatureProviderRegistry>(_ => SignatureProviderRegistry.CreateDefault());
        services.TryAddSingleton<ITimeProvider, SdkSystemClock>();
        services.TryAddScoped<LedgerClient>();
        return services;
    }

    public static IServiceCollection AddLedgerSdkWithBusinessObjectLayer(this IServiceCollection services)
    {
        services.AddBusinessObjectLedgerLayer();
        services.AddLedgerSdk();
        return services;
    }

    public static IServiceCollection AddLedgerSdkWithNodeDriver(
        this IServiceCollection services,
        Uri nodeBaseUri)
        => services.AddLedgerSdkWithNodeDriver(nodeBaseUri, configure: null);

    public static IServiceCollection AddLedgerSdkWithNodeDriver(
        this IServiceCollection services,
        Uri nodeBaseUri,
        Action<LedgerNodeLedgerClientOptions>? configure)
    {
        var options = new LedgerNodeLedgerClientOptions();
        configure?.Invoke(options);
        services.AddBusinessObjectAccountLayer();
        services.AddLedgerSdk();
        services.RemoveAll<ILedgerGateway>();
        services.RemoveAll<ILedgerReadClient>();
        services.TryAddScoped(_ => new LedgerNodeLedgerClient(nodeBaseUri, options));
        services.AddScoped<ILedgerGateway>(serviceProvider => serviceProvider.GetRequiredService<LedgerNodeLedgerClient>());
        services.AddScoped<ILedgerReadClient>(serviceProvider => serviceProvider.GetRequiredService<LedgerNodeLedgerClient>());
        return services;
    }

    public static IServiceCollection AddLedgerSdkWithBrowserNodeDriver(
        this IServiceCollection services,
        Uri nodeBaseUri)
        => services.AddLedgerSdkWithBrowserNodeDriver(nodeBaseUri, configure: null);

    public static IServiceCollection AddLedgerSdkWithBrowserNodeDriver(
        this IServiceCollection services,
        Uri nodeBaseUri,
        Action<LedgerBrowserNodeClientOptions>? configure)
    {
        var browserOptions = new LedgerBrowserNodeClientOptions();
        configure?.Invoke(browserOptions);
        browserOptions.Validate();

        services.AddLedgerSdkWithNodeDriver(
            nodeBaseUri,
            options =>
            {
                options.ApiBasePath = browserOptions.ApiBasePath;
            });
        services.AddBusinessObjectBrowserAccountSigning();
        return services;
    }

    public static IServiceCollection AddLedgerSdkWithGateway<TGateway>(this IServiceCollection services)
        where TGateway : class, ILedgerGateway, ILedgerReadClient
    {
        services.AddBusinessObjectAccountLayer();
        services.AddLedgerSdk();
        services.RemoveAll<ILedgerGateway>();
        services.RemoveAll<ILedgerReadClient>();
        services.TryAddScoped<TGateway>();
        services.AddScoped<ILedgerGateway>(serviceProvider => serviceProvider.GetRequiredService<TGateway>());
        services.AddScoped<ILedgerReadClient>(serviceProvider => serviceProvider.GetRequiredService<TGateway>());
        return services;
    }
}

internal sealed class SdkSystemClock : ITimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class LedgerBrowserNodeClientOptions
{
    public string ApiBasePath { get; set; } = "api/v1";

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiBasePath))
        {
            throw new ArgumentException("API base path is required.", nameof(ApiBasePath));
        }

        if (ApiBasePath.Contains('?', StringComparison.Ordinal) || ApiBasePath.Contains('#', StringComparison.Ordinal))
        {
            throw new ArgumentException("API base path cannot contain a query string or fragment.", nameof(ApiBasePath));
        }
    }
}
