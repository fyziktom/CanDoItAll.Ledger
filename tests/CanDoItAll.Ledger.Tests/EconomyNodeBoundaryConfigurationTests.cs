using CanDoItAll.Economy.Node;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomyNodeBoundaryConfigurationTests
{
    [Fact]
    public void AddEconomyNode_AppliesConfiguredRequestBodyLimitToKestrel()
    {
        const long expectedLimit = 65_537;
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddEconomyNode(configuration, options => options.MaxRequestBodyBytes = expectedLimit);
        using ServiceProvider provider = services.BuildServiceProvider();

        KestrelServerOptions kestrel = provider
            .GetRequiredService<IOptions<KestrelServerOptions>>()
            .Value;
        Assert.Equal(expectedLimit, kestrel.Limits.MaxRequestBodySize);
    }
}
