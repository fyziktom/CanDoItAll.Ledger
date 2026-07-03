# SB05 Transaction Raw View And Tests

## Status

- `Completed`

## Objective

- Finish raw transaction test coverage and make transaction details useful for privacy inspection.

## Covered Inputs

- User needs raw tx similar to Bitcoin raw tx data and newest-to-oldest transaction history.

## Prerequisites

- SB02, SB03, and SB04 complete.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Services/LedgerTransactionOrdering.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/TransactionUiComponentTests.cs`

## Deliverables

- Raw tx structured JSON includes expanded data envelope.
- Tests assert ordering and raw data shape.

## Dependency Impact

- Supports visual and manual inspection in SB06.

## Validation Depth

- Component tests plus browser screenshot.

## Implementation Steps

1. Add tests for raw tx nested state expansion.
2. Re-run transaction ordering tests.
3. Verify no overlap/overflow in large desktop screenshot.

## Do Not Do

- Do not hide ledger-level signature/public-key facts from raw tx.

## Acceptance Checklist

- [ ] Raw tx shows structured envelope/state.
- [ ] Transaction history remains newest-to-oldest.
- [ ] Raw tx can be visually inspected in large-screen dialog.

## Proof Required

- Targeted component test output and screenshot path.

## Browser Validation Logging

- Record URL, viewport, screenshot file, and inspected transaction.

## Progression Gate

- SB06 starts after tests pass locally.

## Suggested Agent Prompt

- Validate raw tx UI and transaction ordering with focused component and browser proof.
