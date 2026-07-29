# CanDoItAll.Ledger.Sdk

Typed node client and dependency-injection integration for CanDoItAll Ledger
account and transaction workflows.

Use this package to connect .NET applications, services, tools, and drivers to
a running Ledger node. It provides health and bootstrap contracts, account and
transaction queries, transaction submission, treasury/admin request models,
business-object account composition, and configurable API-key or JWT
authentication.

## Requirements

- .NET 10
- A reachable CanDoItAll Ledger node

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Sdk
```

## Connect Directly

```csharp
using CanDoItAll.Ledger.Sdk;

using var node = new LedgerNodeLedgerClient(
    new Uri("http://127.0.0.1:8080/"),
    new LedgerNodeLedgerClientOptions
    {
        ApiBasePath = "api/v1",
        ApiKey = Environment.GetEnvironmentVariable("LEDGER_API_KEY")
            ?? throw new InvalidOperationException(
                "Set LEDGER_API_KEY for protected node reads.")
    });

NodeHealthResponse health =
    await node.GetHealthAsync(CancellationToken.None);

NodeBootstrapInfo bootstrap =
    await node.GetBootstrapInfoAsync(CancellationToken.None);
```

The default API base path is `api/v1`.

## Register With Dependency Injection

```csharp
using CanDoItAll.Ledger.Sdk;

builder.Services.AddLedgerSdkWithNodeDriver(
    new Uri(builder.Configuration["Ledger:NodeBaseUri"]
        ?? throw new InvalidOperationException("Node URI is missing.")),
    options =>
    {
        options.ApiBasePath = "api/v1";
        options.BearerToken =
            builder.Configuration["Ledger:BearerToken"];
    });
```

Registration choices:

- `AddLedgerSdk()` adds client-side canonicalization, hashing, rules, clock, and
  `LedgerClient`, but does not choose a gateway.
- `AddLedgerSdkWithBusinessObjectLayer()` adds the account layer and defaults
  to the in-process gateway unless the host registered `ILedgerGateway` and
  `ILedgerReadClient` first.
- `AddLedgerSdkWithNodeDriver(...)` selects the HTTP node gateway and read
  client.
- `AddLedgerSdkWithBrowserNodeDriver(...)` adds browser account signing and
  node access. Register Web Crypto and IndexedDB separately with
  `CanDoItAll.Ledger.Cryptography.Browser` and
  `AddCanDoItAllBrowserCrypto()`.
- `AddLedgerSdkWithGateway<TGateway>()` selects an application-owned gateway.

## Authentication And Results

`LedgerNodeLedgerClientOptions` supports `ApiBasePath`, `ApiKeyHeaderName`,
`ApiKey`, and `BearerToken`. A bearer token takes precedence when both token and
API key are configured.

- Liveness may be public while readiness, bootstrap, reads, or writes require
  node authorization.
- Never embed API keys or bearer tokens in published WebAssembly. Put privileged
  calls behind a server or trusted same-origin proxy.
- Catch `LedgerNodeRequestException` to inspect HTTP status and Problem Details
  returned by the node.
- Check `LedgerSubmitResult.Accepted` for submitted signed transactions; HTTP
  transport success alone is not ledger acceptance.

See the
[Node and AccountApp quickstart](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/getting-started/node-and-account-app.md),
[driver integration guide](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/development/sdk-and-drivers.md),
and [full source](https://github.com/fyziktom/CanDoItAll.Ledger).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT-derived license with website-link requirement.
