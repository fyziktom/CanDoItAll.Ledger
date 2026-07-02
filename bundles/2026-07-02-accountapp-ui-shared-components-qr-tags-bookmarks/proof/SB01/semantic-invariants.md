# SB01 Semantic Invariants - Shared component boundary and QR foundation

## Invariant SB01-INV-01

- Source raw note: R05/R06: add shared QR generation and scanning components for ledger apps.
- Expected behavior: CanDoItAll.Components.QRCode project with QrCodeView, QrCodeButton, QrCodeDialogContent, QrScanButton, Net.Codecrete renderer, and ZXingBlazor scanner integration.
- Disallowed shallow implementation: status-only, placeholder-only, fixture-only, or UI-only code that does not exercise production components/services.
- Failing-first or adversarial proof: QR tests cover empty/invalid payload boundaries; source assertions prove production QR code is not a placeholder.
- Passing proof: bundle://proof/SB01/transcripts/components-qrcode-build.txt; bundle://proof/SB01/transcripts/components-qrcode-tests.txt; bundle://proof/SB01/screenshots/qr-dialog-large.png
- Changed source files and hashes: bundle://proof/SB07/changed-file-hashes.json
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt
- Red-team negative case: bundle://proof/SB07/red-team-verifier.md
- Downstream dependency check: bundle://reviews/01-execution-report.md

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| QRCodePayload | repo://C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.QRCode/NetCodecreteQrCodeRenderer.cs | repo://C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.QRCode/Components/QrCodeDialogContent.razor | repo://C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.QRCode/Components/QrCodeButton.razor | bundle://proof/SB01/transcripts/components-qrcode-tests.txt |
