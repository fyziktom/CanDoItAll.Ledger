# SB02 Shared Foundation Proof Manifest

## Implemented Contract

- `TransactionFlowCatalog` owns the sole twelve-mode inventory: eleven direct flows and delegated `CreateRuleToken`.
- `TransactionDraftValidator` is pure, immutable-result validation for amount, address, JSON, token balance, plain UTXO, owned BO, merge compatibility, and submission readiness rules.
- The composer is a coordinator only: catalog chooser, active form, shared validation/review, and callbacks. Its former mode-specific raw field/render helpers were removed.
- Shared primitives are `TransactionFlowChooser`, `TransactionValidationSummary`, `TransactionAmountInput`, `BusinessObjectLookupPanel`, `BusinessObjectSelectionGrid`, and `BusinessObjectSelectionCard`.

## Test Evidence

- Failing-first transcript: `transcripts/01-failing-first.txt`.
- Passing focused components/SSR suite: `transcripts/02-focused-components-passing.txt` — 21 passed, 0 failed.
- The direct test set covers catalog coverage/disposition, family selection, payment and owned-BO submit guards, UTXO merge minimum, merge compatibility, populated card selection, and direct-reference lookup delivery.

## Architecture / Anti-Stub Evidence

- `git diff --check` passed (line-ending notices only).
- Search across new transaction code found no `partial class`, `IServiceProvider`, `GetService`, or `GetRequiredService` usages.
- No project references changed; dependency direction remains AccountApp -> Components -> BusinessObjects/Core.
- CodeAnalytics remains an external validation gap, documented in `reviews/csharp-architecture-gate.md`.

## Changed-File Hashes

```text
9c9cbeefdc60a4e41ac34cf2a18f2b26c1f639c68340509df73b4869a67ea6a3  TransactionFlowFoundation.cs
33db35c3c2a71a1653434f3833744e03910e28f0c328cfb9df3c40d1107d2f57  TransactionFlowChooser.razor
420612e296332a3069fac201b2681496524b77f821be076ddf55691f2a32fd90  TransactionComposerShell.razor
b7df65a81ee28f098437470448ebb764d61d8fa4f9dbd795d71de192e9e372a1  TransactionFlowFoundationTests.cs
```

## Downstream Smoke

- AccountApp WASM was rebuilt and exercised at `http://localhost:5081/`.
- The validated Payment family switch and owned-BO disabled-submit repair are logged in `../SB05/manifest.md`.
