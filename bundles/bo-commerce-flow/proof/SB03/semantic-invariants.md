# SB03 Semantic Invariants

## Invariants

1. Listing a BO for sale must update `commerce.sale`, not only UI-only price text.
2. Copy sales must expose and persist a positive `MaxClones`.
3. A shareable Buy BO route must load listing data from the node API by object id.
4. The buyer payment flow must use the browser account key and submit signed transactions through the node API.
5. A successful buy action creates a pending payment BO linked to buyer, seller, target BO, amount, and sale mode.
6. Payment, purchase, and receipt BOs must be visible through the Account dashboard and openable in a detail dialog.
7. UI proof must show real dialog/page states, not only API assertions.
8. SB03 must not complete seller dispatch; pending payment is the handoff contract for SB04.

## Verification

| Invariant | Verification artifact |
| --- | --- |
| `1` | `bundle://proof/SB03/transcripts/playwright-alice-set-sale-final-step.txt` and `bundle://proof/SB03/transcripts/api-sale-payment-state.json`. |
| `2` | Component tests and API state show `maxClones=5` for a copy sale. |
| `3` | `bundle://proof/SB03/transcripts/playwright-bob-buy-bo-final-step.txt` opened the shared route and loaded the listing. |
| `4` | Source assertions and Bob browser proof show AccountApp unlock/import plus signed payment submission. |
| `5` | API state and Bob browser proof show `bo:payment:ca99382ba95385cbdc548158` with pending commerce metadata. |
| `6` | `bundle://proof/SB03/transcripts/playwright-alice-commerce-dashboard.txt` and dashboard screenshots. |
| `7` | Six browser screenshots under `bundle://proof/SB03/browser/`. |
| `8` | Manifest scope notes and raw API state show payment status still pending. |

## Visual Review Answers

- Does the Set Sale dialog expose mode, price, max clones, clear sale, and share link? `Yes`.
- Does the gallery show sale state without clipping at desktop width? `Yes`.
- Does the Buy BO page show the listing and allow Bob to submit payment after vault unlock/import? `Yes`.
- Does Alice see a pending commerce dashboard row with detail click-through? `Yes`.
- Is dispatch intentionally absent from SB03? `Yes`.
