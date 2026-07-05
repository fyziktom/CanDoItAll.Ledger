# SB03 Docker Browser Transaction Proof

## Status

- `Completed`

## Objective

- Rebuild/restart the docker stack and prove through the large-screen UI that Send, UTXO Merge, and UTXO Split submit real transactions with expected transaction details and readable many-row dialogs.

## Success Criteria

- Docker stack is rebuilt/restarted and reachable.
- UI proof submits Send, Merge, and Split through the home send dialog.
- Merge details show selected inputs and exactly one output.
- Split details show selected inputs and the requested many outputs.
- Screenshots prove details dialogs with multiple inputs or outputs remain readable without overlay/overflow on large desktop.
- Raw note closure rows are all `Solved` or explicitly reopened with a concrete blocker.

## Covered Inputs

- R009, R010, R011, R012 and final closure for all raw notes.

## Prerequisites

- SB01 and SB02 completed with trusted proof manifests.
- `repo://docker-compose.yml` is available.

## Exact Source References

- `repo://docker-compose.yml`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://bundles/2026-07-04-accountapp-utxo-merge-split/reviews/01-execution-report.md`

## Deliverables

- Docker rebuild/restart transcript.
- Playwright scripts/transcripts for Send, Merge, Split, and transaction details.
- Screenshots under `proof/SB03/browser/`.
- Final execution report and raw-note closure.
- Final validator transcript.

## Dependency Impact

- This is the final closure subbundle. Weak proof here invalidates the bundle closure and must reopen SB01 or SB02 based on defect source.

## Validation Depth

- Critical end-to-end regression and closure with docker, Playwright, screenshots, transaction details, red-team verifier, and completed-stage bundle validator.

## Implementation Steps

1. Re-read SB01/SB02 manifests and verify referenced artifacts exist.
2. Rebuild/restart docker compose stack.
3. Use large desktop Playwright to open AccountApp home, unlock/import as needed, and create enough plain UTXOs for proof.
4. Submit a normal Send transaction and record success.
5. Submit a Merge transaction from multiple selected UTXOs and open transaction details.
6. Submit a Split transaction with many outputs and open transaction details.
7. Capture screenshots and DOM assertions for dialog/details layout.
8. Repair defects and repeat until proof passes.
9. Update execution report, raw note closure, manifests, semantic invariants, and run completed validator.

## Scope Exceptions

- Small and medium screen screenshots are intentionally excluded per raw request.

## Do Not Do

- Do not close from screenshots alone; transaction details must prove vin/vout shapes.
- Do not use seeded fake details as the only proof.
- Do not skip docker rebuild/restart.

## Acceptance Checklist

- [x] Docker rebuild/restart transcript exists.
- [x] Send browser proof exists.
- [x] Merge browser proof includes tx id, input count, output count, and screenshot.
- [x] Split browser proof includes tx id, input count, output count, and screenshot.
- [x] Visual review answers are recorded.
- [x] Final raw note closure is complete.
- [x] Completed-stage validator passes.

## Proof Required

- Docker transcript under `bundle://proof/SB03/transcripts/`.
- Playwright transcripts under `bundle://proof/SB03/transcripts/`.
- Screenshots under `bundle://proof/SB03/screenshots/`.
- Final `dotnet test` and `dotnet build` transcripts.
- `bundle://proof/SB03/manifest.md`, `bundle://proof/SB03/semantic-invariants.md`, and red-team verifier artifact.

## Browser Validation Logging

- Route/window: AccountApp home, send dialog, transaction details dialog.
- Viewport: large desktop, at least `1600x900`.
- Actions: submit Send, Merge, Split; open newest transaction details for Merge and Split; assert badges or raw JSON show expected input/output counts.
- Screenshots: send dialog states, merge details with multiple inputs and one output, split details with many outputs, any repaired overflow state.
- Review questions: all text readable, no overlap, no clipped dialog content, scroll lists obvious and usable, primary actions reachable, details cards do not collide laterally.

## Progression Gate

- Passed. Docker/browser proof passed, raw notes N001-N008 are closed with cited proof, and completed-stage validator passed.

## Suggested Agent Prompt

```text
Implement SB03 only. Rebuild/restart docker, run large-screen UI proof for Send/Merge/Split, capture real transaction details and screenshots, repair any failed behavior, update closure proof, and run completed-stage validation.
```
