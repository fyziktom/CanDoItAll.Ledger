using CanDoItAll.Ledger.Node;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Ledger.Tests;

public sealed class LedgerNodeBoundaryConfigurationTests
{
    [Fact]
    public void AddLedgerNode_AppliesConfiguredRequestBodyLimitToKestrel()
    {
        const long expectedLimit = 65_537;
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddLedgerNode(configuration, options => options.MaxRequestBodyBytes = expectedLimit);
        using ServiceProvider provider = services.BuildServiceProvider();

        KestrelServerOptions kestrel = provider
            .GetRequiredService<IOptions<KestrelServerOptions>>()
            .Value;
        Assert.Equal(expectedLimit, kestrel.Limits.MaxRequestBodySize);
    }
}
