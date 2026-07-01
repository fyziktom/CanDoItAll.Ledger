# SB03 Proof Manifest

## Status

- Subbundle: `SB03 Sell Share Buy UI`
- Closure decision: `Passed`
- Closed at: `2026-06-30`
- Owned requirements: `REQ-004`, `REQ-005`, `REQ-008`, `REQ-009`
- Owned raw notes: `RN-03`, `RN-04`, `RN-06`, `RN-07`

## Production Changes

- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectSetPriceDialogContent.razor`
  - Replaced the simple price entry with a Set Sale dialog that supports original/copy mode, price, max clones, clear sale, and share-path display.
- `repo://src/CanDoItAll.Economy.Components/Services/EconomyNodeWorkspaceSession.cs`
  - Persists sale terms by updating structured `commerce.sale` metadata through the node API transaction flow.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor`
  - Adds visible sale badges in gallery cards.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor`
  - Adds sale facts, buy link, and long-description rendering in the details dialog.
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/BuyBo.razor`
  - Adds `/buy-bo/{objectId}` route that loads a listed BO through the node API, unlocks/imports a browser vault, and submits a browser-signed payment BO.
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCommerceDashboard.razor`
  - Adds dashboard rows and click-through details for payment, purchase, and receipt BOs.
- `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectCommerceUiComponentTests.cs`
  - Adds component coverage for sale display, request shaping, and commerce dashboard rendering.

## Evidence

| Evidence | Path | Result |
| --- | --- | --- |
| Targeted component/contract tests | `bundle://proof/SB03/transcripts/commerce-ui-component-tests.txt` | `Passed!` 10 tests covering SB01-SB03 behavior. |
| AccountApp build | `bundle://proof/SB03/transcripts/accountapp-build.txt` | AccountApp built successfully with the Buy BO route and shared UI. |
| Docker image rebuild | `bundle://proof/SB03/transcripts/docker-accountapp-build.txt` | AccountApp image rebuilt; transcript ends with `Image candoitall-ledger-accountapp:dev Built`. |
| Docker app recreate | `bundle://proof/SB03/transcripts/docker-accountapp-recreate.txt`, `bundle://proof/SB03/transcripts/docker-accountapp-ps.txt` | Alice, Bob, and Lab hosted WASM apps recreated on ports `5081`, `5082`, and `5083`. |
| Alice Set Sale browser proof | `bundle://proof/SB03/transcripts/playwright-alice-set-sale-final-step.txt` | Listed `bo:post:new-ledger-post` as copy sale with `7.25 MAIN` and `maxClones=5`. |
| Bob Buy BO browser proof | `bundle://proof/SB03/transcripts/playwright-bob-buy-bo-final-step.txt` | Bob submitted payment BO `bo:payment:ca99382ba95385cbdc548158`. |
| Alice commerce dashboard proof | `bundle://proof/SB03/transcripts/playwright-alice-commerce-dashboard.txt` | Alice dashboard displayed pending payment row and opened detail dialog. |
| Raw node state assertion | `bundle://proof/SB03/transcripts/api-sale-payment-state.json` | Node state contains listed sale terms and Bob's pending payment metadata. |
| Browser screenshots | `bundle://proof/SB03/browser/alice-set-sale-dialog.png`, `bundle://proof/SB03/browser/alice-sale-badge-gallery.png`, `bundle://proof/SB03/browser/bob-buy-bo-ready.png`, `bundle://proof/SB03/browser/bob-buy-bo-payment-submitted.png`, `bundle://proof/SB03/browser/alice-commerce-dashboard-payment.png`, `bundle://proof/SB03/browser/alice-commerce-payment-detail.png` | Captured at desktop viewport during the production UI flow. |
| Source assertions | `bundle://proof/SB03/transcripts/source-assertions.txt` | Shows Set Sale, Buy BO, dashboard selectors, browser signing, and node API submission points. |
| Anti-stub audit | `bundle://proof/SB03/transcripts/anti-stub-audit.txt` | Only expected guard exceptions were found; no VEFramework dependency, reported object hash, or hard-coded validation shortcut was found. |
| Changed-file hashes | `bundle://proof/SB03/transcripts/changed-file-hashes.txt` | SHA-256 recorded for changed SB03 files. |

## Production Behavior Artifact Matrix

| Behavior | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Sale terms can be set from production UI and persisted as structured commerce metadata. | Set Sale dialog component test and Alice browser proof. | Gallery/details sale badges consume `commerce.sale`. | API state shows price, mode, max clones, seller, and share path after broadcast. | Source audit shows no reported-object hard-code. |
| Shared Buy BO route loads listing data through the node API and signs only in the browser. | Buy BO source assertions and AccountApp build. | Bob browser proof opened `localhost:5082/buy-bo/...` and submitted a payment. | API state shows payment BO owned by seller with buyer/seller/target/price links. | Token-priced purchases are rejected with a guard instead of a fake success path. |
| Commerce dashboard exposes pending payment records with detail click-through. | Component test renders dashboard rows. | Alice browser proof shows payment row and detail dialog. | Pending payment from Bob is visible after Alice refresh/navigation. | Dashboard is data-driven from workspace BOs, not from seeded UI state. |

## Closure Notes

- SB03 intentionally stops at a pending payment BO. Seller-side dispatch, clone/original delivery, purchase/receipt finalization, and sale count updates are owned by `SB04`.
- The browser proof used the already-running Docker node and hosted WASM apps, not mocked API data.
