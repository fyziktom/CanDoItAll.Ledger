# Assumptions And Risks

## Assumptions

- AccountApp remains a Blazor WASM PWA and continues to use the existing economy workspace adapter.
- `CanDoItAll.Components.BaseLib` is the preferred UI component source for toolbar, dialogs, badges, tag editor, notification, tooltip, and icon action surfaces.
- `Net.Codecrete.QrCodeGenerator` is acceptable for generation because the WASM proof builds and renders.
- Browser storage can initially use localStorage through the existing cache abstraction, with IndexedDB left as an implementation option where volume or binary payloads require it.
- Large-screen validation can use a maximized or 1920x1080 browser pass first; mobile behavior is intentionally deferred.

## Critical Path Risks

- Shared QR components may need changes in the sibling `CanDoItAll.Components` repository and package/reference updates in this repository.
- ZXingBlazor camera scanning can be harder to prove in automated browser runs because camera permissions and device availability vary.
- Tag occurrence semantics can become noisy if unique/general counters are not keyed by BO identity and account address consistently.
- Address directory scope can expand quickly unless execution keeps external bookmarks, own subaccounts, cached profile BO, favorites, and tags in one normalized model.
- Replacing top cards with sticky toolbar behavior can regress account unlock/import/create flows if session state and notification timing are not tested.

## Validation Risks

- Screenshot-only proof is insufficient for settings backup, tag counts, or verification-driver correctness; semantic tests must inspect stored data and service behavior.
- QR reader proof may require mocked scan payload tests plus manual/browser camera proof notes if no physical camera is exposed.
- Visual validation must explicitly check for vertical scrolling reduction and no overlays/overflow, not only that the page renders.
- The workbook and traceability files must stay synchronized with subbundle gate results during execution.

## Reopen Triggers

- A later Blazor/WebAssembly build fails after adding `CanDoItAll.Components.QRCode`.
- The QR reader package cannot run in the AccountApp browser target.
- A screenshot shows the old post-login card stack still above the workspace.
- Tag badges lose color consistency or readable contrast after reload.
- Address manager cannot serve both external bookmarks and own subaccounts through the same component contract.
- Settings import can corrupt or partially overwrite account data without preview/confirmation.
