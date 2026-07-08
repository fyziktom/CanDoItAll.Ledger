# SB03 AccountApp Docker Validation

Status: completed

Scope:

- Build and run docker AccountApp stack.
- Execute Alice/Bob sale and purchase flow.
- Capture transaction details screenshots proving BO token amount `1` for payment, receipt, purchase, and clone/original delivery as applicable.
- Capture test/build transcripts and final architecture review.

Acceptance:

- Real docker AccountApp transactions confirm the screenshot regression is fixed.
- Follow-up real docker AccountApp transactions confirm Commerce tab placement, related BO detail actions, and receipt recycle merge behavior.
- Proof artifacts are stored under `proof/SB03`.

Proof:

- `proof/SB03/transcripts/23-accountapp-real-transaction-proof-after-commerce-token-fallback.txt`
- `proof/SB03/transcripts/29-accountapp-commerce-recycle-real-transaction-proof-nodepath.txt`
- `proof/SB03/screenshots/alice-created-bo-token-detail.png`
- `proof/SB03/screenshots/alice-receipt-token-detail.png`
- `proof/SB03/screenshots/bob-purchase-token-detail.png`
- `proof/SB03/screenshots/alice-dispatch-dashboard.png`
- `proof/SB03/screenshots/alice-payment-related-bo-detail.png`
- `proof/SB03/screenshots/alice-receipt-related-bo-detail.png`
- `proof/SB03/screenshots/alice-receipt-recycle-merge-transaction.png`
