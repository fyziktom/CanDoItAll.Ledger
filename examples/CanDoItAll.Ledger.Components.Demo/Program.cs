using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Components.Mermaid.Infrastructure;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Demo.Components;
using CanDoItAll.Ledger.Components.Demo.SampleDomain;
using CanDoItAll.Ledger.Components.Rendering;
using CanDoItAll.Ledger.Components.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllCharts();
builder.Services.AddCanDoItAllMermaid();
builder.Services.AddBusinessObjectComponentRenderers();
builder.Services.AddBusinessObjectIpfsStorage(
    new Uri(builder.Configuration["BusinessObjects:IpfsApiUrl"] ?? "http://127.0.0.1:5001", UriKind.Absolute),
    new Uri(builder.Configuration["BusinessObjects:IpfsGatewayUrl"] ?? "http://127.0.0.1:5093", UriKind.Absolute));
builder.Services.AddScoped<IBusinessObjectRendererProvider, PurchaseOrderRendererProvider>();
builder.Services.AddLedgerDevelopmentWorkspaceSession();
builder.Services.AddLedgerSsrAccountWorkspaceAdapters();
builder.Services.AddLedgerNodeWorkspaceSession(options =>
{
    options.NodeBaseUri = builder.Configuration["LedgerUi:NodeBaseUri"] ?? options.NodeBaseUri;
    options.ApiBasePath = builder.Configuration["LedgerUi:ApiBasePath"] ?? options.ApiBasePath;
    options.ApiKeyHeaderName = builder.Configuration["LedgerUi:ApiKeyHeaderName"] ?? options.ApiKeyHeaderName;
    options.ApiKey = builder.Configuration["LedgerUi:ApiKey"];
    options.BearerToken = builder.Configuration["LedgerUi:BearerToken"];
    options.TestKeyRootPath = builder.Configuration["LedgerUi:TestKeyRootPath"] ?? options.TestKeyRootPath;
    options.KeySetName = builder.Configuration["LedgerUi:KeySetName"] ?? options.KeySetName;
    options.LedgerId = builder.Configuration["LedgerUi:LedgerId"] ?? options.LedgerId;
    options.ScenarioId = builder.Configuration["LedgerUi:ScenarioId"];
    options.DefaultAccount = builder.Configuration["LedgerUi:DefaultAccount"] ?? options.DefaultAccount;
    options.PeerAccount = builder.Configuration["LedgerUi:PeerAccount"] ?? options.PeerAccount;
    if (bool.TryParse(builder.Configuration["LedgerUi:AutoSeedDemoAccounts"], out bool autoSeedDemoAccounts))
    {
        options.AutoSeedDemoAccounts = autoSeedDemoAccounts;
    }

    if (decimal.TryParse(builder.Configuration["LedgerUi:SeedMainAmount"], out decimal seedMainAmount))
    {
        options.SeedMainAmount = seedMainAmount;
    }

    if (decimal.TryParse(builder.Configuration["LedgerUi:SeedTokenSourceAmount"], out decimal seedTokenSourceAmount))
    {
        options.SeedTokenSourceAmount = seedTokenSourceAmount;
    }

    options.SeedTokenNamespace = builder.Configuration["LedgerUi:SeedTokenNamespace"] ?? options.SeedTokenNamespace;
    options.SeedTokenId = builder.Configuration["LedgerUi:SeedTokenId"] ?? options.SeedTokenId;
    options.SeedRuleNamespace = builder.Configuration["LedgerUi:SeedRuleNamespace"] ?? options.SeedRuleNamespace;
    if (decimal.TryParse(builder.Configuration["LedgerUi:SeedRulePermissionAmount"], out decimal seedRulePermissionAmount))
    {
        options.SeedRulePermissionAmount = seedRulePermissionAmount;
    }
});

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
