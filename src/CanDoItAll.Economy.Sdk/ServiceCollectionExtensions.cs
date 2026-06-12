using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Economy.Sdk;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEconomySdk(this IServiceCollection services)
    {
        services.TryAddScoped<ITransactionCanonicalizer, DefaultTransactionCanonicalizer>();
        services.TryAddScoped<ITransactionHasher, TransactionHasher>();
        services.TryAddScoped<ISignaturePayloadBuilder, SignaturePayloadBuilder>();
        services.TryAddSingleton<ISignatureProviderRegistry>(_ => SignatureProviderRegistry.CreateDefault());
        services.TryAddSingleton<ITimeProvider, SdkSystemClock>();
        services.TryAddScoped<EconomyClient>();
        return services;
    }

    public static IServiceCollection AddEconomySdkWithBusinessObjectLayer(this IServiceCollection services)
    {
        services.AddBusinessObjectLedgerLayer();
        services.AddEconomySdk();
        return services;
    }

    public static IServiceCollection AddEconomySdkWithNodeDriver(
        this IServiceCollection services,
        Uri nodeBaseUri)
        => services.AddEconomySdkWithNodeDriver(nodeBaseUri, configure: null);

    public static IServiceCollection AddEconomySdkWithNodeDriver(
        this IServiceCollection services,
        Uri nodeBaseUri,
        Action<EconomyNodeLedgerClientOptions>? configure)
    {
        var options = new EconomyNodeLedgerClientOptions();
        configure?.Invoke(options);
        services.AddBusinessObjectAccountLayer();
        services.AddEconomySdk();
        services.RemoveAll<ILedgerGateway>();
        services.RemoveAll<ILedgerReadClient>();
        services.TryAddScoped(_ => new EconomyNodeLedgerClient(nodeBaseUri, options));
        services.AddScoped<ILedgerGateway>(serviceProvider => serviceProvider.GetRequiredService<EconomyNodeLedgerClient>());
        services.AddScoped<ILedgerReadClient>(serviceProvider => serviceProvider.GetRequiredService<EconomyNodeLedgerClient>());
        return services;
    }

    public static IServiceCollection AddEconomySdkWithGateway<TGateway>(this IServiceCollection services)
        where TGateway : class, ILedgerGateway, ILedgerReadClient
    {
        services.AddBusinessObjectAccountLayer();
        services.AddEconomySdk();
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
