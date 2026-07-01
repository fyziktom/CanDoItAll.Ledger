# SB05 Proof Manifest

## Status

- Subbundle: `SB05 Alice Bob Browser Validation`
- Closure decision: `Passed`
- Closed at: `2026-07-01`
- Owned requirements: `REQ-001` through `REQ-009`
- Owned raw notes: `RN-01` through `RN-07`

## Evidence

- Failing-first transcript: N/A process validation only; SB05 adds no production behavior and relies on SB04 failing-first proof.
- Passing transcript: `bundle://proof/SB05/transcripts/playwright-bob-original-verify.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Semantic invariants: `bundle://proof/SB05/semantic-invariants.md`
- Changed-file SHA-256 anchor: SB05 is validation-only; SB04 dispatcher hash `92C31520B4A9E854D05D126CBD371FF07F2E4A602567716F86F82D4F4629BCD5`.

| Evidence | Path | Result |
| --- | --- | --- |
| Final Docker state | `bundle://proof/SB05/transcripts/docker-final-ps.txt` | Node, Postgres, Alice, Bob, and Lab containers running. |
| Copy-sale seller dispatch | `bundle://proof/SB04/transcripts/playwright-alice-dispatch-refresh.txt` | Alice refresh dispatched Bob's copy purchase and created receipt/purchase. |
| Copy-sale raw state | `bundle://proof/SB04/transcripts/api-dispatch-state.json` | Clone owner is Bob; Alice original sale remains listed with `soldCount=1`. |
| Original sale flow transcript | `bundle://proof/SB05/transcripts/playwright-original-sale-e2e-rerun.txt` | Captured Alice original sale setup and Bob buy screenshots; run was stopped after final verification got stuck, but node state shows the signed flow completed. |
| Original seller dashboard | `bundle://proof/SB05/transcripts/playwright-alice-original-dashboard.txt` | Alice sees dispatched payment and receipt rows for `bo:post:purchase-order-post`. |
| Original buyer dashboard/gallery | `bundle://proof/SB05/transcripts/playwright-bob-original-verify.txt` | Bob sees dispatched purchase row and owns the original BO in gallery. |
| Original raw state | `bundle://proof/SB05/transcripts/api-original-sale-state.json` | Original BO owner is Bob, sale is sold, payment is dispatched, purchase owner is Bob, receipt owner is Alice. |
| Browser result summary | `bundle://proof/SB05/transcripts/browser-result-summary.txt` | Summarizes successful SB04/SB05 Playwright result rows. |
| Browser screenshot inventory | `bundle://proof/SB05/transcripts/browser-screenshot-inventory.txt` | Lists SB04/SB05 screenshots and sizes. |
| Focused regression tests | `bundle://proof/SB04/transcripts/dispatch-focused-tests.txt` | `Passed!` 12 commerce tests. |
| AccountApp build and Docker image | `bundle://proof/SB04/transcripts/accountapp-build.txt`, `bundle://proof/SB04/transcripts/docker-accountapp-build.txt` | Build/image succeeded. |

## Browser Screenshots

- `bundle://proof/SB01/browser/long-description-details.png`
- `bundle://proof/SB01/browser/long-description-raw.png`
- `bundle://proof/SB03/browser/alice-set-sale-dialog.png`
- `bundle://proof/SB03/browser/bob-buy-bo-ready.png`
- `bundle://proof/SB03/browser/bob-buy-bo-payment-submitted.png`
- `bundle://proof/SB04/browser/seller-dispatch-refresh.png`
- `bundle://proof/SB04/browser/seller-payment-dispatched-detail.png`
- `bundle://proof/SB04/browser/seller-receipt-detail.png`
- `bundle://proof/SB05/browser/alice-original-sale-dialog.png`
- `bundle://proof/SB05/browser/bob-original-buy-ready.png`
- `bundle://proof/SB05/browser/bob-original-buy-submitted.png`
- `bundle://proof/SB05/browser/alice-original-dispatch-dashboard.png`
- `bundle://proof/SB05/browser/bob-original-purchase-dashboard.png`
- `bundle://proof/SB05/browser/bob-original-bo-gallery.png`

## Raw Note Closure

| Raw note | Closure |
| --- | --- |
| `RN-01` | Solved by SB01 long-description UI/raw-state proof. |
| `RN-02` | Solved by typed payment/purchase/receipt contracts and automated dispatch records. |
| `RN-03` | Solved by Set Sale UI for copy/original modes and raw sale state. |
| `RN-04` | Solved by Buy BO route and Bob browser-signed payment proof. |
| `RN-05` | Solved by seller refresh dispatch for copy and original sales. |
| `RN-06` | Solved by Alice/Bob dashboard rows and details for payment, receipt, and purchase records. |
| `RN-07` | Solved by CanDoItAll-owned contracts/services/tests and source audits showing no VEFramework dependency. |

## Closure Notes

- The original-sale full script was intentionally split after the final verification phase got stuck in Playwright. The signed sale/buy/dispatch had already completed, and separate Alice/Bob verification scripts plus raw API state closed the flow.
- All local proof used the Docker-hosted apps and node API with persisted Postgres state.
