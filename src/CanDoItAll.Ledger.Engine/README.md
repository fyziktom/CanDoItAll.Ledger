# CanDoItAll.Ledger.Engine

Deterministic validation and execution for CanDoItAll Ledger transactions.

The engine checks transaction structure, references, signatures, authorities,
asset conservation, token rules, business-object transitions, replay, and
chain consistency. Choose it for an in-process ledger host or for independent
verification of Ledger history.

## Requirements

- .NET 10
- A store and cryptographic services compatible with
  `CanDoItAll.Ledger.Core`

## Install

```powershell
dotnet add package CanDoItAll.Ledger.Engine
```

## Run A Shape Check

```csharp
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Engine;

static ValidationResult ValidateGenesisShape(Address recipient)
{
    var transaction = new LedgerTransaction(
        new LedgerId("agent-expenses"),
        ScenarioId: null,
        Version: 1,
        Kind: TransactionKind.Genesis,
        SigningMode: SigningMode.Independent,
        Timestamp: DateTimeOffset.UtcNow,
        Inputs: [],
        Outputs:
        [
            new TransactionOutput(
                0,
                recipient,
                Amount.FromDecimal(1_000m))
        ],
        Data: null);

    ValidationResult shape =
        new TransactionShapeValidator().ValidateShape(transaction);

    return shape;
}
```

This example checks only the transaction shape. It does not confirm references,
signatures, authority, conservation, or state transitions. Supply a recipient
derived from real account key material so the resulting allocation can be
spent.

## Authoritative Execution

Compose `LedgerExecutor` with:

- `ILedgerStore` or, preferably, `ILedgerConfirmationStore`;
- `ITransactionHasher` and `ISignaturePayloadBuilder`;
- `ILedgerAdminSigner`;
- `ITimeProvider`;
- `ISignatureProviderRegistry`;
- the required issuance and token-creation policies.

Call `LedgerExecutor.SubmitAsync(...)` and inspect the returned
`LedgerSubmitResult`. An `ILedgerConfirmationStore` provides an atomic
confirmation boundary; a plain store cannot coordinate confirmations across
independent processes.

## Correctness Notes

- Preflight and shape validation are early gates, not proof of acceptance.
- Keep clocks and policy inputs explicit so replay and tests remain
  deterministic.
- Preserve canonical hash, signing, and ledger-sequence behavior when replacing
  a store or signer.
- Use `LedgerChainVerifier` and `LedgerSemanticReplayVerifier` when auditing
  persisted history.
- Surface validation errors to callers; do not convert a rejected transaction
  into an apparent success.

See the
[runtime architecture](https://github.com/fyziktom/CanDoItAll.Ledger/blob/main/docs/architecture/ledger-runtime.md),
[Core contracts](https://www.nuget.org/packages/CanDoItAll.Ledger.Core), and
[full source](https://github.com/fyziktom/CanDoItAll.Ledger).

This package is part of the [CanDoItAll ecosystem](https://aicandoitall.com)
and uses the repository's MIT License.
