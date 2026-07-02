# SB05 - Docker Browser E2E Validation

## Status

- Completed

## Objective

Rebuild Docker images, restart containers, and prove the WASM AccountApps can exchange secure messages between Alice and Bob in both directions, with and without encrypted attachments.

## Covered Inputs

- N006
- N010
- N013
- N014

## Prerequisites

- SB04 closure gate passed.

## Exact Source References

- `repo://docker-compose.yml`
- `repo://examples/CanDoItAll.Economy.AccountApp`
- `repo://src/CanDoItAll.Economy.Components`
- `repo://src/CanDoItAll.Economy.Cryptography.Browser`
- `repo://src/CanDoItAll.Economy.BusinessObjects`

## Deliverables

- Rebuilt Docker images.
- Recreated containers for node, postgres, and Alice/Bob/lab AccountApps.
- Playwright Docker browser proof for:
  - Alice to Bob message without attachment.
  - Bob to Alice message without attachment.
  - Alice to Bob message with encrypted attachment.
  - Bob to Alice message with encrypted attachment.
- IPFS/gateway or API proof that attachment storage is encrypted.
- Final raw-note closure and completed-stage bundle validation.

## Dependency Impact

- Final closure phase. Any failure reopens earlier subbundle matching the defect.

## Validation Depth

- Full build/test, Docker rebuild/recreate/smoke, Playwright browser proof, screenshots, storage inspection, red-team closure audit.

## Implementation Steps

1. Run full targeted tests and solution build.
2. Rebuild Docker images.
3. Recreate Docker services.
4. Import/unlock Alice and Bob accounts as needed.
5. Ensure public-key discovery prerequisites exist for both accounts.
6. Send/decrypt no-attachment messages both directions.
7. Send/decrypt attachment messages both directions.
8. Inspect encrypted BO state and encrypted IPFS bytes for plaintext-negative proof.
9. Capture screenshots and JSON summary.
10. Complete raw-note closure table and final validator.

## Scope Exceptions

- If camera/file picker cannot be automated, use Playwright `setInputFiles` while still proving UI-driven upload and decrypt controls.

## Do Not Do

- Do not use SSR/node server-side encryption to satisfy WASM proof.
- Do not manually seed decrypted local state.
- Do not mark closure complete if any direction or attachment case is missing.

## Acceptance Checklist

- [ ] Docker build passed.
- [ ] Docker services are up and node healthy.
- [ ] Alice to Bob no-attachment secure message passes.
- [ ] Bob to Alice no-attachment secure message passes.
- [ ] Alice to Bob attachment secure message passes.
- [ ] Bob to Alice attachment secure message passes.
- [ ] IPFS attachment plaintext-negative proof passes.
- [ ] Completed bundle validator passes.

## Proof Required

- `proof/SB05/transcripts/ledger-tests-final.txt`
- `proof/SB05/transcripts/ledger-build-final.txt`
- `proof/SB05/transcripts/docker-compose-build.txt`
- `proof/SB05/transcripts/docker-compose-up.txt`
- `proof/SB05/transcripts/docker-compose-ps.txt`
- `proof/SB05/transcripts/docker-smoke.txt`
- `proof/SB05/transcripts/playwright-docker-e2e-messaging.txt`
- `proof/SB05/docker-e2e-messaging-summary.json`
- `proof/SB05/screenshots/*.png`
- `proof/SB05/transcripts/source-assertions.txt`
- `proof/SB05/transcripts/anti-stub-audit.txt`
- `proof/SB05/red-team-closure.md`
- `proof/SB05/changed-file-hashes.json`
- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`

## Browser Validation Logging

- Record each AccountApp URL, large-screen viewport, import/unlock state, send/decrypt actions, attachment actions, network/storage assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Final closure passes only when Docker E2E proof, raw-note closure, proof manifests, red-team closure, and `python scripts/validate_bundle.py --stage completed` all pass.

## Suggested Agent Prompt

Execute SB05. Rebuild Docker, restart services, run real browser Alice/Bob messaging validation both directions with and without attachments, inspect encrypted storage, update closure artifacts, and run the completed validator.
