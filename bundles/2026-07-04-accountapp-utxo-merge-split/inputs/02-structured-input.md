# Structured Input

## Core Objective

- Repair the AccountApp home send dialog so Send, Merge, and Split are reusable forms and Merge/Split perform explicit plain UTXO transactions instead of BusinessObject operations.

## Success Criteria

- Merge lets the user choose eligible UTXOs and submits a transaction whose inputs are exactly the selected UTXOs and whose output set has exactly one main-currency output.
- Split lets the user choose one or more eligible UTXOs and creates equal-amount or specific-amount output rows.
- Send still works after the form extraction.
- Transaction details show large input/output sets without overlay, clipping, or broken scrolling on a large desktop viewport.

## Hard Constraints

- Preserve "all those forms" as reusable component scope.
- Preserve "proper testing via UI of all types" as Send, Merge, and Split browser proof.
- Preserve "rebuild and restart our dockers" as a required final proof step.
- Exclude BO unique outputs. For this bundle, eligible Merge/Split inputs are plain main-currency outputs only: `BusinessObject is null` and `Token is null`.
- Do not spend small/medium viewport effort; large desktop only is requested.

## Allowed Side Effects

- New account request models, transaction builder methods, adapter/session routing, reusable transaction form components, focused tests, and targeted details-dialog overflow fixes.
- No broad AccountApp redesign or token/BusinessObject merge-split redesign.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionComposerShell.razor`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectTransactionBuilder.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/Transactions/TransactionDetailsDialogContent.razor`

## Input Coverage Signals

- Merge selector, BO exclusion, one-output merge, selected-vin proof, split equal outputs, split specific outputs, reusable forms, UI proof for all types, docker rebuild/restart, and many-row detail screenshots each need explicit proof rows.

## Dependency And Sequencing Signals

- Domain vin/vout semantics must land before UI proof.
- Reusable UI forms must land before docker/browser proof.
- Docker/browser proof must close final raw notes.

## Validation Expectations

- Targeted domain tests, adapter/component tests, build, docker restart transcript, Playwright large-screen proof, transaction IDs, transaction details assertions, screenshots, and final validator.

## Evidence Contract

- `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md`
- `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md`
- `proof/SB03/manifest.md`, `proof/SB03/semantic-invariants.md`, and red-team verifier artifact
- Transcripts for failing-first tests, passing tests, build, docker restart, Playwright proof, source assertions, and anti-stub audits.

## UI Validation Strategy

- Use a large desktop viewport at least `1600x900`.
- Capture send dialog screenshots for Send, Merge, and Split.
- Capture transaction details screenshots for Merge with multiple inputs and one output and Split with many outputs.
- Review readability, clipping, lateral overflow, scroll containers, button reachability, and layering.

## Browser Validation Analytics

- SB02 logs send-dialog route/window, viewport, actions, screenshots, and visual review result.
- SB03 logs docker-backed route/window, viewport, submitted transaction IDs, vin/vout assertions, screenshots, and final result.

## Working Assumptions

- The AccountApp home send dialog is the target home page surface.
- Plain UTXO means no token and no BusinessObject reference.
- Positive split remainder can be returned to self as change.

## Primary Risks

- A label-only or UI-only fix could appear correct while still submitting wrong transactions.
- Seed data may not naturally contain enough plain UTXOs, so browser proof may need setup transactions.
- Transaction details may need targeted scroll/height fixes after real many-row proof.
