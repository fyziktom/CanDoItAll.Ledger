# SB06 Proof Manifest - WASM PWA Demo App

## Status

- Closure result: `Passed`
- Date: `2026-06-29`
- Scope closed: Blazor WASM PWA AccountApp project, browser crypto/vault DI, API-only node SDK wiring, WASM workspace adapter, account create/unlock/import/backup flows, transaction detail load, local signing and broadcast smoke path, desktop/mobile browser proof, PWA manifest/service worker checks, dependency audit, and workbook/status updates.
- Downstream gate: SB07 final integration and no-key-leak audit may close using this AccountApp browser proof.

## Implementation Summary

- Added `examples/CanDoItAll.Economy.AccountApp` as a Blazor WebAssembly PWA demo project in the solution.
- Registered BaseLib UI services, browser WebCrypto/IndexedDB crypto services, API-only economy SDK client, `WasmAccountWorkspaceAdapter`, and `AccountAppSession` from `Program.cs`.
- Added a first-screen account workspace UI with create, unlock, encrypted backup import/export, pasted key-pair import, API refresh, transaction detail, and sign/broadcast actions.
- Configured AccountApp settings under `wwwroot/appsettings.json` for node base URI, API base path, ledger, scenario, default vault, and account name.
- Added PWA manifest, service worker, icons, and responsive AccountApp styling.
- Removed the WASM transitive dependency on server IPFS hosting by keeping BusinessObjects IPFS storage disabled for browser builds and replacing the local IPFS engine dependency with an HTTP IPFS API client behind `BUSINESS_OBJECT_IPFS`.

## Production Behavior Artifact Matrix

| Artifact | Behavior Proven | Evidence |
| --- | --- | --- |
| `examples/CanDoItAll.Economy.AccountApp` | WASM PWA app builds and launches through the browser dev server | `transcripts/01-accountapp-build.txt`, `accountapp-browser-proof-output.json` |
| `AccountAppSession` | Create, unlock, encrypted backup export/import, pasted-key import, API workspace load, transaction detail, and sign/broadcast orchestration | `accountapp-browser-proof-output.json` |
| `WasmAccountWorkspaceAdapter` | Account data loaded only through API clients and transaction broadcast sends a signed transaction | captured GET/POST requests in `accountapp-browser-proof-output.json` |
| Browser vault | IndexedDB stores encrypted private key material with KDF salt/nonce; local/session storage stay empty of secrets | `proof/SB07/no-key-leak-audit.md` |
| PWA assets | Manifest name, standalone mode, icons, theme color, and service worker registration validated | `accountapp-browser-proof-output.json` |

## Browser Proof

| Flow | Viewport | Evidence | Result |
| --- | --- | --- | --- |
| First-run create account | Desktop 1440x1000 | `screenshots/01-first-run-desktop.png`, `screenshots/02-unlocked-workspace.png` | Passed |
| Transaction detail and signed broadcast | Desktop 1440x1000 | `screenshots/03-transaction-detail.png`, `screenshots/04-signed-broadcast-result.png` | Passed |
| Encrypted backup export/import/reload unlock | Desktop 1440x1000 | `screenshots/05-backup-export.png`, `screenshots/06-imported-backup-unlocked.png`, `screenshots/07-unlock-stored-vault-after-reload.png` | Passed |
| Mobile first run and pasted-key import | Mobile 390x844 | `screenshots/08-first-run-mobile.png`, `screenshots/09-pasted-key-import-mobile.png` | Passed |
| Manifest and service worker | Browser runtime | `accountapp-browser-proof-output.json` | Passed |

## Command Transcripts

| Transcript | Result |
| --- | --- |
| `transcripts/01-accountapp-build.txt` | AccountApp scoped build passed. |
| `transcripts/02-accountapp-browser-proof.txt` | AccountApp browser proof passed and wrote `accountapp-browser-proof-output.json`. |
| `transcripts/03-sb06-dependency-audit.txt` | Browser dependency/security audit passed with documented expected non-WASM crypto references. |
| `transcripts/04-workbook-regeneration-final.txt` | Workbook regenerated with SB06/SB07 final statuses. |

## Source Hashes

- See `changed-file-hashes.txt`.

## Downstream Notes

- SB07 consumes the same browser proof output for final storage/network no-key-leak assertions.
- Production auth/CORS policy remains deployment-specific; the demo PWA does not compile static bearer/API-key secrets.
