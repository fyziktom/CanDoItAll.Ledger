# SB04/SB05 Proof Manifest

## Status

- Result: `Completed`
- Date: `2026-07-02`

## Runtime Proof

- Browser proof summary: `proof/SB04/docker-secure-messaging-browser-proof.json`
- Node REPL transcript: `proof/SB04/transcripts/docker-secure-messaging-browser-proof-node-repl.txt`
- Alice decrypted attachment screenshot: `proof/SB04/screenshots/docker-secure-mail-alice-decrypted-attachment.png`
- Bob decrypted reply screenshot: `proof/SB04/screenshots/docker-secure-mail-bob-decrypted-reply.png`

## Validated Behavior

- Bob sent Alice a secure message with an attachment.
- Alice opened the message and decrypted the attachment only through the explicit attachment decrypt action.
- Alice sent Bob a secure reply without an attachment.
- Bob opened the reply after the sender public key was discoverable from Bob's signed transaction.
- Message rows were selected by transaction id during proof to avoid false positives from older encrypted messages.
- Final layout metrics reported no horizontal overflow at 1760x990 for Alice or Bob.
- Final proof reported zero API errors and zero browser console errors.

## Docker State

- Rebuilt images:
  - `candoitall-ledger-accountapp:dev`
  - `candoitall-ledger-economy-node:dev`
  - `candoitall-ledger-demo-seeder:dev`
- Recreated containers:
  - `accountapp-alice`
  - `accountapp-bob`
  - `accountapp-lab`
  - `economy-node`
- Final `docker compose ps` showed all app/node containers running and Postgres healthy.

## Notes

- The local Postgres database was recreated after an interrupted reset left the app database missing. The demo seeder was rerun successfully before browser validation.
- Existing package warnings remain outside this bundle: `ncalc 1.3.8` compatibility and `SQLitePCLRaw.lib.e_sqlite3` advisory.
