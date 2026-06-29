# Assumptions And Risks

## Working Assumptions

- AccountApp will be a new example/demo project, not a replacement for the node SSR console.
- The browser account path can use existing ledger transaction records and node API contracts after DI/config separation.
- Node read endpoints may remain authenticated if the chosen auth mode is browser-safe; private node operator secrets cannot be compiled into the PWA.
- The first supported browser algorithm target is the repository's current ECDSA P-256 path unless SB01 proves a different required signature algorithm.
- VEFramework is local reference material only; its dependencies and broad service implementation are not target dependencies.

## Critical Path Risks

- WebCrypto ECDSA signature format may not match the existing .NET `ECDsa.SignData` format without conversion or verification changes.
- Browser key material export rules may conflict with the desired backup/import format if keys are generated as non-extractable.
- Address generation currently depends on .NET SHA256 APIs in core value objects; this can block any meaningful account creation in WASM.
- `BusinessObjectTransactionBuilder` signs during build, so shallow UI integration could accidentally keep signing server-side.
- Existing Razor components may depend on SSR session services more deeply than the surface parameters imply.
- API authentication and CORS decisions can block the PWA even if SDK endpoints are otherwise ready.

## Validation Risks

- Unit tests alone cannot prove WebCrypto/IndexedDB behavior; browser tests with real JS interop are mandatory.
- A passing fake signer or in-memory key source would not satisfy the raw requirement because signatures and encrypted key persistence must happen in the browser.
- A transaction broadcast test that posts server-signed transactions would be a false positive.
- PWA installability and service worker behavior can hide stale asset/config problems if not tested from a clean browser context.
- The old VEFramework flow includes weak crypto implementation details; copying it verbatim would pass UX checks while failing security intent.

## Reopen Triggers

- Any private key, encrypted key password, or decrypted key leaves the browser account boundary except in explicit user backup export.
- The node receives unsigned transaction drafts or performs account signing for AccountApp transactions.
- AccountApp requires NBitcoin or BouncyCastle in the browser path.
- Browser crypto tests are skipped, stubbed, or replaced only with .NET tests.
- Existing SSR node console behavior regresses while making components WASM-flexible.
- The planning workbook is missing, stale, or no longer reflects the implemented source surfaces.
