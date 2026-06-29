# Requirement Traceability

| Requirement | Owner | Proof expected |
| --- | --- | --- |
| RQ01 | SB06 | New WASM PWA project builds and launches. |
| RQ02 | SB03, SB06 | SB03 passed browser-safe SDK config and API-only request proof; SB06 still owns final browser network proof. |
| RQ03 | SB03 | Passed in `proof/SB03/manifest.md`; endpoint contract tests cover reads and signed broadcast. |
| RQ04 | SB04 | Passed in `proof/SB04/manifest.md`; unsigned draft assembly is separate from signing and no private key is required for draft assembly. |
| RQ05 | SB01, SB04 | Passed for SB04 in `proof/SB04/manifest.md`; crypto-driver/browser signer applies signatures and node receives signed transaction only. |
| RQ06 | SB01 | Passed in `proof/SB01/manifest.md`; vault tests show encrypted-at-rest key material. |
| RQ07 | SB01 | Passed in `proof/SB01/browser-harness-result.json`; IndexedDB browser test shows encrypted key persistence. |
| RQ08 | SB01 | Passed; SB01 progression gate is closed and downstream may consume the vault. |
| RQ09 | SB05 | SSR and WASM adapter tests. |
| RQ10 | SB05, SB06 | Existing ledger components reused or documented as intentionally replaced. |
| RQ11 | SB01 | Passed in `proof/SB01/browser-harness-result.json`; browser WebCrypto proof for hash/random/encrypt/sign. |
| RQ12 | SB01 | Passed in `proof/SB01/transcripts/08-full-ledger-tests.txt`; non-WASM .NET provider tests stay passing. |
| RQ13 | SB01, SB06 | SB01 passed dependency audit; SB06 must repeat for final AccountApp graph. |
| RQ14 | SB01 | Passed in `BrowserCryptoVaultTests`; .NET/browser compatibility vectors are proven for SB01 scope. |
| RQ15 | SB01 | Passed in vault unit tests and browser harness. |
| RQ16 | SB01, SB04 | Passed; SB01 proves browser/WebCrypto raw signature verifies through .NET provider and SB04 proves browser-signed transaction POST shape. |
| RQ17 | SB02 | Passed in `proof/SB02/manifest.md`; backup/export/import recovers same address. |
| RQ18 | SB02 | Passed in `AccountSetupFlowTests` and `proof/SB02/account-setup-browser-harness-result.json`. |
| RQ19 | SB02, SB06 | SB02 passed unlock flow; SB06 still owns API data load after unlock. |
| RQ20 | SB02 | Passed in `AccountSetupFlowTests` and browser harness for backup and pasted-key paths. |
| RQ21 | SB02 | Passed; encrypted backup export proof in focused tests and browser harness. |
| RQ22 | SB02, SB05 | SB02 passed service-boundary/component-callback proof; SB05 still owns wider shared UI SSR/WASM adapter proof. |
| RQ23 | SB03, SB06 | SB03 passed spendable outputs API request proof; SB06 still owns final AccountApp network proof. |
| RQ24 | SB03, SB06 | SB03 passed account transaction list API request proof; SB06 still owns final AccountApp network proof. |
| RQ25 | SB03, SB06 | SB03 passed ledger-scoped and top-level transaction detail API request proof; SB06 still owns final AccountApp network proof. |
| RQ26 | SB03, SB04, SB06 | SB03 passed signed broadcast request proof; SB04 passed browser transaction signing and POST proof; SB06 owns app wiring. |
| RQ27 | SB03, SB04 | Passed; SB03 and SB04 both assert no private-key fields/material in signed broadcast bodies. |
| RQ28 | SB03 | Passed in `proof/SB03/endpoint-matrix.md`; browser-safe registration uses no static operator secrets. |
| RQ29 | Preparation | `plan/account-wasm-pwa-planning.xlsx`. |
| RQ30 | All subbundles | Proof manifest requirements in every critical subbundle. |
| RQ31 | SB07 | Browser validation analytics and screenshots/artifacts. |
| RQ32 | SB07 | Final no-key-leak and SSR regression proof. |
