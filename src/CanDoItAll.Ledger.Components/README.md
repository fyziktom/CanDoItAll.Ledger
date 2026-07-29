# CanDoItAll.Ledger.Components

Reusable Blazor user interfaces for Ledger accounts, transactions, business
objects, analytics, and node operations.

Use this package to assemble account workspaces, transaction composers and
inspectors, business-object galleries and editors, balance charts, security
panels, and operator views. Components render and collect user intent; the
server or configured adapter remains responsible for authorization,
validation, signing, and persistence.

## Requirements

- .NET 10
- Blazor Web App or Blazor WebAssembly host

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Components
```

## Base Setup

```csharp
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.Components.Rendering;

builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddBusinessObjectComponentRenderers();
```

Load the required styles in the document head:

```html
<link rel="stylesheet"
      href="_content/CanDoItAll.Components.BaseLib/css/material-icons.css" />
<link rel="stylesheet"
      href="_content/CanDoItAll.Components.BaseLib/css/output.css" />
<link rel="stylesheet"
      href="_content/CanDoItAll.Ledger.Components/css/ledger-components.css" />
```

A small presentation-only component can then be used directly:

```razor
@using CanDoItAll.Ledger.Components

<LedgerCodeBlock Text="@ReceiptJson" />

@code {
    private const string ReceiptJson = """{"accepted":true}""";
}
```

## Optional Features

- Charts: call `AddCanDoItAllCharts()` and render `<ChartsHeadAssets />`.
- Mermaid diagrams: call `AddCanDoItAllMermaid()` and render
  `<MermaidHeadAssets />`.
- QR workflows: call `AddCanDoItAllQrCode()` and mount one `<DialogHost />`.
- A full shared shell normally mounts one `<ThemeHost>`, `<DialogHost>`,
  `<Notification />`, and `<Tooltip />`.
- Register custom business-object renderers with
  `AddBusinessObjectRendererProvider<TProvider>()`.

Do not hand-load shared library JavaScript. Use the asset components supplied
by the corresponding `CanDoItAll.Components.*` package.

## Account Workspace Adapters

A WebAssembly account workspace combines browser cryptography, the browser
node driver, workspace options, and the adapter:

```powershell
dotnet add package CanDoItAll.Ledger.Cryptography.Browser
dotnet add package CanDoItAll.Ledger.Sdk
```

```csharp
using CanDoItAll.Ledger.Components.Services;
using CanDoItAll.Ledger.Cryptography.Browser;
using CanDoItAll.Ledger.Sdk;

var nodeBaseUri = new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddCanDoItAllBrowserCrypto();
builder.Services.AddLedgerSdkWithBrowserNodeDriver(
    nodeBaseUri,
    options => options.ApiBasePath = "api/v1");
builder.Services.AddScoped(_ => new WasmAccountWorkspaceOptions
{
    LedgerId = "default",
    AccountName = "Expense account",
    NodeBaseUri = nodeBaseUri
});
builder.Services.AddLedgerWasmAccountWorkspaceAdapter();
```

This setup assumes the host serves the app and forwards `/api/` to the node,
as the AccountApp container does. Direct cross-origin access requires an exact
node CORS origin and is suitable only for node operations that do not require
browser-held API keys, bearer tokens, or operator cookies. Use a reviewed
same-origin server or reverse proxy for protected deployments.

The bundled SSR registration adds both the development and node demo adapters,
so the self-contained component catalog registers both backing sessions:

```csharp
builder.Services.AddLedgerDevelopmentWorkspaceSession();
builder.Services.AddLedgerNodeWorkspaceSession(options =>
{
    options.NodeBaseUri =
        builder.Configuration["LedgerDemo:NodeBaseUri"]
        ?? "http://127.0.0.1:8080/";
    options.ApiBasePath =
        builder.Configuration["LedgerDemo:ApiBasePath"]
        ?? "api/v1";
    options.ApiKey = builder.Configuration["LedgerDemo:ApiKey"];
    options.BearerToken = builder.Configuration["LedgerDemo:BearerToken"];
    options.TestKeyRootPath = Path.Combine(
        builder.Environment.ContentRootPath,
        "artifacts",
        "demo-keys");
    options.KeySetName = "component-catalog";
    options.DefaultAccount = "demo-primary";
    options.PeerAccount = "demo-peer";
    options.AutoSeedDemoAccounts = false;
});
builder.Services.AddLedgerSsrAccountWorkspaceAdapters();
```

The built-in development and node sessions and their adapters are demo
infrastructure, not production custody boundaries. The node session reads local
account-key files and can auto-seed accounts. If you evaluate it, explicitly
configure its node, authentication, key-root, key-set, account, and auto-seed
options; register the required business-object casting services; keep raw key
files under ignored disposable artifacts; and disable auto-seeding outside
demos. Production SSR hosts should provide an application-owned
`ILedgerAccountWorkspaceAdapter` backed by reviewed key-custody and
authorization services, or connect components through their explicit
callbacks and services.

Full account and administration components require real adapters, services, or
callbacks. The
[AccountApp](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/examples/CanDoItAll.Ledger.AccountApp)
and
[component catalog](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/examples/CanDoItAll.Ledger.Components.Demo)
show complete host composition.

## Security Notes

- UI visibility is not an authorization boundary; enforce every rule at the
  node or application service boundary.
- Never embed a static node API key, bearer token, or private signing key in a
  published WebAssembly application.
- Treat browser state as untrusted and validate all submitted transactions on
  the server.
- The full workspaces are optimized for application and desktop-sized layouts;
  test the routes and breakpoints used by your product.

See the
[Ledger documentation](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/docs)
and [CanDoItAll Components packages](https://www.nuget.org/profiles/fyziktom).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT License.
