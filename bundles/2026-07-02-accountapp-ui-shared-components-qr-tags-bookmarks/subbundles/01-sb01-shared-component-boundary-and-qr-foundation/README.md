# SB01 Shared Component Boundary And QR Foundation

## Status

- `Completed`

## Objective

Create the shared QR component foundation and confirm the component ownership boundary between `CanDoItAll.Components`, `CanDoItAll.Economy.Components`, and AccountApp.

## Success Criteria

- `CanDoItAll.Components.QRCode` exists in the components repository and builds.
- QR generation uses the WASM-proven `Net.Codecrete.QrCodeGenerator` package.
- Scanner abstractions/components use ZXingBlazor or a documented equivalent if execution finds a blocking issue.
- AccountApp/economy components can consume shared QR components without local QR rendering code.
- The component boundary document or source comments make clear which project owns generic QR UI versus ledger payload semantics.

## Covered Inputs

- QR WASM feasibility request.
- New shared `CanDoItAll.Components.QRCode` project request.
- Address, transaction, ownership, and verification QR reuse request.
- Prepared component reuse request.

## Prerequisites

- `bundle://evidence/qr-wasm-test/qrcode-wasm-cli-render.png`
- `bundle://evidence/qr-wasm-test/QrCodeWasmTest.csproj`
- `bundle://evidence/qr-wasm-test/Home.razor`

## Exact Source References

- `repo://src/CanDoItAll.Economy.Components/CanDoItAll.Economy.Components.csproj`
- `repo://examples/CanDoItAll.Economy.AccountApp/CanDoItAll.Economy.AccountApp.csproj`
- `bundle://evidence/qr-wasm-test/Home.razor`
- `bundle://evidence/qr-wasm-test/QrCodeWasmTest.csproj`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Feedback\TooltipTarget.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Feedback\Notification.razor`
- `C:\repositories\CanDoItAll.Components\src\CanDoItAll.Components.BaseLib\Components\Modals\DialogScaffold.razor`

## Deliverables

- Shared QR project and project references.
- Generic QR rendering service and payload model.
- QR button/dialog components with tooltip-ready action API.
- Scanner button/dialog component contracts.
- Minimal demo or test host proving QR generation renders in Blazor.
- Build/test proof copied into the execution report.

## Dependency Impact

- SB02 uses QR action buttons in the top identity and address surfaces.
- SB06 depends on QR scanning/generation contracts for verification.
- SB07 cannot close without this shared foundation compiling in the full solution.

## Validation Depth

- `Critical foundation`
- Requires build proof, component-level proof, and browser render proof.

## Implementation Steps

1. Add `CanDoItAll.Components.QRCode` to the components repository using existing component project conventions.
2. Add `Net.Codecrete.QrCodeGenerator` for QR generation.
3. Add ZXingBlazor scanner dependency and wrap it behind a generic scan result model.
4. Implement QR render service, QR dialog, QR action button, scan dialog, and scan action button.
5. Add examples/tests for address, tx hash, BO ownership hash, and generic text payloads.
6. Add or update project references/packages so this repository can consume the shared project.
7. Build the components repo and this ledger repo.
8. Capture browser render proof for at least one QR payload through a consuming Blazor surface.

## Scope Exceptions

- Ledger-specific verification flow is SB06.
- AccountApp shell placement is SB02.
- Camera-device manual QA can be deferred to SB06/SB07 if CI/browser automation cannot expose a real camera.

## Do Not Do

- Do not put QR rendering directly in AccountApp page components.
- Do not couple the shared QR project to ledger-specific business object types.
- Do not replace BaseLib dialog, tooltip, or button primitives with bespoke equivalents.

## Acceptance Checklist

- `CanDoItAll.Components.QRCode` builds.
- QR SVG output is deterministic enough for snapshot or structural tests.
- QR dialog exposes copy, close, payload label, and accessible title.
- Scanner contract can return decoded text and validation errors.
- Ledger repo builds against the new shared component reference.

## Proof Required

- `dotnet build` for the components repository.
- `dotnet build` for `repo://CanDoItAll.Ledger.slnx`.
- Screenshot of a rendered QR dialog in a Blazor browser surface.
- Test or demo proof for QR payload kinds.
- Execution report row update for SB01.

## Browser Validation Logging

- Route: QR demo or AccountApp route exposing a QR dialog.
- Viewport: large screen, minimum 1600px wide.
- Actions: open QR dialog, verify SVG visible, copy payload, close dialog.
- Screenshots: `bundle://evidence/SB01/qr-dialog-large.png`.
- Review questions: Is the QR dialog readable, unclipped, and consistent with BaseLib dialog styling?

## Progression Gate

- Downstream QR-dependent work may start only after shared project build proof and one browser QR dialog screenshot are recorded.

## Suggested Agent Prompt

```text
Implement SB01 only. Build the shared QR project and the ledger solution, capture QR render proof, update the execution report, and stop if the shared component boundary cannot compile cleanly.
```

