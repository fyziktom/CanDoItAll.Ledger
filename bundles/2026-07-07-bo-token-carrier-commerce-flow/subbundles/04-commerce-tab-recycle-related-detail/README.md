# SB04 Commerce Tab, Related Details, And Recycle Repair

Status: completed

Scope:

- Move Account home payment and receipt commerce lists into their own Commerce tab after Balances and before Transactions history.
- Keep commerce rows close to the legacy VEFramework NFT payment/receipt lists by showing marker-record details and a separate related BO details action.
- Wire BO detail Recycle to the existing close-object submit path so payment/receipt BOs can be destroyed into the reusable same-token source output.
- Validate the repair with component tests and real Docker AccountApp transactions.

Acceptance:

- Account home tab order is Overview, Balances, Commerce, Transactions history.
- Payment and receipt rows expose Record and Sold/Bought BO actions.
- Recycle from a receipt BO detail submits a BusinessObject close transaction with two inputs and one non-BO token-source output.

Proof:

- `proof/SB03/transcripts/29-accountapp-commerce-recycle-real-transaction-proof-nodepath.txt`
- `proof/SB03/accountapp-bo-token-carrier-proof.json`
- `proof/SB03/screenshots/alice-dispatch-dashboard.png`
- `proof/SB03/screenshots/alice-payment-related-bo-detail.png`
- `proof/SB03/screenshots/alice-receipt-related-bo-detail.png`
- `proof/SB03/screenshots/alice-receipt-recycle-merge-transaction.png`
