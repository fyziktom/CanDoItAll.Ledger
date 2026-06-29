# Normalized Requirements

## Product Requirements

- RQ01: Prepare, then implement later, a Blazor WASM PWA AccountApp demo for ledger accounts.
- RQ02: AccountApp must communicate with the node only through API calls.
- RQ03: Node API calls for AccountApp are limited to account transaction/list/detail/spendable-output reads and signed transaction broadcast.
- RQ04: Transaction assembly must happen in the AccountApp/browser side.
- RQ05: Transaction signing and private key use must happen in the AccountApp/browser side.
- RQ06: Private keys must be encrypted before browser persistence.
- RQ07: Encrypted private keys must be stored in browser IndexedDB.
- RQ08: The encryption/key-vault foundation must be implemented and proven before backup/import or transaction work proceeds.
- RQ09: Shared Razor components must support SSR and WASM usage based on configuration/adapter selection.
- RQ10: Existing ledger account components must be reused where they can be decoupled safely from SSR services.

## Crypto Requirements

- RQ11: WASM cryptography must use browser APIs for SHA256/hash, random, AES-GCM/KDF operations, and signing where .NET crypto is unsupported.
- RQ12: Non-WASM paths must continue using built-in .NET cryptography where available.
- RQ13: The new AccountApp path must not introduce NBitcoin or BouncyCastle dependencies.
- RQ14: WebCrypto and .NET crypto outputs must be validated by deterministic compatibility vectors.
- RQ15: Browser vault tests must cover create, encrypt, persist, unlock, decrypt, reject wrong password, and tamper rejection.
- RQ16: Signature tests must prove a browser-created signature verifies through node/server verification logic.
- RQ17: Key backup/export must preserve enough data to re-import and recover the same address.

## Account Flow Requirements

- RQ18: Create-account flow must follow the VEBlazor user-story steps: password, account generation, encrypted key persistence, account-loaded event.
- RQ19: Unlock flow must follow the VEBlazor user-story steps: password, decrypt stored key, load account, load API data.
- RQ20: Import flow must follow the VEBlazor user-story steps: upload backup or paste key, old password if needed, new password and confirmation, import, encrypt, persist, load account.
- RQ21: Backup flow must export account recovery data through an explicit browser action.
- RQ22: Import/backup implementation must improve the old implementation by separating vault, crypto, account session, and UI state.

## API And Data Requirements

- RQ23: AccountApp must load account spendable outputs through node API.
- RQ24: AccountApp must load account transaction lists through node API.
- RQ25: AccountApp must load specific transaction details through node API.
- RQ26: AccountApp must broadcast already signed `LedgerTransaction` payloads through node API.
- RQ27: Node/API tests must prove AccountApp endpoints do not require private key material.
- RQ28: Browser-safe authentication/CORS/static-hosting configuration must be decided and tested.

## Planning And Proof Requirements

- RQ29: The bundle must include detailed XLSX checklists and planning.
- RQ30: Each critical subbundle must define artifact-backed proof, anti-stub checks, and a progression gate.
- RQ31: Browser validation proof must include real browser automation, screenshots or equivalent artifacts, and IndexedDB/WebCrypto assertions.
- RQ32: Final closure must show no SSR regression and no browser private-key leakage.
