# SB03 Validation Docker Closure

## Status

Closed

## Objective

Validate the implemented layout with large-screen screenshots, compare against the generated proposal, rebuild dockered apps, and close the bundle.

## Covered Inputs

- Large-screen screenshots only.
- Analysis against proposal.
- Docker rebuild and test.

## Prerequisites

- SB01 and SB02 completed.
- Focused tests and local build pass.

## Exact Source References

- `repo://docker-compose.yml`
- `repo://docker/accountapp/Dockerfile`
- `repo://docker/node/Dockerfile`
- `repo://examples/CanDoItAll.Economy.AccountApp/CanDoItAll.Economy.AccountApp.csproj`

## Deliverables

- Large-screen screenshot(s) of implemented detail layout.
- Comparison notes against `bundle://evidence/imagegen/bo-detail-layout-proposal.png`.
- Docker build/up proof.
- Final execution report and architecture gate update.

## Dependency Impact

Final closure phase.

## Validation Depth

Critical final browser/docker proof.

## Implementation Steps

1. Run focused tests and local AccountApp build.
2. Run large-screen browser proof and screenshots.
3. Review screenshots using `shared-prompts/qa-prompt.md`.
4. Build docker compose services.
5. Start rebuilt docker AccountApp/node and open route.
6. Update proof and closure files.

## Scope Exceptions

No small/medium viewport validation.

## Do Not Do

- Do not use generated image as shipped proof.
- Do not close if docker app cannot be opened.

## Acceptance Checklist

- [x] Focused tests pass.
- [x] Local build passes.
- [x] Large-screen screenshot shows no empty DataItems hero for no-DataItems BO.
- [x] Screenshot comparison is recorded.
- [x] Docker build/open proof recorded.

## Proof Required

- Command transcripts under `proof/SB03/transcripts`.
- Screenshots under `proof/SB03/screenshots`.
- Final raw-note closure in execution report.

## Browser Validation Logging

Required in `reviews/01-execution-report.md`.

## C# Architecture Impact

Final gate only.

## Boundary Ownership

No new architecture changes expected.

## Dependency Direction

No new references.

## Pattern Decision

N/A.

## Testability Contract

Uses tests from SB01/SB02 plus browser/docker proof.

## Partial Class Policy

No partial classes.

## Architecture Proof Required

Update `reviews/csharp-architecture-gate.md`.

## Progression Gate

Passed. Browser/docker proof and raw-note closure are recorded.

## Closure Proof

- `bundle://evidence/playwright/gallery-sample-large.png`
- `bundle://evidence/playwright/ticket-related-no-data-detail-large.png`
- `bundle://evidence/playwright/docker-accountapp-alice-large.png`
- `bundle://proof/SB03/transcripts/accountapp-build.txt`
- `bundle://proof/SB03/transcripts/docker-compose-build.txt`
- `bundle://proof/SB03/transcripts/docker-compose-up.txt`
- `bundle://proof/SB03/transcripts/docker-compose-ps.txt`
- `bundle://proof/SB03/transcripts/docker-node-probes.txt`

## Suggested Agent Prompt

Execute SB03 only. Capture large-screen browser proof, compare to the image proposal, rebuild dockered apps, and update closure artifacts.
