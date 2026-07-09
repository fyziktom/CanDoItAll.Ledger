# Assumptions And Risks

## Assumptions

- Large desktop is the only required responsive target for this request.
- Existing AccountApp cryptography and vault storage behavior should remain intact except for adding a deliberate local reset.
- The reset/delete flow is scoped to the local browser vault and in-memory AccountApp session state; it does not delete ledger history or remote node data.
- BaseLib CSS is already loaded by AccountApp through `AddCanDoItAllBaseLib` and `_Imports.razor`.

## Critical Path Risks

- If top-bar removal also removes the only accessible actions for backup/export/refresh, users may lose important account controls. The shared workspace shell must still expose needed account-level actions or replacement controls must be retained inside the workspace.
- Reset must not be a single-click destructive operation. The typed confirmation and acknowledgement gate are critical.
- Import steps can accidentally hide required inputs or enable finish too early. Step validation must reflect selected import mode and password confirmation.

## Validation Risks

- Browser vault state persists in IndexedDB across runs, so Playwright proof must create isolated browser contexts or explicitly clear storage.
- WASM startup depends on the node API. Browser proof should either use the docker/local node stack or validate locked UI independently when node data is unavailable.
- Docker rebuild may surface unrelated image warnings or cached layers; transcript must still show the requested apps rebuild successfully.

## Reopen Triggers

- The unlocked workspace still shows an AccountApp top frame above the shared workspace after implementation.
- The import UI does not use BaseLib `Steps` or the source assertion cannot find it.
- Reset can be triggered without both acknowledgement and typed `RESET LOCAL VAULT`.
- Large-screen Playwright screenshot shows clipped labels, overlapping panels, or hidden destructive-state text.
- Docker compose build fails for an app image touched by this request.
