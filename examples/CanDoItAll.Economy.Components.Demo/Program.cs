using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Components.Mermaid.Infrastructure;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Demo.Components;
using CanDoItAll.Economy.Components.Demo.SampleDomain;
using CanDoItAll.Economy.Components.Rendering;
using CanDoItAll.Economy.Components.Services;

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
builder.Services.AddEconomyDevelopmentWorkspaceSession();
builder.Services.AddEconomyNodeWorkspaceSession(options =>
{
    options.NodeBaseUri = builder.Configuration["EconomyUi:NodeBaseUri"] ?? options.NodeBaseUri;
    options.ApiBasePath = builder.Configuration["EconomyUi:ApiBasePath"] ?? options.ApiBasePath;
    options.ApiKeyHeaderName = builder.Configuration["EconomyUi:ApiKeyHeaderName"] ?? options.ApiKeyHeaderName;
    options.ApiKey = builder.Configuration["EconomyUi:ApiKey"];
    options.BearerToken = builder.Configuration["EconomyUi:BearerToken"];
    options.TestKeyRootPath = builder.Configuration["EconomyUi:TestKeyRootPath"] ?? options.TestKeyRootPath;
    options.KeySetName = builder.Configuration["EconomyUi:KeySetName"] ?? options.KeySetName;
    options.LedgerId = builder.Configuration["EconomyUi:LedgerId"] ?? options.LedgerId;
    options.ScenarioId = builder.Configuration["EconomyUi:ScenarioId"];
    options.DefaultAccount = builder.Configuration["EconomyUi:DefaultAccount"] ?? options.DefaultAccount;
    options.PeerAccount = builder.Configuration["EconomyUi:PeerAccount"] ?? options.PeerAccount;
    if (bool.TryParse(builder.Configuration["EconomyUi:AutoSeedDemoAccounts"], out bool autoSeedDemoAccounts))
    {
        options.AutoSeedDemoAccounts = autoSeedDemoAccounts;
    }

    if (decimal.TryParse(builder.Configuration["EconomyUi:SeedMainAmount"], out decimal seedMainAmount))
    {
        options.SeedMainAmount = seedMainAmount;
    }

    if (decimal.TryParse(builder.Configuration["EconomyUi:SeedTokenSourceAmount"], out decimal seedTokenSourceAmount))
    {
        options.SeedTokenSourceAmount = seedTokenSourceAmount;
    }

    options.SeedTokenNamespace = builder.Configuration["EconomyUi:SeedTokenNamespace"] ?? options.SeedTokenNamespace;
    options.SeedTokenId = builder.Configuration["EconomyUi:SeedTokenId"] ?? options.SeedTokenId;
    options.SeedRuleNamespace = builder.Configuration["EconomyUi:SeedRuleNamespace"] ?? options.SeedRuleNamespace;
    if (decimal.TryParse(builder.Configuration["EconomyUi:SeedRulePermissionAmount"], out decimal seedRulePermissionAmount))
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
