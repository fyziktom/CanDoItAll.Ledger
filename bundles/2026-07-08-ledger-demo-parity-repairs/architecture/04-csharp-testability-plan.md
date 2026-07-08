# C# Testability Plan

## Characterization Tests

- Capture failing-first tests for console missing UTXO split/merge helper behavior or lesson parity.
- Capture failing-first test for SSR development workspace BO create draft token amount if it still uses `250`.

## Isolated Unit Tests

- Use existing in-memory ledger fixtures where possible.
- Add focused tests that instantiate or call shared `BusinessObjectAccount` paths through demo-facing helpers/session methods.

## Negative Tests

- Prove plain UTXO split/merge rejects BO/token outputs by using shared `BusinessObjectTransactionBuilder` behavior or existing advanced transaction tests.
- Prove token minting cannot mint main amount by asserting positive `MainAmount` is rejected in shared builder tests or source assertions.

## Integration Composition Smoke

- Build console demo project after helper/lesson updates.
- Build SSR components demo project after development session/sample updates.
- Run relevant tests through the test project.

## Fake Provider Tool Driver Proof

- N/A. No external provider, tool, or process driver is introduced.

## Direct Testability Contract

- Console parity proof must not require a live node for core transaction shape assertions.
- SSR fallback proof must not require a live node for one-token BO draft and UTXO split/merge details.
