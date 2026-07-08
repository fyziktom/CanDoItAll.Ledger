# BO token carrier commerce flow

Status: implemented and validated
Date: 2026-07-07

This follow-up bundle repairs the completed `bundles/bo-commerce-flow` implementation so BusinessObject create, payment, receipt, purchase, and copy-dispatch records follow the legacy VEFramework NFT payment/receipt pattern: every created BO artifact is represented by a unique one-unit token output, while payments also carry the main payment amount.

The user-reported regression is visible in `inputs/screenshot-bo-token-none.png`: a create transaction shows a BO output with `TOKEN none`. That transaction is structurally valid in the current ledger, but it violates the target BO carrier-token contract.

## Implementation Scope

- Default BO creation in AccountApp and shared components to the first reusable non-main token source when available.
- Preserve the existing explicit main-only path where no reusable source exists or the caller explicitly chooses `Main`.
- Add clone dispatch support for a new one-unit carrier token without consuming the original BO's carrier token.
- Create commerce payment, receipt, and purchase BOs with one-unit token outputs.
- Keep close/destroy behavior able to return the BO's token to a non-BO output for later source merge operations.
- Split or label commerce lists so payments and receipts/purchases can be inspected independently, matching the old `NFTPaymentsList` and `NFTReceiptsList` account-home concept.

## Validation

- Focused unit tests for transaction builder, token policy, dispatcher drafts, and UI rendering passed.
- Full `CanDoItAll.Ledger.Tests` project passed: 287 tests.
- AccountApp build passed.
- Docker AccountApp validation passed with real Alice/Bob transactions and screenshots showing token-backed BO create, payment, receipt, purchase, and delivered clone details.
- Final proof JSON: `proof/SB03/accountapp-bo-token-carrier-proof.json`.
