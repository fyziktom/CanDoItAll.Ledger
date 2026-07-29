# SDK Quickstart

This page shows the smallest useful application shape: create a ledger driver, wrap it in `EconomyClient`, load an account, then read or submit ledger operations.

## 1. Reference the Code Projects

For source-based development in this repo, application code usually references:

- `CanDoItAll.Economy.Core`
- `CanDoItAll.Economy.Cryptography`
- `CanDoItAll.Economy.BusinessObjects`
- `CanDoItAll.Economy.Sdk`

When using a local simulation driver, also reference:

- `CanDoItAll.Economy.Ledger`
- `CanDoItAll.Economy.Storage.InMemory`

## 2. Create an Account Key

```csharp
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Sdk;

(byte[] publicKey, byte[] privateKey) = EcdsaP256KeyMaterial.CreateKeyPair();

var key = new EconomyAccountKeyEnvelope(
    SignatureAlgorithmId.EcdsaP256Sha256,
    keyId: "alice",
    publicKeyMaterial: publicKey,
    privateKeyMaterial: privateKey);
```

Persist keys in your own key store. The SDK can export and import `EconomyAccountKeyEnvelope`, but production applications should protect private key material outside ordinary data tables.

## 3. Connect to a Node

For DI-based application startup, use the SDK node-driver registration:

```csharp
using CanDoItAll.Economy.Sdk;

services.AddEconomySdkWithNodeDriver(new Uri("http://localhost:5000/"));
```

That registers `EconomyNodeLedgerClient` as both `ILedgerGateway` and `ILedgerReadClient`, plus default crypto/signing/time services and `EconomyClient`.

For a JWT-protected node:

```csharp
services.AddEconomySdkWithNodeDriver(
    new Uri("https://economy.example.com/"),
    options => options.BearerToken = configuration["Economy:Api:BearerToken"]);
```

The driver sends a bearer token before falling back to an API key. Local auth-off development still works by leaving both credentials empty.

### Blazor Component UI

Generic ledger applications can reference `CanDoItAll.Economy.Components` to compose account, transaction, business-object, analytics, and node-admin surfaces without taking a dependency on a legacy demo shell.

Host applications should register the shared UI services and chart wrapper before rendering Economy components:

```csharp
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Economy.Components.Rendering;
using CanDoItAll.Economy.Components.Services;

builder.Services.AddCanDoItAllBaseLib();
builder.Services.AddCanDoItAllCharts();
builder.Services.AddBusinessObjectComponentRenderers();
builder.Services.AddEconomyNodeWorkspaceSession(options =>
{
    options.NodeBaseUri = builder.Configuration["EconomyUi:NodeBaseUri"] ?? options.NodeBaseUri;
    options.ApiBasePath = builder.Configuration["EconomyUi:ApiBasePath"] ?? options.ApiBasePath;
    options.ApiKeyHeaderName = builder.Configuration["EconomyUi:ApiKeyHeaderName"] ?? options.ApiKeyHeaderName;
    options.ApiKey = builder.Configuration["EconomyUi:ApiKey"];
    options.BearerToken = builder.Configuration["EconomyUi:BearerToken"];
});
```

The app host must include BaseLib, Economy, and Charts assets:

```razor
<link rel="stylesheet" href="_content/CanDoItAll.Components.BaseLib/css/material-icons.css" />
<link rel="stylesheet" href="_content/CanDoItAll.Components.BaseLib/css/output.css" />
<link rel="stylesheet" href="_content/CanDoItAll.Economy.Components/css/economy-components.css" />
<ChartsHeadAssets />
```

The demo route `/sdk-components` renders the generic component workbench with in-memory sample data. It covers `LedgerSdkShell`, account/source/security selectors, transaction workbench/detail primitives, business-object state/history/diff/action surfaces, `CdaChart` analytics, and confirmation-gated node-admin panels.

For manual construction, wire the same objects directly:

```csharp
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Sdk;

using var ledgerDriver = new EconomyNodeLedgerClient(new Uri("http://localhost:5000/"));

ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
var payloadBuilder = new SignaturePayloadBuilder(hasher);
var signer = new AccountInputSigner(SignatureProviderRegistry.CreateDefault());
var txBuilder = new BusinessObjectTransactionBuilder(
    ledgerDriver,
    hasher,
    payloadBuilder,
    signer,
    new SystemClock());

var client = new EconomyClient(ledgerDriver, ledgerDriver, txBuilder);
```

`EconomyNodeLedgerClient` implements both `ILedgerGateway` and `ILedgerReadClient`, so it can be passed to the high-level client and transaction builder.

## 4. Load an Account

```csharp
var context = new LedgerAccountContext(new LedgerId("default"));
BusinessObjectAccount alice = await client.LoadAccountAsync(
    context,
    key,
    cancellationToken);
```

Use `new LedgerAccountContext(new LedgerId("default"), new ScenarioId("simulation-a"))` when working in a scenario.

## 5. Read Balance and History

```csharp
LedgerAccountBalance balance = await alice.GetBalanceAsync(cancellationToken);
IReadOnlyList<LedgerSpendableOutput> outputs = await alice.GetSpendableOutputsAsync(cancellationToken);
IReadOnlyList<LedgerTransactionSummary> recent = await alice.GetTransactionsAsync(
    maxCount: 25,
    includeData: true,
    cancellationToken);
```

Balance and history are read through `ILedgerReadClient`. The node driver uses `/api/v1/ledger/{ledgerId}/accounts/{address}/...` by default. If you must talk to an older compatibility surface, construct `EconomyNodeLedgerClient` with `new EconomyNodeLedgerClientOptions { ApiBasePath = "api" }`.

## 6. Build and Submit a Transaction

```csharp
(byte[] receiverPublicKey, _) = EcdsaP256KeyMaterial.CreateKeyPair();
Address receiverAddress = Address.FromPublicKey(receiverPublicKey);

var payment = new PaymentRequest(
    receiverAddress,
    Amount.FromInt64(25));

LedgerSubmitResult result = await alice.SendPaymentAsync(payment, cancellationToken);

if (!result.Accepted)
{
    string message = string.Join("; ", result.Errors.Select(error => error.ToString()));
    throw new InvalidOperationException(message);
}
```

Account helpers build signed ledger transactions, submit them through `ILedgerGateway`, and return `LedgerSubmitResult`.

## 7. Create a BusinessObject

```csharp
var create = new CreateBusinessObjectRequest(
    new BusinessObjectId("invoice-1001"),
    objectType: "invoice",
    stateJson: """{"status":"draft","amount":250}""",
    MainAmount: Amount.FromInt64(250),
    ItemType: TransactionDataItemType.BusinessObject);

LedgerSubmitResult created = await alice.CreateBusinessObjectAsync(create, cancellationToken);
```

The BusinessObject is represented by a UTXO with a `BusinessObjectRef`. Updating or transferring it spends the current object output and creates the next version.

The HTTP node driver and in-process driver now share contract coverage for core account reads, BusinessObject create/update reads, typed problem responses, cancellation, and node transaction-detail reads. Mutating HTTP calls do not retry automatically because the write contract does not yet have idempotency keys.

## Common Pitfalls

- The account must own spendable UTXOs before it can pay, create value-bearing BusinessObjects, or transfer tokens.
- The first transaction for a ledger/scenario must be genesis. A node normally bootstraps the default treasury genesis during initialization.
- Non-treasury accounts must submit signed ledger transactions. Node helper endpoints are treasury/development convenience paths, not a general signer for every account.
- Projection tables are read models. Do not mutate them to create business state.
