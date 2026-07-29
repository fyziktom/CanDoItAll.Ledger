# Documentation, Tooling, And Residual Guard

## Status

- `Completed`

## Objective

- Make current documentation and repository-owned validation describe/enforce
  the implemented Ledger identity and relevant SharedInfo standards, including
  explicit WIP/experimental Simulator status.

## Success Criteria

- Root/project/docs maps and commands resolve to renamed projects.
- Current docs distinguish the active Ledger identity from historical/quarantined
  Economy evidence and the separate Economy repository.
- Simulator is clearly WIP, experimental, and not production-ready.
- `tools/validation/Test-LedgerIdentity.ps1` finds no unapproved active residual
  and fails against a deliberate old-namespace fixture.
- `tools/validation/Test-Docker.ps1` and SharedInfo Docker validation cover the
  actual Compose file.

## Covered Inputs

- N003-N005, R004-R007, and the allowlist/documentation part of R009.

## Prerequisites

- SB01 closure gate passes and the rename map/build/test proof remains trusted.

## Exact Source References

- `C:\repositories\CanDoItAll.Ledger\README.md`
- `C:\repositories\CanDoItAll.Ledger\docs`
- `C:\repositories\CanDoItAll.Ledger\src`
- `C:\repositories\CanDoItAll.Ledger\examples`
- `C:\repositories\CanDoItAll.Ledger\tools`
- `C:\repositories\CanDoItAll.Ledger\scripts`
- `C:\repositories\CanDoItAll.SharedInfo\docs\standards`

## UI Composition Contract

- One status-only warning was added to the Simulator home page so experimental
  limitations are visible at runtime; no layout or interaction redesign.

## Deliverables

- Updated current README/docs/project docs.
- Simulator WIP/experimental notices.
- Portable Ledger identity validator with deliberate negative fixture support.
- Repository-owned Docker validator using the canonical `compose.yaml`.
- Explicit residual-compatibility allowlist/rationale.

## Dependency Impact

- SB03 relies on these commands and docs to select the correct Compose file,
  services, ports, prerequisites, and validation.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: no, but it is a required runtime-validation prerequisite.

## Implementation Steps

1. Update current docs and project maps; keep closed/legacy evidence historical.
2. Add WIP/experimental Simulator statements to root/current Simulator docs.
3. Implement identity validation over tracked active source with narrow
   historical/sibling/persisted exceptions.
4. Prove the validator passes the repository and fails a controlled old
   namespace fixture.
5. Add/run local and SharedInfo Docker convention/config validation.
6. Resolve current-doc path/link/command regressions and update execution proof.

## Scope Exceptions

- No broad repository-layout adoption unrelated to touched identity/Docker/docs.
- Legacy quarantine content remains unchanged but must be labeled from current
  docs as non-current.

## Do Not Do

- Do not weaken shared standards silently.
- Do not hard-code developer profiles/repository roots in executable tools.
- Do not allowlist entire directories when a narrower line/pattern rule works.

## Acceptance Checklist

- [x] Current README/project map/commands use target names.
- [x] Simulator WIP/experimental status is prominent and consistent.
- [x] Identity validator passes active source and catches a negative fixture.
- [x] Current documentation links/paths resolve.
- [x] Docker config/local/shared validations pass.
- [x] Residual exceptions are individually justified.

## C# Architecture Impact

- No production C# architecture change. Documentation and guards assert SB01's
  boundaries.

## Boundary Ownership

- Validation tools live under `tools/validation`; product code stays in owning
  projects; SharedInfo remains read-only.

## Dependency Direction

- No project-reference change.

## Pattern Decision

- Simple PowerShell validation entry points; no compiled abstraction.

## Testability Contract

- Repository positive case plus deliberate negative fixture.

## Partial Class Policy

- N/A; no C# type changes.

## Architecture Proof Required

- Identity validator output and source assertions that it inspects the intended
  active roots/exclusions.

## Proof Required

- Positive validator run.
- Negative old-namespace fixture run.
- SharedInfo Docker convention output and local Docker validator output.
- Current-doc link/path scan and Simulator WIP source assertions.

## Browser Validation Logging

- N/A.

## Progression Gate

- SB03 may start only when current commands/Compose references resolve and both
  identity and Docker policy/config validation pass.

## Reopen Triggers

- Reopen SB01 for a production identity miss; reopen SB02 for stale docs, a weak
  allowlist, broken negative fixture, or runtime instructions inconsistent with
  SB03.

## Suggested Agent Prompt

```text
Update current docs and repository validation to enforce the completed Ledger
identity, SharedInfo standards, and Simulator WIP status. Preserve historical
truth, prove the identity guard with positive and negative cases, and stop if an
exception is broader than its compatibility reason.
```
