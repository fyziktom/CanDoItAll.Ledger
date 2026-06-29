# Structured Input

## Bundle Type

- Initiative bundle for a new Blazor WASM PWA demo application plus supporting shared libraries, API contracts, crypto abstractions, and browser validation.

## Primary Actors

- Browser account user: creates, imports, unlocks, backs up, signs, and broadcasts from the AccountApp without giving private keys to the node.
- Node API: exposes account transaction lists, transaction details, spendable outputs, and signed transaction broadcast.
- Shared Razor components: render account workspace, history, UTXOs, composer, and backup/import flows in SSR and WASM host modes.
- Implementation agents: execute one subbundle at a time and cannot bypass critical crypto/vault gates.

## Must-Have Flow

1. AccountApp launches as Blazor WASM PWA and reads node connection settings.
2. User creates or imports an account.
3. Private key material is encrypted in the browser and persisted in IndexedDB.
4. User unlocks account; decrypted private key is used only in browser memory for signing.
5. App loads spendable outputs, transactions, and transaction details through node API.
6. App assembles transactions locally, signs inputs locally, and broadcasts signed ledger transactions through node API.
7. User can export a backup and re-import it without losing address integrity.

## Explicit Non-Goals For Preparation

- No production implementation in this bundle-preparation step.
- No UX invention beyond adapting the VEBlazor create/unlock/import/backup flow and existing ledger account components.
- No NBitcoin or BouncyCastle dependencies in the new AccountApp path.
- No private key transmission to node endpoints.
