# Node and Ledger Invariant Hardening

Status: Complete — second implementation wave (R-001 through R-005) verified on 2026-07-10

## Objective

Audit and harden the actual single-node UTXO ledger implementation so malformed, unauthorized, imbalanced, stale, or concurrently conflicting transactions cannot be confirmed in either the PostgreSQL or in-memory lanes. Preserve the product model: one non-distributed node, append-only confirmed history, UTXO ownership, token issuance through treasury-derived permission, and token-carried BusinessObjects.

## Outcome contract

This bundle closes only when:

- the implemented protocol invariants are written as executable negative tests;
- validation runs before expensive canonical hashing where possible;
- minting authorizes only issuance deltas and cannot duplicate unrelated input assets or permission supply;
- token/main output separation and BO carrier rules are enforced by the node, not just builders;
- BO updates/transfers preserve carrier identity and express ownership changes honestly;
- in-memory confirmation evaluates and commits against one serialized state;
- EF persistence has provider-independent defense-in-depth constraints for the supported model;
- existing and new unit/integration tests pass;
- PostgreSQL concurrency tests pass when the local test database is available;
- Docker AccountApp large-screen flows and direct negative submissions prove the deployed node behavior;
- the checklist workbook and execution report agree with code and proof.

## Important product boundary

This is a single-node tamper-evident ledger, not a distributed blockchain. No consensus, peer, mempool, mining, reorg, or P2P work is in scope.

## Compatibility-sensitive implementations

Two model upgrades were implemented without reinterpreting the existing v1 transaction format:

1. A token definition whose immutable ID is derived from canonical definition content while retaining a human symbol/namespace.
2. Semantic replay of persisted history in readiness, in addition to the existing hash-chain and projection verification.

The user approved R-001 through R-005 for implementation on 2026-07-10. The second wave adds a production migration/adoption path, version-selected semantic replay, protocol-v2 token definitions without reinterpreting v1 IDs, JWT scope enforcement, and an isolated NCalc driver. The original v1 closure evidence remains immutable historical proof.

## Second-wave outcome contract

- Existing PostgreSQL schemas are audited before a migration baseline is adopted; invalid rows block rollout.
- New PostgreSQL databases are migration-created, and previously `EnsureCreated` schemas can be adopted safely.
- Readiness replays history through a rule set selected by transaction protocol version and reports semantic failures.
- Protocol v2 token IDs derive from canonical immutable definitions while v1 token IDs remain addressable and hash-compatible.
- V2 token classification enforces fungible versus BusinessObject-carrier output semantics at the ledger boundary.
- JWT bearer access requires named read, write, or admin scope; development and API-key behavior remain explicit.
- BusinessObjects no longer references NCalc; a dedicated driver project implements the repository-owned expression contract.

## Bundle map

- `inputs/00-original-request.md`: preserved request and constraints.
- `requirements/01-normalized-requirements.md`: testable requirements.
- `analysis/00-protocol-invariant-and-threat-model.md`: trust boundaries and invariants.
- `analysis/01-current-findings.md`: prioritized actual-code findings.
- `architecture/`: current state, target boundaries, dependency direction, pattern decisions, and testability contract.
- `plan/01-phase-plan.md`: phases/subbundles and dependency order.
- `traceability/ledger-hardening-checklist.xlsx`: editable execution and coverage matrix.
- `reviews/01-execution-report.md`: commands, results, browser proof, and residual risk.

## Baseline

- Branch: `development`
- Working tree at audit start: clean
- Baseline command: `dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj -c Release --no-restore --logger "console;verbosity=minimal"`
- Baseline result: 311 passed, 0 failed, 0 skipped; duration 1m17s
- Baseline warnings: a high-severity advisory for transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, compatibility warning for `ncalc` 1.3.8, and an unnecessary package warning in the simulator project.
- CodeAnalytics attempt: failed/empty snapshot `snap-20260709210242-9c7cb5f2-failed` because the MCP host could not resolve the installed .NET 10.0.300 SDK while requesting 10.0.200. Manual exact-source and project-reference evidence is required for this bundle.

## Final result

- Final suite: 334 passed, 0 failed, 0 skipped; duration 1m13s.
- Newly focused ledger-hardening class: 19 passed, 0 failed.
- Current NuGet audit: no vulnerable packages for the test-project dependency graph and configured sources.
- Docker: PostgreSQL, rebuilt Node, and three rebuilt AccountApps running; readiness at ledger sequence 12 reports zero chain or projection issues.
- Large-screen browser proof: signed token-backed BO create/transfer, main-currency split/merge, account import/unlock, and refresh all passed at 1600x1000.
- Deployed negative proof: mixed main/token output rejected, spent outputs rejected, unauthorized mint rejected, and an oversized chunked JSON request returned HTTP 413.
- The original eight subbundles are complete. SB08-SB13 are the approved second implementation wave and will carry independent proof without rewriting the 2026-07-09 evidence.

Second-wave closure:

- 361 passed, 0 failed, 0 skipped; final TRX is `proof/second-wave/second-wave-full-final-361.trx`.
- Four PostgreSQL migration proofs pass: new database, valid legacy adoption, invalid-row refusal, and concurrent-start serialization.
- The retained Docker database contains both EF migrations and all ten output/UTXO constraints; sequence 13 readiness is `valid_semantic_payload` with zero semantic/projection issues.
- Protocol-v2 token definitions have deterministic hash IDs, immutable registry metadata, explicit classification/divisibility rules, and v1 compatibility coverage.
- JWT read/write/admin policies have direct and live-host allow/deny tests.
- NCalc is owned only by `CanDoItAll.Economy.Rules.NCalc`; BusinessObjects depends only on the repository-owned evaluator contract.
- Rebuilt AccountApp large-screen refresh passed at 1600x1000 with seven successful API responses and no browser errors.
- Targeted Node/PostgreSQL/NCalc/test package audits report no known vulnerable packages. A separate solution-wide audit still reports the pre-existing AppHost MessagePack advisory, recorded as residual scope.

Proof starts at `reviews/01-execution-report.md`; the editable matrix is `traceability/ledger-hardening-checklist.xlsx`.
