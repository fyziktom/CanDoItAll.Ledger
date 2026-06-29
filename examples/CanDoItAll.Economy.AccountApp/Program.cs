using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.AccountApp;
using CanDoItAll.Economy.AccountApp.Services;
using CanDoItAll.Economy.Components.Services;
using CanDoItAll.Economy.Cryptography.Browser;
using CanDoItAll.Economy.Sdk;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var accountAppOptions = builder.Configuration.GetSection("AccountApp").Get<AccountAppOptions>() ?? new AccountAppOptions();
builder.Services.AddSingleton(accountAppOptions);
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllBrowserCrypto();
builder.Services.AddEconomySdkWithBrowserNodeDriver(
    accountAppOptions.NodeBaseUri,
    options => options.ApiBasePath = accountAppOptions.ApiBasePath);
builder.Services.AddScoped(_ => new WasmAccountWorkspaceOptions
{
    LedgerId = accountAppOptions.LedgerId,
    ScenarioId = accountAppOptions.ScenarioId,
    AccountName = accountAppOptions.DefaultAccountName,
    ConnectionLabel = "Browser API",
    NodeBaseUri = accountAppOptions.NodeBaseUri
});
builder.Services.AddEconomyWasmAccountWorkspaceAdapter();
builder.Services.AddScoped<AccountAppSession>();

await builder.Build().RunAsync();
