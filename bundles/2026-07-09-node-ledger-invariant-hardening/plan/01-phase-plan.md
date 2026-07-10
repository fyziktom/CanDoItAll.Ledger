# Phase plan

| Phase | Subbundle | Scope | Depends on | Exit evidence |
| --- | --- | --- | --- | --- |
| 0 | SB00 | Audit, baseline, invariant/threat model, findings, workbook | none | 311-test baseline; bundle/workbook created. |
| 1 | SB01 | Preflight and validation-pipeline architecture | SB00 | Failing-first/passing validator tests; executor shrinks; no hash before preflight. |
| 2 | SB02 | Issuance-aware conservation and permission aggregation | SB01 | Mint exploit tests fail first then pass; token policy suite green. |
| 3 | SB03 | BO owner/carrier/data-binding hardening | SB01 | Transition matrix and builder regressions green. |
| 4 | SB04 | In-memory atomic confirmation and relational constraints | SB01-SB03 | Parallel in-memory test; SQLite constraint proof; PostgreSQL concurrency green/explicitly unavailable. |
| 5 | SB05 | Node request/dependency hardening and architecture review | SB01-SB04 | Request limit proof; advisory resolved or explicitly blocked; dependency/partial/testability gate. |
| 6 | SB06 | Full automated and Docker AccountApp large-screen proof | SB01-SB05 | Full tests, Docker build/up, Playwright MCP assertions/screenshots. |
| 7 | SB07 | Final reconciliation and compatibility roadmap | SB06 | Workbook, findings, execution report, residual risks, raw-request closure agree. |

## Execution posture

- Gates are evidence checkpoints, not ceremonial blockers.
- A later observation may reopen an earlier subbundle.
- Canonical v1 hash changes are blocked unless separately approved and documented.
- PostgreSQL/Docker infrastructure unavailability is recorded honestly; it does not convert missing proof into a pass.

## Completion

All phases completed on 2026-07-09. Final evidence is 334/334 automated tests, a clean current dependency-vulnerability scan, rebuilt Docker services, deployed negative probes, and large-screen AccountApp browser proof. The compatibility roadmap in the security/architecture review is intentionally outside the v1 implementation boundary.
