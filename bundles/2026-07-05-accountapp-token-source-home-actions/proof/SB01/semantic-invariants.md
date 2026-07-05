# SB01 Semantic Invariants

## Invariant INV-SB01-TOKEN-SOURCE

- Invariant ID: `INV-SB01-TOKEN-SOURCE`
- Source raw note: N001 says "Token source is utxo with larger than 1 amount of that token"; N002 says the Balances card can show token sources except unique one-amount tokens.
- Expected behavior: AccountApp home uses the Balances panel to show source UTXO rows only when a spendable output has `BusinessObject is null`, `Token is not null`, and `Token.Amount.Value > 1m`.
- Disallowed shallow implementation: hiding the old Token Sources heading while still rendering one-amount token UTXOs or BO-backed token outputs as source rows.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-token-source-filter.txt` shows `HomeBalances_ShowOnlyRealTokenSourcesInsideBalancesCard` failed before the production change because the old standalone `Token Sources` panel was still rendered.
- Passing test: `bundle://proof/SB01/transcripts/passing-component-tests.txt` shows the same component-test class passed after the production change.
- Changed source files: `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor`, `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`, `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectOwnershipPlacementComponentTests.cs`; hashes are in `bundle://proof/SB01/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt` proves the component filters BO-backed outputs, filters unique one-amount outputs, renders source rows inside Balances, and no longer contains the standalone Token Sources header.
- Red-team negative case: the sample workspace includes unique one-amount permission/rule outputs and a BO-backed permission output; the component test asserts those outpoints are absent while the real `tx-token-source` source remains present.
- Downstream dependency check: SB02 browser validation must prove the compact AccountApp home layout after this predicate and card consolidation are in place.

## Closure Notes

- Shallow-pass trap rejected: structure-only removal of a card title would not satisfy the negative assertions for `tx-permission-source`, `tx-rule-permission-source`, and `tx-rule-cashpool`.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.
