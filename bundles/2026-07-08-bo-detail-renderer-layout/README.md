# BO Detail Renderer Layout System

Status: Implemented and validated

## Raw Request

User asked on 2026-07-08 to use the CanDoItAll bundle workflow, C# architecture governance, image generation, and other C# skills to prepare and execute a bundle for improved UI components in ledger-related apps.

The main goal is a good and flexible display system for BO data. Some BOs have common fields and some have specific behavior, so the existing Ledger Components renderers should be improved. Missing BO types include events, tickets, payments, receipts, purchases, and related commerce objects.

The first tab in the BO detail dialog must automatically layout better. If a BO has no DataItems, the dialog must not use an empty DataItems gallery as the hero. It should hide that empty gallery and show a visually strong layout of the other BO information instead. If the BO is related to another BO, such as a ticket related to an event or a purchase/receipt related to the BO that was bought or delivered, the related BO should be shown with a thumbnail/card using the same card as the BO gallery page.

Use image generation to create a visual proposal. Provide ImageGen the fields needed, a description of the actual gallery card, and a description of the current BO detail dialog with image gallery. Then implement, test with screenshots, compare against the proposal, avoid custom CSS, prefer BaseLib components and Tailwind styling, target large screens only, then rebuild dockered apps and test there.

## Profile

Initiative. This touches shared Blazor components, renderer extension architecture, dialog layout behavior, and browser/docker validation.

## Source Inputs

- `bundle://inputs/raw-request.md`
- `bundle://inputs/imagegen-prompt.md`
- `bundle://evidence/imagegen/bo-detail-layout-proposal.png`
- `bundle://evidence/references/accountapp-gallery.png`
- `bundle://evidence/references/accountapp-bo-details-fixed.png`

## Scope

- Extract or reuse a single BO gallery-card component so gallery and related-object previews share markup.
- Improve `BusinessObjectLedgerDetailsDialogContent` Overview behavior for large-screen dialogs.
- Hide DataItems hero and DataItems rail when no DataItems exist.
- Render a data-first overview for typed BOs with no DataItems.
- Show related BO cards for ticket-event and commerce target/delivered object relationships when the related BO is present in the current `BusinessObjectWorkspaceModel`.
- Add default renderer/provider support for family/typed and commerce BOs where useful, without breaking external custom renderer providers.
- Keep existing media BOs with DataItems on the gallery-first path.
- Add component tests and large-screen browser screenshots.
- Rebuild Docker AccountApp/node images and validate the rebuilt AccountApp UI.

## Hard Constraints

- Do not create new custom CSS for this work.
- Use BaseLib components (`Grid`, `Stack`, existing panels/facts/badges/buttons) and existing utility/classes already present in the component library.
- Large-screen validation only; do not spend scope on small or medium responsive behavior.
- Do not weaken hidden-gallery behavior for payment/purchase/receipt records on the main gallery.
- Do not introduce a new project reference or split unless implementation proves it is required.

## Validation Summary

- Readiness validator script is unavailable in this repository: `repo://scripts/validate_bundle.py` does not exist.
- CodeAnalytics MCP snapshot was attempted and failed because the tool process resolved `C:\repositories\CanDoItAll\global.json` and could not find the requested .NET SDK. Manual source inspection and normal build/test/browser proof must cover the gap.
- Focused layout/renderer tests passed: `7/7`.
- Adjacent component tests passed: `27/27`.
- AccountApp build passed.
- Large-screen Playwright screenshots were captured for the local demo validation route and Docker AccountApp.
- Docker compose rebuild and smoke test passed for `economy-node`, `demo-seeder`, `accountapp-alice`, `accountapp-bob`, and `accountapp-lab`.
- Final manual gate result: closed with dependency warnings and CodeAnalytics tooling gap recorded.

## Bundle Files

- Requirements: `bundle://requirements/01-normalized-requirements.md`
- Current state: `bundle://analysis/01-current-state.md`
- Risks: `bundle://analysis/02-assumptions-and-risks.md`
- Type inventory: `bundle://inventories/01-bo-type-renderer-inventory.md`
- Phase plan: `bundle://plan/01-phase-plan.md`
- Architecture checkpoints: `bundle://plan/architecture-checkpoints.md`
- Traceability: `bundle://traceability/01-input-coverage.md`
- Execution report: `bundle://reviews/01-execution-report.md`
