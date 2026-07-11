# SB03 Payment, Token, and UTXO Proof Manifest

## Delivered

- Payment composes reusable address and balance-aware amount controls.
- Token transfer composes available-token selection, token/main amount bounds, receiver, memo, and shared validation.
- UTXO Merge and Split retain their plain-main-only selectors and now receive shared submit validation for selection count, duplicates, availability, positive amount, output presence, and overspend.

## Evidence

- Focused suite: `../SB02/transcripts/02-focused-components-passing.txt` (21 passed).
- AccountApp WASM empty-account visual pass verified Payment, Token, Merge, and Split forms, their clear empty states, and disabled Submit for invalid values.
- Existing UTXO component tests remain part of that focused suite; SSR source parity confirms distinct Merge/Split routing.

## Semantic Invariants

- Plain UTXO modes cannot accept token or BO outputs.
- Merge requires two selected UTXOs; Split requires an input and at least one positive output.
- Split output total cannot exceed the selected plain total.
- Main/token amounts cannot exceed supplied balance context.
