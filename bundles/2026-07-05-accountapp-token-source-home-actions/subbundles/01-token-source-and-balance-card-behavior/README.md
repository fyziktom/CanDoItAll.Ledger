# Token Source And Balance Card Behavior

## Status

- `Completed`

## Objective

- Make `EconomyAccountWorkspace` display only real token sources and fold those sources into the Balances card instead of a separate Token Sources card.

## Covered Inputs

- N001: token sources should show just real token sources, not unique tokens or BOs.
- N002: one card should show main currency and token sources to save space.

## Prerequisites

- Bundle prepared-stage validator passes.
- Current-state source references have been confirmed against the repo.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor`
- `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectAccount.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectOwnershipPlacementComponentTests.cs`

## Deliverables

- Balances card renders main balance and real token-source rows.
- Separate Token Sources card is removed from the work grid.
- Token-source filtering excludes one-amount token outputs and BO-backed token outputs.
- Focused component tests cover positive and negative token-source cases.

## Dependency Impact

- SB02 browser proof depends on this phase because the visual footprint and card count are not meaningful until token-source display is consolidated.
- Commerce and BO creation dialogs depend on the same domain wording, so the display predicate must match existing source-selection semantics.

## Validation Depth

- Critical UI/domain foundation with Semantic Adequacy Gate and artifact-backed proof manifest.

## Implementation Steps

1. Add a focused failing/negative component assertion for mixed spendable outputs: real token source, unique one-token output, and BO-backed token output.
2. Update `TokenSourceOutputs` or an equivalent helper to require `BusinessObject is null`, `Token is not null`, and token amount greater than `1`.
3. Move token-source rows into the Balances card markup and remove the standalone Token Sources panel.
4. Adjust CSS only as needed for compact rows inside the existing Balances card.
5. Capture source assertions, changed-file hashes, anti-stub audit, and passing test transcripts under `proof/SB01/`.

## Scope Exceptions

- Do not add a new main-source UTXO API unless existing workspace data already exposes the needed source rows.
- Permission grant display is not required in the compact home Balances card unless it can be included without recreating the separate source card.

## Do Not Do

- Do not change ledger transaction selection rules.
- Do not treat token balance totals as token sources.
- Do not display `Token.Amount.Value == 1m` outputs as sources.
- Do not display BO-backed outputs as sources even when their token amount is greater than `1`.

## Acceptance Checklist

- Component test proves a real token source appears.
- Component test proves a unique one-amount token output is absent from source rows.
- Component test proves a BO-backed token output is absent from source rows.
- Source no longer renders a standalone `Token Sources` card.
- Balances card still renders main balance and token balance/type rows.

## Proof Required

- `bundle://proof/SB01/transcripts/failing-first-token-source-filter.txt` or equivalent negative first proof.
- `bundle://proof/SB01/transcripts/passing-component-tests.txt`.
- `bundle://proof/SB01/transcripts/source-assertions.txt`.
- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.
- `bundle://proof/SB01/transcripts/changed-file-hashes.txt`.
- `bundle://proof/SB01/manifest.md`.
- `bundle://proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- Route: AccountApp `/`, unlocked home workspace.
- Viewport: large-screen desktop in SB02 after this subbundle closes.
- Required assertions for this phase: no standalone Token Sources panel; Balances panel includes token-source row for an amount greater than `1`; source-like text for unique/BO outpoints is absent.
- Screenshot path: deferred to `bundle://proof/SB02/browser/accountapp-home-large.png`.
- Review questions: Does the Balances card own the relevant rows, and did the removed card save space without hiding required information?

## Progression Gate

- SB02 may start only after the component tests and source assertions prove the real token-source predicate and the execution report records SB01 closure as passed.

## Suggested Agent Prompt

```text
Implement SB01 only. Preserve the exact token-source domain predicate, consolidate display into the Balances card, add focused component tests, capture proof under proof/SB01, update the execution report, and stop if unique one-token or BO-backed outputs still appear as token sources.
```
