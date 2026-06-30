# SB04 Semantic Invariants

## SB04-INV-01 Real Alice Bob WASM Validation

- Invariant ID: SB04-INV-01
- Source raw note: RN-06 requires real validations between Alice and Bob in the WASM app.
- Expected behavior: Alice and Bob use separate docker-hosted AccountApp instances, import accounts, create BOs, and exchange a BO through signed browser transactions and the node API.
- Disallowed shallow implementation: seed test state directly or verify Alice only.
- Failing-first test and transcript: separate Bob import/refresh is required; Alice-only gallery proof is insufficient.
- Passing test and transcript: `bundle://proof/SB04/transcripts/04-playwright-bob-receive-refresh.txt`.
- Changed source files and hashes: `bundle://proof/changed-file-hashes.md`.
- Production assertions: docker app config and browser proof transcripts.
- Red-team negative case: Bob gallery verification after transfer proves the record came through ledger state, not local Alice state.
- Downstream dependency check: current docker services remain running according to final health commands.

## SB04-INV-02 Generic Family Matrix Works In Browser

- Invariant ID: SB04-INV-02
- Source raw note: RN-02 requires all created BO types to have correct gallery data.
- Expected behavior: each in-scope practical family can be created in the browser and appears in gallery with typed card data.
- Disallowed shallow implementation: unit-test only coverage with no rendered UI proof.
- Failing-first test and transcript: upload failure screenshots captured the initial browser break.
- Passing test and transcript: `bundle://proof/SB04/transcripts/03-playwright-alice-create-matrix.txt` and `bundle://proof/SB04/screenshots/alice-all-generic-types-gallery.png`.
- Changed source files and hashes: `bundle://proof/changed-file-hashes.md`.
- Production assertions: source assertion transcripts from SB02 and SB03.
- Red-team negative case: a raw JSON gallery would not satisfy the screenshot review of typed cards/facts.
- Downstream dependency check: Bob receive proof covers cross-account refresh after creation.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `AliceCreatedGenericBusinessObjects` | `bundle://proof/SB04/transcripts/03-playwright-alice-create-matrix.txt` | `bundle://proof/SB04/screenshots/alice-all-generic-types-gallery.png` | `bundle://proof/SB04/transcripts/02-docker-rebuild-restart-health.txt` | Upload failure screenshots and anti-stub transcript |
| `BobReceivedTransferredMessage` | `bundle://proof/SB04/transcripts/04-playwright-bob-receive-refresh.txt` | `bundle://proof/SB04/screenshots/bob-received-transferred-message.png` | Bob refresh browser proof | Separate Bob AccountApp instance rejects Alice-only state |
