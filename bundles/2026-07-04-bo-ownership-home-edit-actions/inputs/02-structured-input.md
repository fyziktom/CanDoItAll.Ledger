# Structured Input

## Core Objective

- Keep BO ownership verification scanning only on the Home wallet-owner security surface and add BO details editing that stages an update transaction through the minting form.

## Success Criteria

- BO details Actions no longer contains an ownership verification scanner action.
- Home Security still exposes ownership payload scan/paste verification through `MessageSecurityPanel`.
- BO details Actions exposes Edit for a selected BO.
- Edit opens the reused `BusinessObjectCreationWizard` form prefilled from the selected BO.
- Staging an edit submits a `BusinessObjectTransactionMode.UpdateObject` draft for the selected BO id.
- Tests, Docker rebuilds, and Playwright browser proof are captured before closure.

## Hard Constraints

- Do not keep scanner verification in BO Actions.
- Do not remove owner-facing QR presentation needed for someone to show BO ownership.
- Do not implement a separate edit form when the minting wizard can be extended.
- Do not stage edit as a create transaction.

## Allowed Side Effects

- Extend shared BO component models and callback wiring across the AccountApp full shell.
- Add or update targeted tests for BO details actions, Home security ownership verification, and wizard update staging.
- Add bundle proof artifacts and transcripts.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `bundle://inputs/01-source-artifacts.md`

## Input Coverage Signals

- N001: Home-only ownership verification scanner.
- N002: Every BO can be edited from details with the minting form and staged as an update.
- N003: Real validation and Docker app rebuilds are mandatory.

## Dependency And Sequencing Signals

- Remove the misplaced verifier scanner before adding edit to the same BO details Actions tab.
- Prove edit/update staging before Docker/browser closure.

## Validation Expectations

- bUnit tests must prove semantic placement and update draft behavior.
- Browser proof must inspect the rendered Home Security and BO details edit dialog.
- Docker proof must rebuild app images.

## Evidence Contract

- `dotnet test` targeted transcript.
- Full solution or project build transcript.
- Docker build transcripts for AccountApp/Node app images.
- Playwright screenshots and assertions for Home Security and BO edit dialog.
- Proof manifests and semantic invariant contracts for critical subbundles.

## UI Validation Strategy

- First browser pass uses the largest practical desktop viewport.
- Dialog open-state proof must check readability, clipping, lateral overflow, layering, and action affordance.
- Narrower-width follow-up is required if the edit dialog or action list layout is changed.

## Browser Validation Analytics

- SB01 logs route, viewport, Home Security actions, BO details Actions assertions, screenshots, and result.
- SB02 logs route, viewport, Edit dialog actions, staged update assertions, screenshots, and result.
- SB03 logs Docker-served route, viewport, smoke actions, screenshots, and result.

## Working Assumptions

- "Home page" means the full app Home surface rendered by `EconomyAccountWorkspace`.
- "BO Actions tab" means the Actions tab in `BusinessObjectLedgerDetailsDialogContent`.
- "Same form as minting" means reuse or directly extend `BusinessObjectCreationWizard`.

## Primary Risks

- A shallow fix could remove a label while leaving another verifier entry point in BO details.
- A shallow edit button could open a duplicate form or stage create-mode drafts.
- Docker/browser proof could fail because app host setup differs from component-test setup.
