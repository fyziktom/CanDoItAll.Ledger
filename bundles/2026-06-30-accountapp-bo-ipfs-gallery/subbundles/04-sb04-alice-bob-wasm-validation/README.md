# SB04 Alice Bob WASM Validation

## Status

- Status: Completed

## Objective

Validate the completed feature with real docker-hosted AccountApp WASM instances for Alice and Bob, including IPFS-backed attachment creation, generic BO gallery data, send/transfer, refresh, and screenshots.

## Covered Inputs

- RN-01
- RN-02
- RN-06
- REQ-01
- REQ-03
- REQ-05
- REQ-07

## Prerequisites

- SB01, SB02, and SB03 closure gates passed.
- AccountApp docker image rebuilt after source changes.
- IPFS docker stack is healthy.
- Ledger node and three AccountApp containers are healthy.

## Exact Source References

- `repo://docker-compose.yml`
- `repo://docker/accountapp/Dockerfile`
- `repo://docker/accountapp/nginx.conf`
- `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor`
- `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor`

## Deliverables

- Docker stack with IPFS, ledger node, Alice app, Bob app, and lab app running.
- Playwright scripts/transcripts for Alice import/create/upload/send and Bob import/refresh/gallery detail proof.
- Screenshots showing gallery cards/details and IPFS attachments.
- Final raw-note closure and completed bundle validator.

## Dependency Impact

- This is the final closure phase.
- If it fails, reopen the owning earlier subbundle instead of summarizing around the failure.

## Validation Depth

- Critical end-to-end closure
- Real browser proof
- Docker/host proof
- Regression test proof

## Implementation Steps

1. Rebuild AccountApp docker image and restart Alice/Bob/lab apps with IPFS settings.
2. Import Alice and Bob accounts from prepared backups.
3. Use Alice to create BOs across the in-scope family matrix, including at least one IPFS-backed image attachment.
4. Send/transfer at least one created BO from Alice to Bob.
5. Verify Alice and Bob galleries/details show expected data after refresh.
6. Run final tests, anti-stub audit, proof manifests, and completed bundle validator.

## Scope Exceptions

- If existing demo funds are insufficient, create or seed fresh spendable inputs through documented demo seeding rather than manually editing database rows.

## Do Not Do

- Do not manually insert BO rows into storage.
- Do not bypass WASM signing by submitting server-built final transactions for browser proof.
- Do not count screenshots without DOM assertions as closure proof.

## Acceptance Checklist

- [x] IPFS, ledger node, and AccountApps are healthy.
- [x] Alice imports/unlocks and creates generic BOs.
- [x] At least one attachment is uploaded to IPFS and is stored as a CID-backed DataItem.
- [x] Bob imports/unlocks and sees the transferred/sent BO after refresh.
- [x] Final tests pass; completed bundle validator is the remaining closure command.

## Proof Required

- `proof/SB04/transcripts/01-full-tests.txt`
- `proof/SB04/transcripts/02-docker-rebuild-restart-health.txt`
- `proof/SB04/transcripts/03-playwright-alice-create-matrix.txt`
- `proof/SB04/transcripts/04-playwright-bob-receive-refresh.txt`
- `proof/SB04/transcripts/05-anti-stub-audit.txt`
- Screenshots under `proof/SB04/*.png`
- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`

## Browser Validation Logging

- Routes: `http://localhost:5081` for Alice, `http://localhost:5082` for Bob.
- Viewport: 1920x1080 first pass; dialog screenshots as needed.
- Actions: import/unlock accounts, create BOs, upload file, submit signed transaction, send/transfer BO, refresh Bob, open gallery detail.
- Assertions: card/detail data by type, IPFS CID present, image visible, Bob owner/address state, no generic fallback for in-scope types.
- Screenshots: Alice wizard/gallery, Alice detail with attachment, Bob received object detail.

## Progression Gate

- The bundle may close only when Alice/Bob browser proof, command proof, proof manifests, raw-note closure, and completed-stage validator all pass.

## Suggested Agent Prompt

```text
Implement SB04 only. Rebuild/restart docker, run real Alice/Bob Playwright validation, capture screenshots and transcripts, close raw notes, and stop/reopen earlier subbundles if browser truth contradicts the implementation.
```
