# SDK And Driver Integration

`CanDoItAll.Ledger.Sdk` is the primary .NET client for a running Ledger node.
It exposes the versioned API through typed contracts and can also compose the
higher-level account and business-object services.

The examples below assume the local Node from the
[Node And AccountApp First Setup](../getting-started/node-and-account-app.md):

```text
http://127.0.0.1:8080/
```

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Sdk
```

All public Ledger packages target .NET 10.

## Direct Client

```csharp
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Sdk;

CancellationToken cancellationToken = CancellationToken.None;

using var node = new LedgerNodeLedgerClient(
    new Uri("http://127.0.0.1:8080/"),
    new LedgerNodeLedgerClientOptions
    {
        ApiBasePath = "api/v1"
    });

NodeHealthResponse health =
    await node.GetHealthAsync(cancellationToken);

NodeBootstrapInfo bootstrap =
    await node.GetBootstrapInfoAsync(cancellationToken);

var context = new LedgerAccountContext(
    new LedgerId(bootstrap.LedgerId),
    ScenarioId: null);

LedgerAccountBalance treasuryBalance =
    await node.GetBalanceAsync(
        new LedgerAddressQuery(
            context,
            new Address(bootstrap.AdminAddress)),
        cancellationToken);
```

`GetBootstrapInfoAsync` returns public bootstrap and configuration metadata. It
does not expose treasury or Ledger-admin private keys.

## Dependency-Injection Client

```csharp
using CanDoItAll.Ledger.Sdk;

builder.Services.AddLedgerSdkWithNodeDriver(
    new Uri(builder.Configuration["Ledger:NodeBaseUri"]
        ?? throw new InvalidOperationException("Node URI is missing.")),
    options =>
    {
        options.ApiBasePath = "api/v1";
        options.ApiKeyHeaderName = "X-CDA-Ledger-Key";
        options.ApiKey = builder.Configuration["Ledger:ApiKey"];
    });
```

Resolve `LedgerNodeLedgerClient`, `ILedgerGateway`, `ILedgerReadClient`, or the
higher-level `LedgerClient` from the application scope.

Choose the registration that matches the host:

| Registration | Use |
| --- | --- |
| `AddLedgerSdk()` | SDK primitives and `LedgerClient`; the host must add a gateway and any account layer it needs |
| `AddLedgerSdkWithBusinessObjectLayer()` | Higher-level accounts with an in-process gateway by default; earlier gateway registrations are honored |
| `AddLedgerSdkWithNodeDriver(...)` | Server, desktop, worker, CLI, or trusted tool using HTTP |
| `AddLedgerSdkWithBrowserNodeDriver(...)` | Browser account signing and node access |
| `AddLedgerSdkWithGateway<TGateway>()` | Custom transport or embedded gateway |

## Authentication

```csharp
var options = new LedgerNodeLedgerClientOptions
{
    ApiBasePath = "api/v1",
    ApiKeyHeaderName = "X-CDA-Ledger-Key",
    ApiKey = configuration["Ledger:ApiKey"],
    BearerToken = configuration["Ledger:BearerToken"]
};
```

When both are present, the bearer token takes precedence. Keep both values out
of source control, logs, exception text, and package artifacts.

Do not place shared API keys or bearer tokens in public WebAssembly. A browser
user can read its downloaded configuration and memory. Use an authenticated
same-origin server/proxy boundary for privileged production access.

## Read Operations

The typed client includes:

- liveness and readiness;
- Node bootstrap metadata;
- spendable outputs and calculated balances;
- account business objects and transaction history;
- ledger state and transaction details;
- filtered business-object and transaction queries.

Read authorization follows the Node's configured
`Ledger__Auth__Api__ReadMode`. Liveness can remain public while readiness and
Ledger queries require credentials.

## Write Operations

There are two important write styles:

- Treasury/admin helpers such as mint, transfer, permission grants, time
  advance, and reset. Secured modes require the Node's admin authorization;
  the loopback development profile deliberately leaves these helpers open.
- `SubmitTransactionAsync(LedgerTransaction, ...)`, which submits a transaction
  signed by the account that owns its inputs.

For signed account workflows, use `LedgerClient` and the BusinessObjects
account layer to load key material, select spendable outputs, build the
transaction, sign its inputs, and submit it. The
[AccountApp source](https://github.com/fyziktom/CanDoItAll.Ledger/tree/main/examples/CanDoItAll.Ledger.AccountApp)
is the maintained browser composition.

The following disposable development example creates a real P-256 account,
funds it through the open development mint helper, and submits a signed
self-payment:

```csharp
using System.Security.Cryptography;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Sdk;

CancellationToken cancellationToken = CancellationToken.None;
using var node = new LedgerNodeLedgerClient(
    new Uri("http://127.0.0.1:8080/"));

NodeBootstrapInfo bootstrap =
    await node.GetBootstrapInfoAsync(cancellationToken);
var context = new LedgerAccountContext(
    new LedgerId(bootstrap.LedgerId),
    ScenarioId: null);

ITransactionHasher hasher =
    new TransactionHasher(new DefaultTransactionCanonicalizer());
var transactionBuilder = new BusinessObjectTransactionBuilder(
    node,
    hasher,
    new SignaturePayloadBuilder(hasher),
    new AccountInputSigner(SignatureProviderRegistry.CreateDefault()),
    new SystemClock());
var ledger = new LedgerClient(node, node, transactionBuilder);

(byte[] publicKey, byte[] privateKey) =
    EcdsaP256KeyMaterial.CreateKeyPair();
try
{
    var key = new LedgerAccountKeyEnvelope(
        SignatureAlgorithmId.EcdsaP256Sha256,
        $"driver-{Guid.NewGuid():N}",
        publicKey,
        privateKey);
    BusinessObjectAccount account =
        await ledger.LoadAccountAsync(context, key, cancellationToken);

    await node.MintAsync(
        new MintTransactionRequest(
            Address: account.Address.Value,
            MainAmount: 25m,
            TokenId: null,
            TokenAmount: null,
            DataItemType: null,
            DataJson: null,
            ExternalRef: "driver-first-funding",
            LegacyBusinessObjectCreationMarker: null,
            ScenarioId: null),
        cancellationToken);

    LedgerSubmitResult payment = await account.SendPaymentAsync(
        new PaymentRequest(account.Address, Amount.FromInt64(1)),
        cancellationToken);

    if (!payment.Accepted)
    {
        throw new InvalidOperationException(
            string.Join("; ", payment.Errors));
    }
}
finally
{
    CryptographicOperations.ZeroMemory(privateKey);
}
```

This intentionally keeps the key only in memory. A real driver must load key
material from an encrypted vault or approved secret/custody service, retain a
recoverable backup, and keep the mint helper disabled outside development.

Always inspect `LedgerSubmitResult.Accepted` and its validation errors.
Transport-level HTTP success is not proof that the Ledger accepted a
transaction.

## Error Handling

```csharp
try
{
    NodeHealthResponse readiness =
        await node.GetReadinessAsync(cancellationToken);
}
catch (LedgerNodeRequestException exception)
{
    Console.Error.WriteLine(
        $"Node returned {(int)exception.StatusCode}: " +
        exception.ProblemDetail);
}
```

`LedgerNodeRequestException` preserves the HTTP status, reason, response body,
and parsed Problem Details fields. Avoid logging a body if the selected
endpoint can echo sensitive input.

Readiness can also return HTTP 200 with a degraded result. Inspect
`NodeHealthResponse.Status`, its verification status, and reported issues
before admitting traffic; catching transport exceptions alone is not a
readiness policy.

## Raw HTTP Drivers

Non-.NET drivers can use the same stable versioned routes:

| Method and path | Purpose |
| --- | --- |
| `GET /api/v1/health` | Process liveness; deep Ledger verification is not run |
| `GET /api/v1/health/readiness` | Dependency and readiness verification |
| `GET /api/v1/node/bootstrap` | Non-secret bootstrap and security metadata; read authorization applies |
| `POST /api/v1/ledger/transactions` | Submit a signed transaction |
| `GET /api/v1/ledger/{ledgerId}/accounts/{address}/spendable-outputs` | Account UTXOs |
| `GET /api/v1/ledger/{ledgerId}/accounts/{address}/business-objects` | Current holdings |
| `GET /api/v1/ledger/{ledgerId}/accounts/{address}/transactions` | Account history |
| `GET /api/v1/ledger/{ledgerId}/state` | Current Ledger state |

Use the SDK contracts and serialized tests as the reference for JSON shape.
Prefer `/api/v1`; the unversioned `/api` routes are compatibility paths and are
disabled by the production security profile.

## Browser And CORS Topology

The Compose AccountApp avoids CORS by calling its own origin and proxying
`/api/` to the internal Node service. A separately hosted browser application
must either:

- use a trusted same-origin proxy; or
- configure the exact allowed browser origins on the Node.

CORS does not authenticate a caller. Node authorization must still protect the
selected read and write endpoints.
