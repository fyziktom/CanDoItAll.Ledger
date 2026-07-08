# SB02 Commerce Payment Receipt Flow

Status: completed

Scope:

- Default shared BO creation to reusable carrier tokens.
- Add carrier tokens to BuyBo payment drafts.
- Add carrier tokens to dispatcher clone, receipt, and purchase drafts.
- Separate payment and receipt/purchase UI records for account-home parity with VEFramework.

Acceptance:

- Payment BO carries sale main amount and token amount `1`.
- Receipt and purchase BOs carry token amount `1`.
- Copy-sale clone carries token amount `1` without consuming the original BO carrier.

Proof:

- `proof/SB02/transcripts/07-focused-tests-after-commerce-token-fallback.txt`
- `proof/SB02/transcripts/08-accountapp-build-after-commerce-token-fallback.txt`
- `proof/SB03/accountapp-bo-token-carrier-proof.json`
