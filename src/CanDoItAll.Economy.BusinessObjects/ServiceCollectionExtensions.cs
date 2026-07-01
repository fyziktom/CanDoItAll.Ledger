using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;

namespace CanDoItAll.Economy.BusinessObjects;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessObjectLedgerLayer(this IServiceCollection services)
    {
        services.AddBusinessObjectAccountLayer();
        services.TryAddScoped<InProcessLedgerGateway>();
        services.TryAddScoped<ILedgerGateway>(provider => provider.GetRequiredService<InProcessLedgerGateway>());
        services.TryAddScoped<ILedgerReadClient>(provider => provider.GetRequiredService<InProcessLedgerGateway>());
        return services;
    }

    public static IServiceCollection AddBusinessObjectAccountLayer(this IServiceCollection services)
    {
        services.TryAddSingleton<ITimeProvider, SystemClock>();
        services.TryAddScoped<IEconomyCryptoDriver, DotNetEconomyCryptoDriver>();
        services.TryAddSingleton<IAccountVaultStore, InMemoryAccountVaultStore>();
        services.TryAddScoped<AccountVaultService>();
        services.TryAddScoped<IAccountInputSigner, AccountInputSigner>();
        services.TryAddScoped<IAccountTransactionSigner, AccountTransactionSigner>();
        services.TryAddScoped<IEconomyAccountHandler, EconomyAccountHandler>();
        services.TryAddScoped<IAccountBackupService, AccountBackupService>();
        services.TryAddScoped<IAccountSetupService, AccountSetupService>();
        services.TryAddScoped<BusinessObjectTransactionBuilder>();
        services.AddBusinessObjectCasting();
        return services;
    }

    public static IServiceCollection AddBusinessObjectBrowserAccountSigning(this IServiceCollection services)
    {
        services.TryAddScoped<IEconomyCryptoDriver, DotNetEconomyCryptoDriver>();
        services.Replace(ServiceDescriptor.Scoped<IAccountInputSigner, CryptoDriverAccountInputSigner>());
        services.TryAddScoped<IAccountTransactionSigner, AccountTransactionSigner>();
        return services;
    }

    public static IServiceCollection AddBusinessObjectCasting(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectCaster, AccountSettingsBusinessObjectCaster>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectCaster, CommerceBusinessObjectCaster>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectCaster, GenericTypedBusinessObjectCaster>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectCaster, ImageBusinessObjectCaster>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectCaster, PostBusinessObjectCaster>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectCaster, GenericBusinessObjectCaster>());
        services.TryAddScoped<IBusinessObjectCastRegistry, BusinessObjectCastRegistry>();
        return services;
    }

    public static IServiceCollection AddBusinessObjectCaster<TCaster>(this IServiceCollection services)
        where TCaster : class, IBusinessObjectCaster
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectCaster, TCaster>());
        return services;
    }

#if BUSINESS_OBJECT_IPFS
    public static IServiceCollection AddBusinessObjectIpfsStorage(
        this IServiceCollection services,
        Uri apiBaseUri,
        Uri gatewayBaseUri)
    {
        services.TryAddScoped(_ => new BusinessObjectIpfsStorageOptions(gatewayBaseUri, apiBaseUri));
        services.TryAddScoped(provider =>
        {
            var options = provider.GetRequiredService<BusinessObjectIpfsStorageOptions>();
            return new IpfsBusinessObjectStorageClient(new HttpClient { BaseAddress = options.ApiBaseUri });
        });
        services.TryAddScoped<IBusinessObjectDataStorageDriver, IpfsBusinessObjectDataStorageDriver>();
        services.TryAddScoped<IBusinessObjectDataItemUploadService, BusinessObjectDataItemUploadService>();
        return services;
    }
#endif
}
