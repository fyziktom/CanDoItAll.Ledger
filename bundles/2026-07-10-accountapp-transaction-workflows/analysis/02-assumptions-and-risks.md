# Assumptions And Risks

## Assumptions

- `BusinessObjectWorkspaceModel.BusinessObjects` is the authoritative preload for BOs visible to the current account.
- Address-scoped and direct-id lookup can begin with already loaded workspace objects and present a clear unavailable/not-found state; no speculative node API will be added without an existing lookup contract.
- `CreateRuleToken` remains covered by `TokenCreationDialogContent` because composer submission does not implement that enum member.
- Existing color, typography, DialogService, and BaseLib conventions remain authoritative.

## Critical Path Risks

- The flat draft record may not carry enough flow-state for origin choice or review without UI-local state. Persist only transaction semantics in the draft; keep transient wizard state local to the form.
- Reusing `BusinessObjectGalleryCard` directly may make a picker too large; a compact transaction-specific wrapper should reuse renderer metadata without duplicating domain display rules.
- The adapter has no `CreateRuleToken` switch branch; surfacing it in the composer without delegation would be a broken mode.
- BOs from other addresses may not be loaded in `Workspace.BusinessObjects`. The UI must distinguish `no loaded matches` from a successful remote lookup.
- Docker rebuilds are slower than the absent managed watch loop; component tests must carry most iteration, followed by one rebuilt browser pass.

## Validation Risks

- A mode that renders is not necessarily usable. Each mode needs at least one valid and one invalid interaction path.
- Empty accounts cannot prove selectors with data. BUnit sample workspace proof must cover populated cases; AccountApp must prove empty-state behavior and any seeded populated paths available after reseeding.
- Image-generated text may be inaccurate; proposals define layout direction only.
- CodeAnalytics is unavailable for this repo in the current MCP process; zero-project output must not be treated as a clean architecture result.

## Reopen Triggers

- A new project reference, partial class, or service locator is proposed.
- Existing UTXO eligibility or submission shape changes.
- A supposedly selectable BO mode still requires manually retyping an already loaded id/state.
- A visible mode can reach Submit while its domain request would be rejected for missing/invalid inputs.
- Desktop success hides clipping or unusable layout at tablet/mobile widths.

