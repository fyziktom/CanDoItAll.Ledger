# Target Solution

## End State

- Console demo exposes plain UTXO split/merge helpers and a lesson path that demonstrates plain UTXO split/merge separately from advanced BO split payment and BO merge.
- Blazor SSR demo development fallback creates BO drafts with the same one-token carrier default as the WASM AccountApp and proves UTXO split/merge through the shared account/session path.
- Sample adapters and tests name `UtxoMerge` and `UtxoSplit` explicitly so demo parity cannot regress to old BO modes unnoticed.

## Boundaries

- Shared ledger behavior remains in `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`.
- Account-level orchestration remains in `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs`.
- Demo projects may add helpers, lessons, and tests, but must not assemble raw `LedgerTransaction` objects for behavior already owned by the shared builder.

## Allowed Side Effects

- Console demo output and README wording can change to describe UTXO split/merge and BO token-only carrier semantics.
- SSR development fallback defaults can change from an invalid large BO carrier token amount to `Amount.FromInt64(1)`.
- Tests may add helper fixtures for console/SSR parity.

## Explicit Non-Goals

- Do not change ledger validation policy unless tests prove the existing shared behavior is wrong.
- Do not add a new project boundary.
- Do not change the WASM AccountApp UI flow except through shared component/test compatibility if required.
