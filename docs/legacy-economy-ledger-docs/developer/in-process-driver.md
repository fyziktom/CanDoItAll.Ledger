# In-Process Driver

Use the in-process driver for tests, local simulations, deterministic demos, and code paths where you want the ledger engine in the same process.

`InProcessLedgerGateway` implements the same `ILedgerGateway` and `ILedgerReadClient` contracts as the node driver. That lets application code depend on abstractions while composition decides whether calls go over HTTP or stay local.

## Composition Sketch

```csharp
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using CanDoItAll.Economy.Node;
using CanDoItAll.Economy.Sdk;
using CanDoItAll.Economy.Storage.InMemory;

ITransactionHasher hasher = new TransactionHasher(new DefaultTransactionCanonicalizer());
var payloadBuilder = new SignaturePayloadBuilder(hasher);
var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-05-09T00:00:00Z"));
var store = new InMemoryLedgerStore(hasher);
var registry = SignatureProviderRegistry.CreateDefault();

var executor = new LedgerExecutor(
    store,
    hasher,
    payloadBuilder,
    new DevelopmentLedgerAdminSigner(),
    time,
    registry);

var gateway = new InProcessLedgerGateway(executor, hasher, time);
var inputSigner = new AccountInputSigner(registry);
var txBuilder = new BusinessObjectTransactionBuilder(
    gateway,
    hasher,
    payloadBuilder,
    inputSigner,
    time);

var client = new EconomyClient(gateway, gateway, txBuilder);
```

`DevelopmentLedgerAdminSigner` is suitable for development-style local execution. Production deployments should provide an admin signer backed by protected key material.

## Bootstrap a Local Ledger

The first transaction for a ledger/scenario must be a `Genesis` transaction.

```csharp
var context = new LedgerAccountContext(new LedgerId("local-sim"));
(byte[] publicKey, byte[] privateKey) = EcdsaP256KeyMaterial.CreateKeyPair();
var key = new EconomyAccountKeyEnvelope(
    SignatureAlgorithmId.EcdsaP256Sha256,
    "alice",
    publicKey,
    privateKey);

LedgerTransaction genesis = new(
    context.LedgerId,
    context.ScenarioId,
    Version: 1,
    TransactionKind.Genesis,
    SigningMode.Independent,
    time.UtcNow,
    Inputs: Array.Empty<TransactionInput>(),
    Outputs: new[]
    {
        new TransactionOutput(0, key.Address, Amount.FromInt64(100))
    },
    Data: null);

LedgerSubmitResult seeded = await gateway.SubmitTransactionAsync(
    genesis,
    cancellationToken);
```

After genesis, load an account through `EconomyClient` and use the same account helpers shown in the SDK quickstart.

## Why Use It

- Fast tests without HTTP startup.
- Scenario simulations with a `ManualTimeProvider`.
- Domain services that should be exercised against the real ledger validation engine.
- Deterministic local demos where persistent storage is not needed.

## What It Still Enforces

The in-process driver still calls `LedgerExecutor.SubmitAsync`, so validation is not skipped:

- transaction shape;
- genesis order;
- duplicate transaction checks;
- input UTXO/spent-output checks;
- signatures;
- conservation;
- token creation policy when configured in the executor;
- ledger sequence/hash advancement.

## Limits

- `InMemoryLedgerStore` is not durable.
- It does not model every database locking behavior.
- It is best for tests/simulations, not a shared production ledger.
