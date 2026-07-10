using CanDoItAll.Economy.BusinessObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Economy.Rules.NCalc;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNCalcAccountRuleExpressionDriver(this IServiceCollection services)
    {
        services.TryAddScoped<IAccountRuleExpressionEvaluator, NCalcAccountRuleExpressionEvaluator>();
        return services;
    }
}
