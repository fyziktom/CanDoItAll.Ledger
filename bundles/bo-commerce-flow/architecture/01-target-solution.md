# Target Solution

## Architecture

The target design adds a typed BO commerce layer over the existing ledger APIs. Sale terms live inside the BO media payload as structured commerce metadata. Payment, purchase, and receipt records are first-class BO payloads with stable reference fields rather than untyped ad hoc JSON. AccountApp remains a browser-signed client: it queries listed BOs through the node API, builds unsigned transactions locally, signs with the browser vault, and broadcasts through the node API.

## Core Records

- `BusinessObjectSaleTerms`: sale status, mode (`Original` or `Copy`), amount, token asset or main currency, seller, max clones, listed timestamp, share path.
- `BusinessObjectPaymentRecord`: payment object id, buyer, seller, target BO id/version/outpoint, amount, asset, sale mode, status (`Pending`, `Dispatched`, `Rejected`), buyer payment tx, dispatch tx, receipt id.
- `BusinessObjectPurchaseRecord`: buyer-side purchase reference for dashboard and detail linking.
- `BusinessObjectReceiptRecord`: seller/buyer receipt with original BO id, delivered BO id, payment id, dispatch tx, amount, sale mode, issued timestamp.

## Flow

1. Seller opens Set Sale from gallery/details or during minting and writes sale terms to the owned BO.
2. Seller copies a Buy BO link built from ledger id, optional scenario id, and object id.
3. Buyer opens `/buy-bo/{objectId}` or query-equivalent route. The page queries the node for current BO state, validates sale terms, and unlocks or uses the local browser vault.
4. Buyer creates a payment BO owned by seller and carrying the price main amount. Payment state references the target BO and sale terms snapshot.
5. Seller refresh loop loads workspace, finds pending payment records, validates target object and sale limits, then dispatches:
   - original sale: transfer the original BO to buyer and mark payment dispatched;
   - copy sale: clone the BO to buyer with a deterministic clone object id and mark payment dispatched after clone success.
6. Seller creates or updates receipt/purchase records and dashboard lists payments/receipts with related BO detail links.

## Ledger Boundary

- No consensus or node trust change is required. The node remains a read/broadcast API.
- Because projection has one payload envelope per transaction, implementation should prefer clear transaction chains over multi-state payload tricks.
- Deterministic ids and status checks provide idempotency across refresh retries.

## UI Boundary

- Use shared components under `repo://src/CanDoItAll.Economy.Components` so SSR and WASM paths share sale dialogs, details rendering, and dashboard panels.
- AccountApp adds only route/page wiring and browser-session orchestration.

## Validation Strategy

- Unit tests cover serialization, parsing, sale validation, payment creation, max clone enforcement, and idempotent dispatch.
- Browser proof exercises production UI with Alice and Bob against the real node/docker setup.
- Every critical subbundle records manifests and semantic invariants under `bundle://proof/SBxx/`.
