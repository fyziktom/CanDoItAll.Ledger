# Desktop Workbench Follow-up

## Final Environment

- AccountApp Lab: `http://localhost:5083/`
- Viewport used for inspection: 1860 x 900
- Send Transaction dialog: 1792 x 846
- Dialog working body: 1790 x 757
- Dataset: 100005 main, 1 token type, 3 BOs, 6 UTXOs, 13 transactions

## Measured Screens

| Screen | Measurement | Result |
|---|---|---|
| Type selector | body `clientHeight=757`, `scrollHeight=757`, `scrollTop=0`; no horizontal overflow | Pass |
| Create details | body `757/757`; editor 1390 px and context rail 352 px; repeated active-step heading hidden | Pass |
| Create review | body `757/757`; Submit enabled only after Review | Pass |
| Payment details | receiver, amount, and memo share one row; body `757/757` | Pass |
| BO picker | six approximately 283 px gallery columns; search, type and tag filters; sticky selection footer | Pass |
| Clone details | body `757/757`; source preview retained; advanced JSON bounded/collapsed | Pass |
| UTXO picker | two eligible plain outputs; outpoint-specific checkbox names; sticky selection action | Pass |
| UTXO split details | body `757/757`; two 660 x 105 px output cards on one row; split collection `105/105` | Pass |
| UTXO split review | mode-specific inputs, outputs, allocation, receiver facts; Submit becomes enabled | Pass |
| Create Token dialog | 1312 px wide; body `clientHeight=350`, `scrollHeight=350` | Pass |

## Screenshot Inventory

- `screenshots/final-selector.png`
- `screenshots/final-create-details.png`
- `screenshots/final-create-review.png`
- `screenshots/final-payment-details.png`
- `screenshots/final-bo-picker.png`
- `screenshots/final-clone-details.png`
- `screenshots/final-utxo-split.png`

The last split screenshot was recaptured from the rebuilt image after the Material Icon ligature repair. Runtime console inspection after the final flow returned an empty warning/error list.

## Final Rebuild Boundary

The last source pass added draft lifecycle reset, token attached-main disclosure, BO picker capacity/ARIA semantics, and delegated-rail row placement. These changes passed the 115-test transaction/BO-picker suite and the final full AccountApp image build. Alice, Bob, Lab, node health, and node readiness all returned HTTP 200 from that image/runtime. A delegated attempt to attach a second in-app browser session after the original deliverable session was finalized returned no available IAB backend; no external browser backend was substituted.
