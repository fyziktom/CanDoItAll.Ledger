# SB05 Proof Manifest

## Status

- Result: `Completed`
- Date: `2026-07-02`

## Runtime Proof

- Browser proof script: `bundle://proof/SB04/scripts/docker-secure-messaging-browser-proof.mjs`
- Browser proof summary: `bundle://proof/SB04/docker-secure-messaging-browser-proof.json`
- Alice screenshot: `bundle://proof/SB04/screenshots/docker-secure-mail-alice-decrypted-attachment.png`
- Bob screenshot: `bundle://proof/SB04/screenshots/docker-secure-mail-bob-decrypted-reply.png`
- Semantic contract: `bundle://proof/SB05/semantic-invariants.md`

## Changed File Hashes

- `B786F58C885E87F9B93EF22257D8F10FCDA1C1570510D2521C8E6ACE5828A61E` `bundle://proof/SB04/scripts/docker-secure-messaging-browser-proof.mjs`
- `69A35771B848C3E15FA497FC316B09CC3B3ED784B4959740097EA641248C17CF` `repo://src/CanDoItAll.Economy.Components/Components/Account/SecureMessagingWorkspace.razor`

## Docker Proof

- Passing transcript: `bundle://proof/SB05/transcripts/sb05-proof-audit.txt`
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/sb05-proof-audit.txt`
- Failing-first: N/A, process non-production exemption; adversarial checks are built into the browser proof and fail the proof command when violated.
- `docker compose build accountapp-alice accountapp-bob accountapp-lab` passed.
- `docker compose build economy-node demo-seeder` passed.
- `docker compose up -d --no-deps --force-recreate accountapp-alice accountapp-bob accountapp-lab` passed.
- `docker compose up -d --no-deps --force-recreate economy-node` passed.
- Final `docker compose ps` showed AccountApp Alice, Bob, Lab, economy-node, and healthy Postgres running.

## Closure

- Final browser proof passed after full image rebuild/recreate.
- Bob to Alice with attachment and Alice to Bob without attachment were accepted by the Docker ledger.
- The proof reported zero API errors, zero console errors, and no horizontal overflow.
