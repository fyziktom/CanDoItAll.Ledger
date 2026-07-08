# SB01 Proof Manifest

Status: Passed

## Production Behavior Artifact Matrix

| Behavior | Producer | Consumer | Proof |
|---|---|---|---|
| Token outputs cannot carry main amount. | `TransactionShapeValidator` | `LedgerExecutor` | `LedgerCriticalInvariantTests` rejects mixed token/main outputs; Docker payment tx output `0` has token amount `1` and main amount `0`. |
| Normal BO outputs are unique token carriers. | `TransactionShapeValidator` | `LedgerExecutor` | `LedgerCriticalInvariantTests` rejects token-less normal BO outputs; Docker BO/payment/receipt/purchase/delivered outputs each carry token amount `1`. |
| Drivers emit separated outputs. | `BusinessObjectTransactionBuilder` and component adapters | Node/API/UI | Unit tests passed; Docker payment tx `fcb1c0a2f7e897605952d0cbb43bb26303f220947254fb227f7025f0b629cdc6` has BO token output `0` and plain main payment output `1`. |

## Test Transcripts

- `dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj --no-restore`: `292/292` passed.
- Focused invariant suite: `33/33` passed.
- Docker build: `transcripts/docker-build-cached.txt`.
- Browser proof: `transcripts/accountapp-bo-token-main-separation-proof.txt`.

## Runtime Artifacts

- Proof JSON: `accountapp-bo-token-main-separation-proof.json`.
- Screenshots:
  - `screenshots/alice-created-bo-token-detail.png`
  - `screenshots/bob-payment-create-separate-output-transaction.png`
  - `screenshots/alice-payment-token-detail.png`
  - `screenshots/alice-receipt-token-detail.png`
  - `screenshots/bob-purchase-token-detail.png`
  - `screenshots/alice-receipt-recycle-merge-transaction.png`
