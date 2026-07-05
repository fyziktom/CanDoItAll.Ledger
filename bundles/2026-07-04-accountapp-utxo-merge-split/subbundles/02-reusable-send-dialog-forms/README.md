# SB02 Reusable Send Dialog Forms

## Status

- `Completed`

## Objective

- Extract Send, UTXO Merge, and UTXO Split into reusable transaction form components and wire the home send dialog to the new explicit UTXO domain behavior.

## Success Criteria

- The dialog no longer presents BusinessObject merge/split as home UTXO Merge/Split operations.
- Send, Merge, and Split forms can be rendered and tested independently.
- Merge/Split forms use workspace spendable outputs, show only eligible plain UTXOs, and calculate selected totals/output totals.
- First large-screen browser screenshot of the send dialog shows clean layout for Send, Merge, and Split tabs.

## Covered Inputs

- R001, R002, R003, R005, R006, R007, R009, and R010 pre-proof.

## Prerequisites

- SB01 completed with trusted explicit UTXO transaction proof.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TxInputCard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TxOutputCard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`

## Deliverables

- Reusable form components and helper models for Send, UTXO Merge, and UTXO Split.
- Dialog/shell wiring that passes `SpendableOutputs`, bookmarks, and account scope to those forms.
- Component tests for form eligibility, totals, output rows, and submit payloads.
- Layout adjustments only where needed by large-screen proof.

## Dependency Impact

- SB03 depends on this subbundle for UI operation and large-dialog layout. If forms are not reusable or layout is broken, final browser proof cannot close the raw notes.

## Validation Depth

- Critical UI foundation with component tests, large-screen browser proof, Semantic Adequacy Gate, and artifact-backed proof manifest.

## Implementation Steps

1. Read SB01 proof and source changes before editing UI.
2. Add component tests that fail against the current BO merge/split tabs.
3. Extract plain Send form if currently embedded in `TransactionComposerShell`.
4. Add UTXO selector/list helper component for eligible outputs.
5. Add Merge form with selected totals and self/external receiver mode.
6. Add Split form with equal/specific output modes and total validation.
7. Wire dialog/shell to pass spendable outputs and submit the new modes.
8. Inspect large desktop browser screenshots and repair dialog/details overflow if needed.

## Scope Exceptions

- Real docker-backed transaction submission and transaction-detail proof are owned by SB03.
- Small and medium responsive work is intentionally skipped per raw request.

## Do Not Do

- Do not spend disallowed outputs to make the UI easier.
- Do not leave Merge/Split fields inside one monolithic switch when reusable components are required.
- Do not redesign unrelated account or gallery surfaces.

## Acceptance Checklist

- [x] Send/Merge/Split components exist and are independently renderable.
- [x] Component tests cover eligible filtering and submit payload shapes.
- [x] Browser screenshot shows the send dialog in Send/Merge/Split states on large desktop.
- [x] Details dialog layout is repaired if first proof shows overflow or overlay.
- [x] `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md` exist.

## Proof Required

- Targeted component/unit tests.
- `dotnet build CanDoItAll.Ledger.slnx`.
- Playwright large desktop pass against the AccountApp send dialog.
- Screenshots under `bundle://proof/SB03/screenshots/` from final docker-backed browser proof.
- Source assertion transcript and anti-stub audit transcript.

## Browser Validation Logging

- Route/window: AccountApp home send dialog.
- Viewport: large desktop, at least `1600x900`; no small/medium proof required.
- Actions: open send dialog, switch Send/Merge/Split tabs, inspect selector/output rows and totals, capture screenshots.
- Review questions: text readable, no overlap, selector and output lists scroll cleanly, buttons remain reachable, details dialog layout remains stable if touched.

## Progression Gate

- Passed. SB03 completed after component tests passed, large-screen send dialog screenshots were acceptable, and the SB02 manifest cited source assertions and screenshot review.

## Suggested Agent Prompt

```text
Implement SB02 only. Build reusable Send, UTXO Merge, and UTXO Split form components, wire the send dialog to SB01 behavior, prove component and large-screen browser behavior, and stop before docker-backed final proof.
```
