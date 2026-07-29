# Full Build And Docker AccountApp Validation

## Status

- `Completed`

## Objective

- Prove the renamed repository builds/tests cleanly and its Dockerized Node,
  DemoSeeder, and Alice/Bob AccountApps rebuild, restart, connect, and render as
  before.

## Success Criteria

- Clean Release restore/build and full Ledger tests pass.
- Canonical Compose config and local/shared Docker policy checks pass.
- Images rebuild from renamed project paths.
- The renamed local Compose project starts with `--wait`; required services are
  healthy/running.
- Node health/API responds and DemoSeeder can reach the renamed service.
- Alice and Bob AccountApps load at `1600x1000` without rename-related console or
  failed-request errors.
- The legacy project is stopped without `--volumes`; ignored prior data is
  untouched and the requested renamed stack remains running.

## Covered Inputs

- N006, R003, R006, R008, and final proof for R001-R009.

## Prerequisites

- SB01 and SB02 closure gates pass.
- Docker engine and sibling Components/IPFS build contexts are available.
- Required non-secret development environment variables are supplied without
  committing `.env`.

## Exact Source References

- `C:\repositories\CanDoItAll.Ledger\CanDoItAll.Ledger.slnx`
- `C:\repositories\CanDoItAll.Ledger\tests\CanDoItAll.Ledger.Tests`
- `C:\repositories\CanDoItAll.Ledger\compose.yaml`
- `C:\repositories\CanDoItAll.Ledger\docker`
- `C:\repositories\CanDoItAll.Ledger\src\CanDoItAll.Ledger.Node`
- `C:\repositories\CanDoItAll.Ledger\examples\CanDoItAll.Ledger.AccountApp`
- `C:\repositories\CanDoItAll.Ledger\tools\CanDoItAll.Ledger.DemoSeeder`

## UI Composition Contract

- Primary surface: existing AccountApp root; no supporting-content change.
- Stats/list/editor/textarea/dialog decisions: unchanged and outside scope.
- First viewport: `1600x1000`; preserve existing document/component scroll owner.
- Open overlays: not required unless a startup flow opens one or a regression
  requires diagnosis.

## Deliverables

- Exact command/results in the execution report.
- Docker service status/API evidence.
- Alice/Bob Playwright/browser analytics and screenshots if needed.
- Final residual/architecture/bundle closure result.
- Use the SB01/SB02 target paths (`compose.yaml`, `src/CanDoItAll.Ledger.Node`,
  `examples/CanDoItAll.Ledger.AccountApp`, and
  `tools/CanDoItAll.Ledger.DemoSeeder`) after their progression gates pass.

## Dependency Impact

- This is the final dependent-flow proof; a runtime identity/config failure
  reopens SB01 or SB02 rather than becoming residual risk.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: no; final end-to-end closure phase.

## Implementation Steps

1. Run clean identity, restore, Release build, and full tests.
2. Run local/SharedInfo Docker config/policy validation.
3. Rebuild Compose images under the renamed local project.
4. Start/restart with `--wait`; inspect service status and relevant logs.
5. Probe Node health/API and DemoSeeder/service connectivity.
6. Use a real browser for Alice and Bob at `1600x1000`; inspect DOM, console,
   failed requests, first viewport, and scroll ownership.
7. Leave the requested restarted local stack healthy, close raw notes, and run
   final architecture/bundle validators.

## Scope Exceptions

- No mobile/tablet/UI redesign proof.
- No destructive removal or migration of existing ignored `docker/data`.

## Do Not Do

- Do not use a production Compose project/data volume for disposable proof.
- Do not run ordinary teardown with `--volumes`.
- Do not accept image-build success as runtime or browser proof.

## Acceptance Checklist

- [x] Identity and standards checks pass.
- [x] Clean Release build and full tests pass.
- [x] Compose images rebuild.
- [x] Stack restarts healthy under the renamed project name.
- [x] Node health/API and DemoSeeder image build pass.
- [x] Alice AccountApp loads cleanly at `1600x1000`.
- [x] Bob AccountApp loads cleanly at `1600x1000`.
- [x] Legacy teardown is non-destructive and the renamed stack remains healthy.
- [x] Final architecture and bundle gates pass.

## C# Architecture Impact

- Host/composition proof only; no planned C# edit.

## Boundary Ownership

- Node remains composition owner; AccountApps remain browser clients;
  DemoSeeder remains the demo-data producer.

## Dependency Direction

- Validate the SB01 graph; add no edge.

## Pattern Decision

- N/A.

## Testability Contract

- Real Docker producer/consumer paths plus browser behavior; not fixture-only.

## Partial Class Policy

- No source changes expected; reopen SB01 if a source repair is required.

## Architecture Proof Required

- Final dependency/partial/source residual assertion and composition smoke.

## Proof Required

- Semantic positive: healthy renamed Node and real Alice/Bob AccountApp render.
- Shallow-pass trap: renamed source builds locally but Docker still references
  old paths/service DNS/config sections.
- Adversarial negative: zero failed old service/config requests, zero
  rename-related console errors, and final old-identity guard pass.
- Exact build/test/Compose/health commands and results.

## Browser Validation Logging

- Routes: Alice and Bob published AccountApp roots.
- Viewport: `1600x1000`.
- Actions/assertions: navigate, wait for app root, verify meaningful content,
  capture console/page errors and failed requests, inspect first viewport and
  scroll owner.
- Screenshots: capture final states when useful; record exact paths and visual
  findings.

## Progression Gate

- Final closure only when every acceptance item passes, raw notes have evidence,
  and code/bundle/proof status agree.

Decision: `Pass`. Evidence is in
`proof/SB03/full-build-docker-accountapp-evidence.md`.

## Reopen Triggers

- Reopen SB01 for build/assembly/namespace/service/config identity failures.
- Reopen SB02 for Compose/docs/tooling/standards drift.

## Suggested Agent Prompt

```text
Prove the completed rename end to end: clean build/tests, Docker policy/config,
image rebuild and service restart, real Node/DemoSeeder connectivity, and
Alice/Bob browser smoke at 1600x1000. Reopen the owning phase on any rename
regression; do not summarize around missing runtime evidence.
```
