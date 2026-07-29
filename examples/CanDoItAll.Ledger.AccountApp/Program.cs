using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.QRCode;
using CanDoItAll.Ledger.AccountApp;
using CanDoItAll.Ledger.AccountApp.Services;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Services;
using CanDoItAll.Ledger.Cryptography.Browser;
using CanDoItAll.Ledger.Sdk;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var accountAppOptions = builder.Configuration.GetSection("AccountApp").Get<AccountAppOptions>() ?? new AccountAppOptions();
builder.Services.AddSingleton(accountAppOptions);
builder.Services.AddSingleton<IBusinessObjectDataItemUriResolver>(
    accountAppOptions.IpfsContentEndpointUri is null
        ? new IpfsGatewayDataItemUriResolver(accountAppOptions.IpfsGatewayBaseUri)
        : new IpfsContentEndpointDataItemUriResolver(accountAppOptions.IpfsContentEndpointUri));
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllQrCode();
builder.Services.AddCanDoItAllBrowserCrypto();
builder.Services.AddLedgerSdkWithBrowserNodeDriver(
    accountAppOptions.NodeBaseUri,
    options => options.ApiBasePath = accountAppOptions.ApiBasePath);
if (accountAppOptions.EnableIpfsUploads && accountAppOptions.IpfsApiBaseUri is not null)
{
    builder.Services.AddScoped(_ => new HttpIpfsBusinessObjectDataItemUploadOptions(
        accountAppOptions.IpfsApiBaseUri,
        accountAppOptions.IpfsGatewayBaseUri.ToString()));
    builder.Services.AddScoped<IBusinessObjectDataStorageDriver>(provider =>
        new HttpIpfsBusinessObjectDataStorageDriver(
            new HttpClient { BaseAddress = accountAppOptions.IpfsApiBaseUri },
            provider.GetRequiredService<HttpIpfsBusinessObjectDataItemUploadOptions>()));
    builder.Services.AddScoped<IBusinessObjectDataItemUploadService>(provider =>
        new HttpIpfsBusinessObjectDataItemUploadService(
            new HttpClient { BaseAddress = accountAppOptions.IpfsApiBaseUri },
            provider.GetRequiredService<HttpIpfsBusinessObjectDataItemUploadOptions>()));
}

builder.Services.AddScoped<IAccountWorkspaceCache, BrowserLocalStorageAccountWorkspaceCache>();
builder.Services.AddScoped(_ => new WasmAccountWorkspaceOptions
{
    LedgerId = accountAppOptions.LedgerId,
    ScenarioId = accountAppOptions.ScenarioId,
    AccountName = accountAppOptions.DefaultAccountName,
    ConnectionLabel = "Browser API",
    NodeBaseUri = accountAppOptions.NodeBaseUri
});
builder.Services.AddLedgerWasmAccountWorkspaceAdapter();
builder.Services.AddScoped<AccountAppSession>();

await builder.Build().RunAsync();
