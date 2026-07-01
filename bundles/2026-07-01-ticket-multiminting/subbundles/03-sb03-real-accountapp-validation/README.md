# SB03 Real AccountApp Validation

## Status

- Completed

## Objective

Validate ticket multiminting in a real running AccountApp with a small batch and capture proof.

## Covered Inputs

- R7 and R8 plus final closure evidence.

## Prerequisites

- SB02 tests pass.
- Docker node and AccountApp containers are running.
- Alice/Bob import files or existing browser vault state are available.

## Exact Source References

- `C:\repositories\CanDoItAll.Ledger\docker-compose.yml`
- `C:\repositories\CanDoItAll.Ledger\examples\CanDoItAll.Economy.AccountApp`

## Deliverables

- Playwright proof script/log.
- Screenshots of multimint setup and completed batch.
- Ledger/API proof showing 5 accepted ticket BOs and owner address.
- Updated execution report.

## Dependency Impact

- Closes the bundle.

## Validation Depth

- Browser interaction must create or select an event, mint tickets, and verify ledger state.
- Batch size must be 5-10, preferably 5.

## Implementation Steps

1. Rebuild/restart if required.
2. Open AccountApp with Playwright.
3. Create/select event.
4. Enable ticket multimint.
5. Resolve recipient target.
6. Submit batch.
7. Query ledger/API for accepted tickets.
8. Capture screenshots and proof manifest.

## Do Not Do

- Do not mint 100 tickets.
- Do not treat screenshots alone as enough proof.

## Acceptance Checklist

- Browser shows multimint UI.
- Submission succeeds.
- Exactly 5 proof tickets are created for the proof run.
- Tickets have distinct BO ids.
- Tickets resolve to the intended owner address.

## Proof Required

- Screenshots under `proof/screenshots/`.
- JSON/log proof under `proof/results/` and normalized transcripts under `proof/SB03/transcripts/`.
- Test transcript in execution report.
- Proof result: `bundle://proof/results/ticket-multimint-result.json`.

## Browser Validation Logging

- Record URL, account alias, batch size, recipient mode, created object ids, and screenshots.

## Progression Gate

- Bundle closure after proof and validator pass.

## Suggested Agent Prompt

Run real browser validation for a 5-ticket multimint batch and capture both UI and ledger proof.
