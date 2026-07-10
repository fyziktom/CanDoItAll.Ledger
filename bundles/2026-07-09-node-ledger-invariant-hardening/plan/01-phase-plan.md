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
| 8 | SB08 | R-001 PostgreSQL migration baseline, existing-schema audit, and adoption | SB07 | New database migration proof; valid legacy adoption; invalid-row fail-closed proof. |
| 9 | SB09 | R-002 version-aware semantic replay and readiness integration | SB08 | V1/V2 rule-set selection; valid history pass; hash-valid semantic-invalid history fails readiness. |
| 10 | SB10 | R-003 protocol-v2 token definitions and classification | SB08-SB09 | V1 golden vectors unchanged; deterministic v2 ID; immutable registry metadata; BO/fungible negatives. |
| 11 | SB11 | R-004 JWT read/write/admin scope authorization | SB08 | Named policies; least-privilege endpoint mapping; missing/wrong scope denied. |
| 12 | SB12 | R-005 NCalc driver isolation | SB07 | NCalc package only in driver project; contract fake and compatibility vectors; composition smoke. |
| 13 | SB13 | Second-wave full proof and reconciliation | SB08-SB12 | Full tests, migration/PostgreSQL proof, package audit, Docker readiness, workbook and review closure. |

## Execution posture

- Gates are evidence checkpoints, not ceremonial blockers.
- A later observation may reopen an earlier subbundle.
- Canonical v1 hash changes are blocked unless separately approved and documented.
- PostgreSQL/Docker infrastructure unavailability is recorded honestly; it does not convert missing proof into a pass.

## Completion

Phases 0-7 completed on 2026-07-09. Phases 8-13 were reopened by explicit user approval and completed on 2026-07-10. They preserve the original evidence and add versioned/operational repairs rather than retroactively changing the v1 closure claim.
