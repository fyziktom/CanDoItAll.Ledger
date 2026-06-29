using CanDoItAll.Economy.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Economy.Cryptography.Browser;

public static class BrowserCryptoServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllBrowserCrypto(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<BrowserEconomyCryptoDriver>();
        services.AddScoped<IEconomyCryptoDriver>(provider => provider.GetRequiredService<BrowserEconomyCryptoDriver>());
        services.AddScoped<IAccountVaultStore, BrowserIndexedDbAccountVaultStore>();
        services.AddScoped<AccountVaultService>();
        return services;
    }
}
