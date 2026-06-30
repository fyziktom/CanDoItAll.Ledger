# SB01 Proof Manifest

## Subbundle

- Id: SB01
- Status: Completed
- Owned raw notes: RN-01
- Owned requirements: REQ-01, REQ-02
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Changed-file hash manifest: `bundle://proof/changed-file-hashes.md`
- Changed-file SHA-256 sample: `727f40a6313b11193241a02331d0074ee07abbb6c7b2a483d5446662f441ae0e`

## Command Transcripts

- Docker rebuild/start and current health: `bundle://proof/SB01/transcripts/01-ipfs-docker-build-start.txt`
- IPFS add/cat and AccountApp proxy cat: `bundle://proof/SB01/transcripts/02-ipfs-health-add-cat.txt`
- AccountApp Release build: `bundle://proof/SB01/transcripts/03-accountapp-build.txt`
- Focused upload tests: `bundle://proof/SB01/transcripts/04-focused-upload-tests.txt`
- Source assertions and anti-stub audit: `bundle://proof/SB01/transcripts/05-source-assertions-and-anti-stub.txt`

## Failing-First And Negative Proof

- Failing-first transcript: N/A - process negative proof is captured by browser failure screenshots before the parser repair.
- Initial browser upload failed on line-delimited IPFS add JSON and is captured by `bundle://proof/SB04/screenshots/alice-upload-timeout.png` and `bundle://proof/SB04/screenshots/alice-upload-retry.png`.
- The shallow implementation rejected by this phase is a single-object JSON parser that only accepts one JSON object. The focused upload test in `bundle://proof/SB01/transcripts/04-focused-upload-tests.txt` covers the line-delimited response shape.
- The existing locked IPFS volume failed with a wrong passphrase after rebuild; it was backed up and replaced with a clean persisted dev volume. The current health transcript proves the restored running state.

## Passing Proof

- Passing transcript: `bundle://proof/SB01/transcripts/04-focused-upload-tests.txt`
- IPFS node and node-control are healthy in `bundle://proof/SB01/transcripts/01-ipfs-docker-build-start.txt`.
- Current node add/cat returned CID `QmdKEA6Ta1jNwNTai9t11b3HBnjR9uDu9DUaodE916CT2n` in `bundle://proof/SB01/transcripts/02-ipfs-health-add-cat.txt`.
- AccountApp same-origin IPFS proxy returned the same content in `bundle://proof/SB01/transcripts/02-ipfs-health-add-cat.txt`.
- AccountApp Release build succeeded in `bundle://proof/SB01/transcripts/03-accountapp-build.txt`.
- Focused upload tests passed 11/11 in `bundle://proof/SB01/transcripts/04-focused-upload-tests.txt`.

## Source-Level Assertions

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItemStorageService.cs` contains the browser-safe HTTP IPFS upload service and line-delimited add response parser.
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectIpfsStorage.cs` contains the same parser behavior for the server-side IPFS driver.
- `repo://examples/CanDoItAll.Economy.AccountApp/Program.cs` registers the browser upload service only when IPFS upload settings are enabled.
- `repo://docker/accountapp/nginx.conf` and `repo://docker/accountapp/docker-entrypoint-accountapp.sh` expose and emit the same-origin IPFS configuration used by the WASM app.
- Source assertion line evidence is in `bundle://proof/SB01/transcripts/05-source-assertions-and-anti-stub.txt`.

## Anti-Stub Audit

- `bundle://proof/SB01/transcripts/05-source-assertions-and-anti-stub.txt` reports no production matches for TODO, NotImplemented, placeholder, stub, fake, or sample-only markers in the SB01 production files.

## Browser And Host Proof

- Host/docker proof is in `bundle://proof/SB01/transcripts/01-ipfs-docker-build-start.txt` and `bundle://proof/SB01/transcripts/02-ipfs-health-add-cat.txt`.
- Browser-visible upload proof is carried by SB04 screenshots and Playwright transcripts after the upload service is consumed by the AccountApp UI.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `CidBackedBusinessObjectDataItem` | `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItemStorageService.cs` and `bundle://proof/SB01/transcripts/04-focused-upload-tests.txt` | `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` and `bundle://proof/SB04/screenshots/alice-ipfs-image-gallery.png` | `bundle://proof/SB01/transcripts/01-ipfs-docker-build-start.txt` and `bundle://proof/SB01/transcripts/02-ipfs-health-add-cat.txt` | `bundle://proof/SB04/screenshots/alice-upload-timeout.png` plus the passing parser test transcript |
| `AccountAppIpfsProxyConfig` | `repo://docker/accountapp/docker-entrypoint-accountapp.sh` | `repo://examples/CanDoItAll.Economy.AccountApp/Program.cs` | `bundle://proof/SB01/transcripts/03-accountapp-build.txt` and docker runtime proof in SB04 | `bundle://proof/SB01/transcripts/05-source-assertions-and-anti-stub.txt` |
