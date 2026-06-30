# SB01 Semantic Invariants

## SB01-INV-01 Browser-Safe CID Upload

- Invariant ID: SB01-INV-01
- Source raw note: RN-01 requires IPFS for images and BO attachments.
- Expected behavior: AccountApp WASM uploads attachment bytes through an HTTP IPFS service and stores CID metadata in `BusinessObjectDataItem`; ledger transaction signing remains browser-side.
- Disallowed shallow implementation: put inline file bytes into the BO state or register a server-only storage driver in WASM.
- Failing-first test and transcript: initial browser upload failure screenshots in `bundle://proof/SB04/screenshots/alice-upload-timeout.png` and `bundle://proof/SB04/screenshots/alice-upload-retry.png`.
- Passing test and transcript: `bundle://proof/SB01/transcripts/04-focused-upload-tests.txt` and `bundle://proof/SB04/transcripts/03-playwright-alice-create-matrix.txt`.
- Changed source files and hashes: `bundle://proof/changed-file-hashes.md`.
- Production assertions: `bundle://proof/SB01/transcripts/05-source-assertions-and-anti-stub.txt`.
- Red-team negative case: line-delimited IPFS add output must still produce the CID-bearing entry; single-object parsing is rejected by the focused upload test.
- Downstream dependency check: SB04 browser proof created an IPFS image BO and showed it in the gallery.

## SB01-INV-02 Rebuildable Persisted IPFS Stack

- Invariant ID: SB01-INV-02
- Source raw note: RN-01 requires the IPFS docker stack to be rebuilt and started for image/attachment loading.
- Expected behavior: the IPFS node and node-control containers run from docker compose with persisted volume storage and a working API.
- Disallowed shallow implementation: only update AccountApp config without a running IPFS API.
- Failing-first test and transcript: wrong-passphrase restart loop is captured in the overwritten start transcript history and repaired by backing up the volume and recreating a persisted dev volume.
- Passing test and transcript: `bundle://proof/SB01/transcripts/01-ipfs-docker-build-start.txt` and `bundle://proof/SB01/transcripts/02-ipfs-health-add-cat.txt`.
- Changed source files and hashes: `bundle://proof/changed-file-hashes.md`.
- Production assertions: docker compose health and IPFS node ID proof in `bundle://proof/SB01/transcripts/01-ipfs-docker-build-start.txt`.
- Red-team negative case: API health alone is insufficient; add/cat and AccountApp proxy cat are both required.
- Downstream dependency check: SB04 AccountApp proof depends on the same local IPFS API path.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `CidBackedBusinessObjectDataItem` | `bundle://proof/SB01/transcripts/04-focused-upload-tests.txt` | `bundle://proof/SB04/screenshots/alice-ipfs-image-gallery.png` | `bundle://proof/SB01/transcripts/02-ipfs-health-add-cat.txt` | `bundle://proof/SB04/screenshots/alice-upload-timeout.png` |
| `AccountAppIpfsProxyConfig` | `repo://docker/accountapp/docker-entrypoint-accountapp.sh` | `repo://examples/CanDoItAll.Economy.AccountApp/Program.cs` | `bundle://proof/SB01/transcripts/03-accountapp-build.txt` | `bundle://proof/SB01/transcripts/05-source-assertions-and-anti-stub.txt` |
