# SB03 Payment Token And UTXO Flows

## Status

- `Completed`

## Objective

Make Payment, Token Transfer, UTXO Merge, and UTXO Split balance-aware, validated, reviewable, and responsive using SB02 primitives.

## Covered Inputs

- R008-R010, R012-R013.

## Prerequisites

- SB02 completed and ACP-01 passed.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionSendForm.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionUtxoMergeForm.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionUtxoSplitForm.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/AvailableTokenSelector.razor`

## Deliverables

- Four guided forms, empty/error states, immediate review, and valid/invalid tests.

## Dependency Impact

- SB05 depends on these flows; UTXO regressions invalidate prior bundle guarantees.

## Validation Depth

- Behavior tests plus targeted adapter/domain regression and desktop/tablet/mobile browser proof in SB05.

## Implementation Steps

1. Add failing invalid readiness/balance tests.
2. Compose shared inputs/validation/review into all four forms.
3. Preserve UTXO filtering and output semantics.
4. Run component and adapter/domain regressions.

## Do Not Do

- Do not change ledger transaction rules or allow token/BO UTXOs into plain Merge/Split.

## Acceptance Checklist

- [x] Positive/bounded amounts and receivers are enforced.
- [x] Zero-token/zero-UTXO states explain next steps and block submit.
- [x] Split plan cannot exceed selected total or be empty.
- [x] Existing Merge/Split tests remain green.

## Proof Required

- Targeted test transcripts, source assertions, and SB05 browser rows.

## Browser Validation Logging

- Send Transaction Payment/Token/Merge/Split; all three viewports; populated where sample/seed allows and explicit empty states otherwise.

## Progression Gate

- All four flows pass direct tests before SB05.

## Suggested Agent Prompt

```text
Implement SB03 only using SB02 primitives. Preserve UTXO semantics and prove valid/invalid behavior.
```

## C# Architecture Impact

- Replaces remaining duplicated field/validation markup with shared components.

## Boundary Ownership

- Each form owns only its domain-specific UI rules.

## Dependency Direction

- Unchanged.

## Pattern Decision

- Composition over inheritance.

## Testability Contract

- Each form is directly rendered with sample balances/outputs.

## Partial Class Policy

- No partials.

## Architecture Proof Required

- Source audit shows no copied shell/selector/validation implementation.
