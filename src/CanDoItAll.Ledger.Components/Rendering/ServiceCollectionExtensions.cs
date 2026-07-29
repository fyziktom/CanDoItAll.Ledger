using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Ledger.Components.Rendering;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessObjectComponentRenderers(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectRendererProvider, SettingsBusinessObjectRendererProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectRendererProvider, RuleBusinessObjectRendererProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectRendererProvider, CommerceBusinessObjectRendererProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectRendererProvider, TypedFamilyBusinessObjectRendererProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectRendererProvider, MediaBusinessObjectRendererProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectRendererProvider, GenericBusinessObjectRendererProvider>());
        services.TryAddScoped<IBusinessObjectRendererRegistry, BusinessObjectRendererRegistry>();
        return services;
    }

    public static IServiceCollection AddBusinessObjectRendererProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IBusinessObjectRendererProvider
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBusinessObjectRendererProvider, TProvider>());
        return services;
    }
}
