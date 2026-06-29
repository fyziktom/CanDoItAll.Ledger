# SB07 No-Key-Leak Audit

## Result

- Status: `Passed`
- Date: `2026-06-29`
- Source evidence: `proof/SB06/accountapp-browser-proof-output.json`

## Checks

| Surface | Result | Evidence |
| --- | --- | --- |
| Broadcast request body | Passed | Captured signed `POST /api/v1/ledger/transactions` includes signature bytes, payload hash, public key hash, and public key material; the proof assertion reports no private key fields. |
| Account read requests | Passed | Captured `GET` requests are account transactions, spendable outputs, business objects, and transaction detail only. |
| IndexedDB | Passed | Created/imported/pasted-key vault audits contain encrypted private key records with salt, nonce, KDF settings, public key metadata, and no raw private key field. |
| Local storage | Passed | Browser proof found no local storage entries containing private key or password text. |
| Session storage | Passed | Browser proof found no session storage entries containing private key or password text. |
| Console/logs | Passed | Captured console messages are framework debug/password-field form hints only; no key or password material was logged. |
| Backup export | Passed | The exported AccountApp backup contains the encrypted vault record, not decrypted private key material, and the import flow re-encrypts it into the active vault. |

## Assertion Text

- Broadcast: `signed transaction included browser signature/public key material and no private key fields`
- Storage: `IndexedDB vault records contain encrypted private keys only; local/session storage contains no private key or password text`
