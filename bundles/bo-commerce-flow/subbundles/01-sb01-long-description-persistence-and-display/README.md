# SB01 Long Description Persistence And Display

## Status

- `Completed`

## Objective

Make BO long description/body text reliable end to end: wizard input, payload serialization, typed parsing, details rendering, and raw-state visibility.

## Covered Inputs

- `RN-01`, `REQ-001`

## Prerequisites

- Bundle prepared-stage validator passes.
- Current wizard/details behavior from `analysis/01-current-state.md` is reviewed.

## Exact Source References

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectFamilyCatalog.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor`
- `repo://tests/CanDoItAll.Ledger.Tests`

## Deliverables

- Long body text is explicitly rendered in BO details even when short description exists.
- Raw state tab exposes the serialized `text` field for media payloads.
- Wizard binding and model reset behavior are corrected if tests/browser proof show capture failure.
- Unit/component tests cover create payload, parse payload, and details display logic.

## Dependency Impact

- Commerce records rely on payload text/fields being visible and trustworthy.
- If this phase is weak, payment/receipt details can appear to pass while hiding data.
- Observed local object for validation: `bo:post:new-ledger-post` / `2f9109c552bd0679b84e94a93fbbb9eb51e370436b97ad6c0b3ff840d70ddedb`.

## Validation Depth

- `Critical UI foundation`
- Requires Semantic Adequacy Gate proof and artifact-backed manifest.

## Implementation Steps

1. Add a failing-first test or captured assertion proving long text is hidden or absent for the reported path.
2. Fix the smallest source path: binding, payload builder, typed view, details rendering, or raw-state formatting.
3. Add tests for long text with a non-empty short description.
4. Run targeted tests and a browser proof against AccountApp details/raw state.

## Scope Exceptions

- Does not implement commerce sale/buy behavior.

## Do Not Do

- Do not replace the BO payload format.
- Do not remove short description.
- Do not hard-code the reported Alice object id as production logic.

## Acceptance Checklist

- Long body entered in wizard survives staged create and node reload.
- Details overview shows short description and long body separately.
- Raw state contains the long body text.
- Existing image/post/gallery rendering remains intact.

## Proof Required

- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- Failing-first transcript or explicit exemption if local data cannot be reproduced.
- Passing transcript for targeted tests.
- Changed-file hashes.
- Source assertion transcript for no hard-coded object id.

## Browser Validation Logging

- Route: AccountApp home and BO details dialog.
- Viewport: `1600x900` minimum.

## Closure

- Closure decision: `Passed`
- Manifest: `bundle://proof/SB01/manifest.md`
- Semantic invariants: `bundle://proof/SB01/semantic-invariants.md`
- Notes: The reported Alice BO already persisted `stateJson.text`; the implementation fixes details overview rendering and adds component/payload coverage so long body text is visible with a non-empty short description.
- Actions: unlock/import Alice, open reported or newly created post, open details, inspect overview and raw state.
- Screenshots: `bundle://proof/SB01/browser/long-description-details.png`, `bundle://proof/SB01/browser/long-description-raw.png`.
- Review questions: body readable, no clipping, raw JSON visible, no overlap in dialog tabs.

## Progression Gate

- `SB02` may start only after long text is proven in source, tests, and browser/raw state.

## Suggested Agent Prompt

```text
Implement SB01 only. Prove BO long description round-trip and display before touching commerce code.
```
