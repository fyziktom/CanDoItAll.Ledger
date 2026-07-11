# SB02 Shared Transaction Interaction Foundation

## Status

- `Completed`

## Objective

Create the complete mode catalog, shared flow shell, validation contract, compact BO selector, amount/balance input, and thin composer orchestration.

## Covered Inputs

- R001, R002, R011-R013; shared foundation for every raw note.

## Prerequisites

- SB01 completed with proposal coverage and prepared-stage validation.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectGalleryCard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCardDisplay.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/TransactionUiComponentTests.cs`

## Deliverables

- Catalog/disposition, reusable shell/validation/selection/amount/review components, workspace data plumbing, and direct tests.

## Dependency Impact

- Critical foundation for SB03/SB04/SB05. Reopen if downstream forms copy its concerns.

## Validation Depth

- Semantic Adequacy Gate, failing-first catalog/validation tests, positive selection tests, anti-stub audit, changed-file hashes, and AccountApp dependent smoke.

## Implementation Steps

1. Capture failing tests for enum coverage and invalid submit readiness.
2. Implement catalog and immutable validation model.
3. Build shared shell and BaseLib-backed picker/input/review primitives.
4. Make composer a thin coordinator and pass workspace BO/balance data.
5. Prove direct component tests and one dependent flow smoke.

## Do Not Do

- Do not implement all mode-specific business forms here; do not add partials/projects/service location.

## Acceptance Checklist

- [x] Every enum mode has one direct/delegated disposition.
- [x] Shared primitives are independently renderable.
- [x] Invalid active flow disables submit.
- [x] Composer no longer owns mode-specific input helpers.
- [x] `proof/SB02/manifest.md` and semantic invariants exist.

## Proof Required

- Failing-first/passing test transcripts, source assertions, anti-stub/no-partial audit, hashes, and downstream AccountApp smoke.

## Browser Validation Logging

- AccountApp Send Transaction at 1920x1080; chooser, one simple flow, one BO selection flow, open validation state.

## Progression Gate

- Pass ACP-01 and subbundle closure gate before SB03/SB04.

## Suggested Agent Prompt

```text
Implement SB02 only. Build and prove the reusable transaction interaction foundation, then stop at ACP-01.
```

## C# Architecture Impact

- Extracts responsibilities from the 441-line composer into cohesive top-level components/models.

## Boundary Ownership

- Components owns UI flow contracts; domain builders remain unchanged.

## Dependency Direction

- Existing AccountApp -> Components -> BusinessObjects/Core only.

## Pattern Decision

- Catalog, thin facade, UI-local strategy choice, immutable validation result.

## Testability Contract

- Catalog/validation/selector tested directly without composer or network.

## Partial Class Policy

- New partials are blocked.

## Architecture Proof Required

- Composer shrink, no new refs/partials, direct tests, ACP-01 pass, explicit CodeAnalytics gap.
