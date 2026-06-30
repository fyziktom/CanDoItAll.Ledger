# SB03 BO Wizard And Gallery UX Coverage

## Status

- Status: Completed

## Objective

Connect the generic BO schema to the creation wizard, gallery cards, renderer host, and details dialogs so every in-scope family can be created with correct type-specific data and shown clearly.

## Covered Inputs

- RN-02
- RN-03
- RN-05
- REQ-03
- REQ-05
- REQ-06

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed.
- The component MCP lookup failure is recorded, so local shared component patterns are the UI source of truth.

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectCreationWizardModel.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Rendering/DefaultBusinessObjectRendererRegistry.cs`
- `repo://src/CanDoItAll.Economy.Components/Rendering/MediaBusinessObjectRendererProvider.cs`
- `repo://tests/CanDoItAll.Ledger.Tests/AccountWorkspaceAdapterTests.cs`

## Deliverables

- Wizard type selector/templates driven by the shared BO catalog.
- Type-specific fields that update the staged BO payload.
- Gallery cards and detail dialogs that show family-specific facts for every in-scope type.
- Attachment preview handling for IPFS-backed images and other data items.
- Component tests for rendering all in-scope families.

## Dependency Impact

- SB04 relies on this UI to create real browser transactions and verify Alice/Bob states.
- Weak UI proof here invalidates final browser proof because user workflows might still be fixture-only.

## Validation Depth

- Critical UI foundation
- Component tests
- Large-screen browser proof
- Dialog/open-state screenshot review

## Implementation Steps

1. Update wizard model and UI to use the BO catalog.
2. Fix any existing input binding gaps so typed fields enter staged state JSON.
3. Extend gallery/details/renderers to show typed facts and attachments from parsed BO views.
4. Add component tests or bUnit coverage for all in-scope families.
5. Use Playwright on a real app route to capture wizard, gallery, detail dialog, and attachment preview states.

## Scope Exceptions

- SB03 may use local/demo/test data for UI coverage, but SB04 must prove real signed Alice/Bob transactions.

## Do Not Do

- Do not create a marketing page or alternate app flow.
- Do not use nested card-heavy layouts beyond existing repeated gallery cards/dialogs.
- Do not add domain-specific VEFramework forms.

## Acceptance Checklist

- [x] Wizard can select/create each in-scope generic BO family.
- [x] Typed fields are included in staged state JSON.
- [x] Gallery shows correct title, description, type badge, tags, and family facts.
- [x] Details dialog shows attachments and type-specific fields.
- [x] Dialogs are readable, unclipped, and layered correctly in Playwright screenshots.

## Proof Required

- `proof/SB03/transcripts/01-components-tests.txt`
- `proof/SB03/transcripts/02-accountapp-build.txt`
- `proof/SB03/transcripts/03-playwright-wizard-gallery.txt`
- `proof/SB03/transcripts/04-anti-stub-audit.txt`
- Screenshots under `proof/SB03/*.png`
- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`

## Browser Validation Logging

- Route: AccountApp unlocked workspace or component demo route that hosts the same shared components.
- Viewport: 1920x1080 first pass.
- Required actions: open wizard, select each type, fill fields, preview/stage, open gallery details, inspect attachment preview.
- Screenshots: wizard type selector, detail dialog, gallery matrix, IPFS attachment preview.
- Review questions: text fits, dialogs not clipped, card data is type-specific, images/media render, and no unsupported domain types appear.

## Progression Gate

- SB04 may start only after browser/component proof shows every in-scope family renders correctly through the shared UI.

## Suggested Agent Prompt

```text
Implement SB03 only. Wire the shared BO schema into the wizard/gallery/details UI, add tests and browser proof, and stop if any in-scope family is missing from the visible creation/gallery path.
```
