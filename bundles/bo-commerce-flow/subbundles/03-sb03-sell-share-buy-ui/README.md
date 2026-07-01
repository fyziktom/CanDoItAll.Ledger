# SB03 Sell Share Buy UI

## Status

- `Completed`

## Objective

Add shared UI and AccountApp route support for setting sale terms, copying a buy link, loading a listed BO by API, unlocking local account, and creating a signed buyer payment BO.

## Covered Inputs

- `RN-03`, `RN-04`, `RN-06`, `RN-07`
- `REQ-004`, `REQ-005`, `REQ-008`, `REQ-009`

## Prerequisites

- `SB02` contracts and parser tests pass.
- AccountApp/node configuration is known from existing docker setup.

## Exact Source References

- `repo://examples/CanDoItAll.Economy.AccountApp/App.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppSession.cs`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectSetPriceDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor`
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyAccountWorkspaceAdapters.cs`
- `repo://src/CanDoItAll.Economy.Sdk/EconomyNodeLedgerClient.cs`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Pages\BuyNFT.razor`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\NFTs\Actions\SetNFTPrice.razor`
- `C:\repositories\VirtualEconomyFramework\VirtualEconomyFramework\VEBlazor\Components\NFTs\Actions\ShareNFT.razor`

## Deliverables

- Set Sale dialog with sale mode, max clones, price, clear sale, and share link.
- Sale badges/facts in gallery and details.
- AccountApp Buy BO page/route that loads by object id from node API and submits a payment BO from buyer to seller.
- Dashboard panels or filters for commerce payments/receipts with detail click-through.
- SSR/WASM shared behavior where practical.

## Dependency Impact

- `SB04` depends on payment BOs produced by this UI.
- `SB05` depends on all user-visible flows.

## Validation Depth

- `Critical UI foundation`
- Requires browser proof, open-state dialog proof, and manifest.

## Implementation Steps

1. Extend the sale dialog and adapter request shape to use `SB02` contracts.
2. Add sale display and share link copy in gallery/details.
3. Add Buy BO route/page to AccountApp.
4. Add payment submit flow using browser signing and node broadcast.
5. Add dashboard payment/receipt panels or filters with click-through.
6. Add tests for request creation and UI-visible state where available.

## Scope Exceptions

- Dispatch completion is in `SB04`; this subbundle only creates pending payment.

## Do Not Do

- Do not perform server-side signing in AccountApp.
- Do not manually seed payments for positive browser proof.
- Do not hide unavailable token-priced purchases behind a fake success path.

## Acceptance Checklist

- [x] Alice can set original sale and copy sale with max clones from details/gallery.
- [x] Buy link opens a page that loads the BO through node API.
- [x] Bob can unlock/import local vault and submit payment BO.
- [x] Payment/receipt dashboard entries have detail click-through.
- [x] Dialogs are readable, unclipped, layered correctly, and use full large-screen width.

## Proof Required

- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/semantic-invariants.md`
- Passing tests/build transcript.
- Playwright screenshots for Set Sale dialog, Buy BO page, payment confirmation, dashboard panels, detail dialogs.
- Source assertions that AccountApp uses browser signing and node API only.

## Browser Validation Logging

- Routes: `/`, `/buy-bo/{objectId}` or implemented equivalent.
- Viewport: `1600x900` large desktop; narrower follow-up only if layout changes are responsive.
- Actions: open Set Sale, set copy/original modes, copy/open Buy link, unlock Bob, submit payment, open payment detail.
- Screenshots: `bundle://proof/SB03/browser/set-sale-dialog.png`, `buy-bo-page.png`, `payment-submitted.png`, `dashboard-commerce-panels.png`.
- Review questions: text fits, no overlay clipping, dialog layers above workspace, related BO detail opens correctly.

## Progression Gate

- `SB04` may start only after a pending payment BO can be created through production UI and verified by node query.

## Suggested Agent Prompt

```text
Implement SB03 only. Add sale/share/buy UI and buyer payment creation using the SB02 contracts; stop before seller dispatch automation.
```
