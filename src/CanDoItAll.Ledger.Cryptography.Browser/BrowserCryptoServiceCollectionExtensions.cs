using CanDoItAll.Ledger.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Ledger.Cryptography.Browser;

public static class BrowserCryptoServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllBrowserCrypto(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<BrowserLedgerCryptoDriver>();
        services.AddScoped<ILedgerCryptoDriver>(provider => provider.GetRequiredService<BrowserLedgerCryptoDriver>());
        services.AddScoped<IAccountVaultStore, BrowserIndexedDbAccountVaultStore>();
        services.AddScoped<AccountVaultService>();
        return services;
    }
}
