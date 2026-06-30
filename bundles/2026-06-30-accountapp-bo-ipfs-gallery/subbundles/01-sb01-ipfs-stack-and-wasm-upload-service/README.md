# SB01 IPFS Stack And WASM Upload Service

## Status

- Status: Completed

## Objective

Rebuild and start the local `CanDoItAll.IPFS` docker stack, prove it can add/read content, and make AccountApp WASM able to upload BO data items through a browser-safe IPFS service.

## Covered Inputs

- RN-01
- REQ-01
- REQ-02

## Prerequisites

- The external IPFS compose file is readable from the local `CanDoItAll.IPFS` checkout.
- Ledger AccountApp docker stack remains available for later integration.
- Bundle prepared-stage validator has passed.

## Exact Source References

- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectStorage.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectDataItemStorageService.cs`
- `repo://src/CanDoItAll.Economy.BusinessObjects/BusinessObjectIpfsStorage.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/Program.cs`
- `repo://examples/CanDoItAll.Economy.AccountApp/Services/AccountAppOptions.cs`
- `repo://docker/accountapp/nginx.conf`
- `repo://docker/accountapp/docker-entrypoint-accountapp.sh`
- `repo://docker-compose.yml`

## Deliverables

- Running IPFS node and node-control docker services with persisted data.
- AccountApp configuration for IPFS API and gateway URLs.
- WASM-compatible `IBusinessObjectDataItemUploadService` registration.
- AccountApp docker nginx proxy path for browser upload to IPFS.

## Dependency Impact

- SB02 and SB03 can parse/render attachment metadata without IPFS, but SB04 cannot close real image/attachment proof unless SB01 proves upload and retrieval.
- Weak proof here invalidates any browser screenshot that claims IPFS-backed image rendering.

## Validation Depth

- Critical foundation
- Host/docker proof
- Build proof
- Browser-visible upload prerequisite

## Implementation Steps

1. Complete the `CanDoItAll.IPFS` docker rebuild/start and capture health output.
2. Add or register a WASM-safe IPFS data storage/upload service without enabling unsupported server-only paths in browser builds.
3. Extend `AccountAppOptions`, appsettings, docker entrypoint, and nginx proxy for IPFS API/gateway configuration.
4. Add focused tests for the upload service request/response and configuration defaults where practical.
5. Build AccountApp and record transcript.

## Scope Exceptions

- SB01 does not implement typed BO families or gallery rendering.
- SB01 does not add authenticated IPFS control flows; local dev docker uses unauthenticated API as configured by `CanDoItAll.IPFS`.

## Do Not Do

- Do not introduce VEFramework dependencies.
- Do not move ledger transaction signing to the server.
- Do not expose IPFS secrets to browser configuration.

## Acceptance Checklist

- [x] IPFS docker services are rebuilt and healthy.
- [x] IPFS add/cat smoke succeeds.
- [x] AccountApp has DI for `IBusinessObjectDataItemUploadService` in WASM.
- [x] AccountApp docker config emits IPFS API/gateway settings.
- [x] AccountApp build passes.

## Proof Required

- `proof/SB01/transcripts/01-ipfs-docker-build-start.txt`
- `proof/SB01/transcripts/02-ipfs-health-add-cat.txt`
- `proof/SB01/transcripts/03-accountapp-build.txt`
- `proof/SB01/transcripts/04-focused-upload-tests.txt`
- `proof/SB01/transcripts/05-source-assertions-and-anti-stub.txt`
- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`

## Browser Validation Logging

- Route: N/A for direct browser proof in this phase.
- Required route-dependent proof moves to SB03/SB04 after the upload service is wired into UI.
- Record docker host and HTTP endpoint health in the execution report.

## Progression Gate

- Downstream subbundles may continue only when IPFS is healthy and AccountApp can build with the WASM upload service registered.

## Suggested Agent Prompt

```text
Implement SB01 only. Rebuild/start CanDoItAll.IPFS, wire browser-safe AccountApp IPFS upload configuration and DI, run the required tests/build/health probes, and stop if IPFS upload cannot be proven.
```
