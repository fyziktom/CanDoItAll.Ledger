# SB04 Business Object Transaction Wizards

## Status

- `Completed`

## Objective

Implement guided Create, Transfer, Update, Close, Clone, BO Merge, and BO Split Payment flows, including the literal three-source clone wizard.

## Covered Inputs

- R003-R007, R012-R013 and the clone example in the raw request.

## Prerequisites

- SB02 completed and ACP-01 passed.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectGalleryCard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectRendererHost.razor`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`

## Deliverables

- Seven BO flows, source cards, preload/current-state preview, advanced JSON editing, validation, review, and tests.

## Dependency Impact

- Critical literal-request phase. SB05 cannot close without every BO flow visible and usable or explicitly blocked/delegated.

## Validation Depth

- Semantic Adequacy Gate with shallow-pass trap (pretty raw fields only), adversarial invalid origin/duplicate/incompatible/total cases, positive draft proof, anti-stub audit, and manifest.

## Implementation Steps

1. Add failing tests for missing BO selection and clone origin behavior.
2. Build simple Create and source-selection flows for Transfer/Update/Close.
3. Build Clone steps: source strategy, source result selection, target/review.
4. Build Merge steps: two distinct compatible sources, target/review.
5. Build Split Payment steps: source, output allocation, review.
6. Prove direct tests, adapter request shapes, and ACP-02.

## Do Not Do

- Do not invent remote node queries in Razor or silently pretend address/direct lookup succeeded when data is not loaded.

## Acceptance Checklist

- [x] Loaded BOs appear as compact selectable cards.
- [x] Transfer/Update/Close preload id/type/version/state.
- [x] Clone supports Mine/Address/Direct id with explicit results/errors.
- [x] Merge rejects duplicate/incompatible selections.
- [x] Split Payment rejects empty/invalid outputs.
- [x] `proof/SB04/manifest.md` and semantic invariants exist.

## Proof Required

- Failing-first/passing test transcripts, source assertions, anti-stub audit, hashes, and SB05 screenshots for every step.

## Browser Validation Logging

- All seven BO flows; every Clone/Merge/Split Payment step; desktop/tablet/mobile; empty AccountApp and populated BUnit/seed contexts.

## Progression Gate

- ACP-02 and subbundle closure pass before SB05.

## Suggested Agent Prompt

```text
Implement SB04 only. Ship guided BO flows and the full three-strategy clone wizard, prove semantics, then stop at ACP-02.
```

## C# Architecture Impact

- Completes mode-specific extraction from composer into cohesive form components.

## Boundary Ownership

- BO UI selection/preload stays in Components; ledger assembly stays in BusinessObjects/adapter.

## Dependency Direction

- Unchanged; no AccountApp service references in forms.

## Pattern Decision

- UI-local strategy choice and component composition.

## Testability Contract

- Each wizard step/state is renderable directly from fixed `IBusinessObjectView` inputs.

## Partial Class Policy

- No partials.

## Architecture Proof Required

- Direct tests, composer shrink, ACP-02, no copied selection logic, explicit CodeAnalytics gap.
