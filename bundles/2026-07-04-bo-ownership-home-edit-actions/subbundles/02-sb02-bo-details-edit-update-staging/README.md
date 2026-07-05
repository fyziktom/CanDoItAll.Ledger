# SB02 BO Details Edit Update Staging

## Status

- `Completed`

## Objective

- Add an Edit action in BO details that opens the same form implementation used for minting and stages an update transaction for the selected BO.

## Success Criteria

- Each BO details Actions surface has an Edit action.
- Edit opens a dialog containing `BusinessObjectCreationWizard` in edit mode.
- The wizard is prefilled from the selected BO state, object type, schema, owner, text, tags, fields, and data items.
- Staging produces `BusinessObjectTransactionMode.UpdateObject` for the selected BO id and updated state JSON.

## Covered Inputs

- N002: "Each BO must be possible to update with use of same form as for minting."
- R004: BO details Edit action.
- R005: reuse the minting form implementation.
- R006: stage an update draft for the selected BO.

## Prerequisites

- SB01 closure gate passed.
- BO details Actions no longer includes ownership verification scanner.
- Source references in this README still exist.

## Exact Source References

- repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectCreationWizardModel.cs
- repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor
- repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor
- repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor
- repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor
- repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor
- repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor
- repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs
- repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs
- repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectTicketMintingComponentTests.cs

## Deliverables

- Extend the wizard model with selected-BO edit source data.
- Extend `BusinessObjectCreationWizard` to support create and edit/update staging modes.
- Add an Edit action and edit dialog from BO details.
- Wire edit staging callback through Gallery, Home, full shell, and adapter host.
- Add tests for prefill and update draft staging semantics.

## Dependency Impact

- SB03 cannot run final browser proof until this subbundle proves update staging. Docker/browser closure must validate the final integrated edit flow.
- Weak SB02 proof would make raw note N002 unresolved even if a button exists.

## Validation Depth

- Critical UI and transaction foundation with Semantic Adequacy Gate, adversarial negative test, semantic positive test, source assertion, Playwright proof, and anti-stub audit.

## Implementation Steps

1. Add edit source metadata to `BusinessObjectCreationWizardModel`.
2. In the wizard, initialize fields from the edit source when present.
3. Stage update-mode drafts in edit mode, preserving original BO id and owner.
4. Add an Edit action to `BusinessObjectLedgerDetailsDialogContent`.
5. Open a dialog with the wizard in edit mode and pass staged drafts to host submission.
6. Wire callbacks through `BusinessObjectLedgerGallery`, `EconomyAccountWorkspace`, `VenftSsrEconomyApp`, and `EconomyAccountWorkspaceAdapterHost`.
7. Add tests covering action rendering, wizard prefill, and captured update draft.

## Scope Exceptions

- None. The raw note says each BO must be possible to update, so unsupported BO types must be explicitly discovered and recorded if any appear.

## Do Not Do

- Do not duplicate the minting form.
- Do not generate a new object id for edit staging.
- Do not stage edit as `Create`.
- Do not reintroduce ownership scanner actions in BO details while editing that action list.

## Acceptance Checklist

- BO details contains an Edit action.
- Edit dialog renders `BusinessObjectCreationWizard`.
- Wizard edit mode uses selected BO data.
- Captured draft has `SelectedMode == BusinessObjectTransactionMode.UpdateObject`.
- Captured draft keeps the selected BO id.
- Captured draft state changes when form values change.
- Targeted tests pass.

## Proof Required

- Failing-first or red-team transcript proving no edit/update action existed before the production change or that create-mode staging would fail the invariant.
- Passing transcript for targeted edit/update tests.
- Source assertion transcript proving wizard reuse and callback wiring in production files.
- Anti-stub audit transcript covering production TODO, NotImplemented, fixture-specific branching, and template-only output in changed files.
- `proof/SB02/manifest.md` with changed-file hashes and portable proof references.
- `proof/SB02/semantic-invariants.md` covering edit wizard reuse and update-mode staging.
- Playwright proof for BO details Edit dialog open state and update staging affordance.
- Final proof manifest: `bundle://proof/SB02/manifest.md`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Docker browser closure: `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt`

## Browser Validation Logging

- Route: AccountApp Gallery/BO details from `/`.
- Viewport: large desktop first; narrower dialog check if the edit dialog layout wraps.
- Actions: open BO details, click Edit, verify wizard form fields are visible and prefilled, change at least one field, stage update.
- Screenshots: `bundle://proof/SB02/browser/bo-edit-dialog-prefilled.png` and `bundle://proof/SB02/browser/bo-edit-stage-update.png`.
- Review questions: dialog content readable, no clipping, action buttons visible, form hierarchy matches mint wizard, no scanner action returned in Actions.

## Progression Gate

- Completed: tests/source proof show edit uses the mint wizard and stages an update-mode draft for the selected BO id, and SB03 Docker browser proof verified the final integrated app.

## Suggested Agent Prompt

```text
Implement SB02 only after SB01 passes. Add BO details Edit by extending the existing BusinessObjectCreationWizard into edit/update mode, wire the staged update through existing callbacks, add semantic tests, capture proof, and stop if the form is duplicated or a create-mode draft is staged.
```
