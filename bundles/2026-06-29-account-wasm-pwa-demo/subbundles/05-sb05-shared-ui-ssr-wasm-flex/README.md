# SB05 Shared UI SSR WASM Flex

## Status

- `Closed`

## Objective

- Decouple reusable ledger account components from SSR-only sessions and wire them through host-neutral models/actions so the same UI can run in SSR demos and AccountApp WASM.

## Success Criteria

- Existing account workspace, transaction history, UTXO explorer, and composer surfaces can be driven by SSR and WASM adapters.
- SSR node/component demo behavior is preserved.
- WASM adapter uses SB02 account session, SB03 API client, and SB04 transaction actions.
- Components do not directly depend on server-side key loading or in-memory stores when used by AccountApp.

## Covered Inputs

- RQ09, RQ10, RQ19, RQ22, RQ23, RQ24, RQ25, RQ26, RQ30.
- Architect note that many Ledger Razor components already exist but may be connected too tightly to SSR.

## Prerequisites

- SB02 progression gate passed.
- SB03 progression gate passed.
- SB04 progression gate passed.

## Exact Source References

- repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor
- repo://src/CanDoItAll.Economy.Components/Components/Transactions
- repo://src/CanDoItAll.Economy.Components/Components/Account/UtxoExplorer.razor
- repo://src/CanDoItAll.Economy.Components/Components/Account/AccountBackupPanel.razor
- repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs
- repo://src/CanDoItAll.Economy.Components/Services/EconomyDevelopmentWorkspaceSession.cs
- repo://examples/CanDoItAll.Economy.Components.Demo
- bundle://architecture/02-source-reference-map.md

## Deliverables

- Host-neutral account workspace interfaces/models/actions.
- SSR adapter preserving existing node/development demo behavior.
- WASM adapter consuming SB02/SB03/SB04 services.
- Functional backup/import/account setup UI integration where existing visual-only components are reused or replaced intentionally.
- Component tests and browser render checks for SSR and WASM host modes.

## Dependency Impact

- SB06 relies on these adapters to build the PWA without duplicating ledger UI.
- SB07 uses this proof to close shared component SSR/WASM flexibility and regression requirements.

## Validation Depth

- UI, component-test, and browser-proof.

## Implementation Steps

1. Inventory direct service dependencies in reusable components.
2. Define host-neutral state/action interfaces that cover current workspace behavior.
3. Adapt SSR sessions to the new interfaces without changing behavior.
4. Add WASM adapter that consumes account session, API client, and transaction actions.
5. Update components to depend on parameters/interfaces rather than concrete SSR sessions.
6. Add component tests and browser render tests for SSR and WASM contexts.
7. Update workbook rows, execution report, and proof manifest.

## Scope Exceptions

- Do not redesign the ledger UI outside the minimum needed for host neutrality.
- Do not add unrelated marketing/landing pages.

## Do Not Do

- Do not couple shared components to browser-only services.
- Do not remove SSR demo/session support.
- Do not hide missing actions behind disabled placeholders without a tracked scope exception.

## Acceptance Checklist

- Existing SSR demo renders account workspace and transaction components.
- WASM host renders the same core workspace surfaces with AccountApp services.
- Backup/import actions are functional or explicitly owned by SB02 components.
- Component tests cover key callbacks and adapter wiring.
- No direct disk-key loading is reachable from WASM UI paths.

## Proof Required

- `proof/SB05/manifest.md` with command transcript links and changed-file hashes.
- Component test output for adapter-driven UI.
- Browser screenshots for SSR and WASM render paths.
- Dependency audit artifact showing removed/isolated SSR-only service references.
- Negative check proving WASM UI cannot resolve `EconomyNodeWorkspaceSession` for account signing.

## Browser Validation Logging

- Target routes: SSR component demo route and AccountApp/WASM host route for account workspace.
- Viewports: desktop 1440x1000, tablet 900x900, mobile 390x844 for affected layouts.
- Actions: render workspace, load transaction history, open UTXO explorer, open composer, open account setup/backup flow.
- Screenshots/evidence: `proof/SB05/ssr-workspace.png`, `proof/SB05/wasm-workspace.png`, `proof/SB05/mobile-workspace.png`.
- Review questions: Are controls usable without overlap, and are SSR/WASM adapters visibly driving the same component surfaces?

## Progression Gate

- Passed in `proof/SB05/manifest.md`. SB06 may proceed using the shared adapter host and WASM account workspace adapter.

## Suggested Agent Prompt

```text
Implement SB05 only. Make existing ledger components host-neutral across SSR and WASM, preserve SSR behavior, wire WASM adapters to prior subbundle services, and prove rendering plus dependency isolation.
```
