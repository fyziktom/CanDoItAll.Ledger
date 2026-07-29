using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

string? dcpProxyPath = TryConfigureDcpProxy();
IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);
bool usePostgreSql = bool.TryParse(builder.Configuration["Ledger:UsePostgreSql"], out bool parsedUsePostgreSql)
    && parsedUsePostgreSql;

IResourceBuilder<ProjectResource> ledgerNode = builder.AddProject<Projects.CanDoItAll_Ledger_Node>("ledger-node")
    .WithEnvironment("Ledger__Persistence__Provider", "InMemory")
    .WithEnvironment("Ledger__InMemory__DatabaseName", "aspire-ledger-node");

if (usePostgreSql)
{
    IResourceBuilder<ParameterResource> postgresPassword = builder.AddParameter(
        "ledger-postgres-password",
        secret: true);

    IResourceBuilder<PostgresServerResource> postgres = builder.AddPostgres(
            "ledger-postgres",
            password: postgresPassword)
        .WithDataVolume();

    IResourceBuilder<PostgresDatabaseResource> ledgerDatabase = postgres.AddDatabase(
        "candoitall-ledger-db",
        "candoitall_ledger");

    ledgerNode = ledgerNode
        .WithReference(ledgerDatabase)
        .WaitFor(ledgerDatabase)
        .WithEnvironment("Ledger__Persistence__Provider", "PostgreSql")
        .WithEnvironment("Ledger__PostgreSql__ConnectionString", ledgerDatabase);
}

IResourceBuilder<ProjectResource> ipfsNode = builder.AddProject<Projects.CanDoItAll_IPFS_Engine>("ipfs-node")
    .WithEnvironment("IPFS_NODE_API_URL", "http://127.0.0.1:5001")
    .WithEnvironment("IpfsHttpHost__Passphrase", "candoitall-ledger-dev")
    .WithEnvironment("IpfsHttpHost__RequireAuthentication", "false")
    .WithEnvironment("IpfsHttpHost__AllowAnyOrigin", "true")
    .WithHttpEndpoint(port: 5001, targetPort: 5001, name: "ipfs-api", isProxied: false);

IResourceBuilder<ProjectResource> ipfsGateway = builder.AddProject<Projects.CanDoItAll_IPFS_NodeControl>("ipfs-gateway")
    .WithEnvironment("ASPNETCORE_URLS", "http://127.0.0.1:5093")
    .WithEnvironment("NodeSettingsDefaults__BaseUrl", "http://127.0.0.1:5001/")
    .WithEnvironment("GatewayPublishing__Mode", "Preview")
    .WithHttpEndpoint(port: 5093, targetPort: 5093, name: "ipfs-gateway", isProxied: false)
    .WaitFor(ipfsNode);

builder.AddProject<Projects.CanDoItAll_Ledger_Components_Demo>("bo-demo")
    .WithEnvironment("BusinessObjects__IpfsApiUrl", "http://127.0.0.1:5001")
    .WithEnvironment("BusinessObjects__IpfsGatewayUrl", "http://127.0.0.1:5093")
    .WaitFor(ledgerNode)
    .WaitFor(ipfsNode)
    .WaitFor(ipfsGateway);

WriteStartupBanner(builder.Configuration, usePostgreSql, dcpProxyPath);
await builder.Build().RunAsync();

static void WriteStartupBanner(IConfiguration configuration, bool usePostgreSql, string? dcpProxyPath)
{
    string dashboardUrl = ResolveDashboardUrl(configuration);
    string persistenceMode = usePostgreSql ? "PostgreSQL" : "EF Core InMemory";
    string dcpCertificateMode = IsDcpDeveloperCertificateEnabled(configuration)
        ? "trusted ASP.NET developer certificate"
        : "Aspire ephemeral certificate";

    Console.WriteLine("CanDoItAll Ledger Aspire AppHost starting.");
    Console.WriteLine($"Dashboard: {dashboardUrl}");
    Console.WriteLine($"Ledger persistence: {persistenceMode}");
    Console.WriteLine($"DCP TLS: {dcpCertificateMode}");
    Console.WriteLine($"DCP proxy: {dcpProxyPath ?? "default Aspire DCP"}");
    Console.WriteLine("Resources: bo-demo, ledger-node, ipfs-node, ipfs-gateway.");
    Console.WriteLine("Use the Aspire dashboard Resources page to open service endpoints.");
}

static string ResolveDashboardUrl(IConfiguration configuration)
{
    string? urls = configuration["ASPNETCORE_URLS"];
    string? firstUrl = urls?
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault();

    return firstUrl ?? "(dashboard URL is provided by the active launch profile)";
}

static bool IsDcpDeveloperCertificateEnabled(IConfiguration configuration)
{
    return bool.TryParse(configuration["ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE"], out bool enabled) && enabled;
}

static string? TryConfigureDcpProxy()
{
    string dcpProxyPath = Path.Combine(AppContext.BaseDirectory, "dcp-proxy");
    string dcpProxyExecutable = Path.Combine(dcpProxyPath, OperatingSystem.IsWindows() ? "dcp.exe" : "dcp");

    if (!File.Exists(dcpProxyExecutable))
    {
        return null;
    }

    Environment.SetEnvironmentVariable("ASPIRE_DCP_PATH", dcpProxyPath);
    return dcpProxyPath;
}
