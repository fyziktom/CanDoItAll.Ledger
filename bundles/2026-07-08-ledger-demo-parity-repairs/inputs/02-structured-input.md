# Structured Input

## Objective

- Update the other ledger-related apps in this repository so their demo behavior matches the now-working WASM AccountApp transaction behavior.

## In-Scope Apps

- Console demo: `repo://examples/CanDoItAll.Economy.ConsoleDemo`
- Blazor SSR components demo: `repo://examples/CanDoItAll.Economy.Components.Demo`

## Reference Behavior

- WASM AccountApp routes transactions through `WasmAccountWorkspaceAdapter`.
- Explicit UTXO split/merge use `BusinessObjectTransactionMode.UtxoSplit` and `BusinessObjectTransactionMode.UtxoMerge`.
- Plain UTXO split/merge select explicit plain main-currency outpoints, excluding BO and token outputs.
- BO outputs carry exactly one carrier token and zero main amount; main currency is emitted in separate plain outputs when needed.
- Permission-holder token minting emits token outputs with zero main amount and rejects main minting.

## Hard Constraints

- Do not duplicate ledger transaction assembly in the demos.
- Keep `BusinessObjectTransactionBuilder` and `BusinessObjectAccount` as the shared production behavior path.
- Add tests that would fail if the demos only relabeled old BO merge/split behavior.
- Keep existing WASM AccountApp behavior and tests passing.

## Validation Expectations

- Targeted tests for console helper/lesson semantics.
- Targeted tests for SSR development session and sample adapter semantics.
- `dotnet test` for relevant filters.
- `dotnet build` for changed demo projects and solution surface as practical.
- Artifact-backed proof under `proof/SBxx/`.
