# Execution report

Status: Complete — 2026-07-09

## Outcome

The v1 Node/Ledger implementation now rejects the audited malformed, imbalanced, unauthorized, stale, replayed, and concurrently conflicting transactions in both the EF/PostgreSQL and custom in-memory paths. The validation refactor keeps `LedgerExecutor` as a thin facade, makes policy omission fail closed, and preserves canonical v1 hashes.

| Check | Result |
| --- | --- |
| Baseline | 311 passed, 0 failed, 0 skipped |
| Final full suite | 334 passed, 0 failed, 0 skipped; 1m13s |
| Focused hardening class | 19 passed, 0 failed |
| PostgreSQL concurrency | Passed in full suite |
| NuGet vulnerability audit | No vulnerable packages in the current test-project dependency graph/configured sources |
| Docker | PostgreSQL, rebuilt Node, and Alice/Bob/Lab AccountApps running |
| Node readiness | Sequence 12; chain `valid_payload_only`, zero issues; projection `valid`, zero issues |
| Browser | Large-screen 1600x1000 flows passed; no small/medium viewport work |
| Diff hygiene | `git diff --check` found no whitespace errors; only repository line-ending notices |

Final TRX: `../proof/automated/ledger-hardening-full-final.trx`.

## Subbundle completion

| Subbundle | Status | Evidence |
| --- | --- | --- |
| SB00 Audit/bundle | Complete | Raw request, normalized requirements, threat model, 29 findings, architecture records, and XLSX. |
| SB01 Validation architecture | Complete | Preflight, reference/signature validators, ordered pipeline, safe default composition, direct tests. |
| SB02 Issuance/conservation | Complete | Existing main/normal/permission/rule assets conserved during mint; authorized new deltas remain policy-owned. |
| SB03 BO semantics | Complete | Carrier ID, unit amount, owner/operation, version/current outpoint, and external data hash checks. |
| SB04 Stores/constraints | Complete | In-memory atomic confirmation, parallel conflicts, EF check constraints, SQLite direct-insert proof, PostgreSQL concurrency. |
| SB05 Node/dependencies/review | Complete | Kestrel stream limit, SQLite package upgrade, architecture/security review. |
| SB06 Docker/browser | Complete | Rebuilt stack, signed BO/token/payment flows, split/merge, negative submissions, screenshots. |
| SB07 Closure | Complete | Findings, workbook, traceability, reports, proof, and roadmap reconciled. |

## Implemented architecture

- `TransactionPreflightValidator` rejects resource abuse before canonicalization.
- `InputReferenceValidator` owns unknown/spent/address checks.
- `InputSignatureValidator` owns payload/key/hash/provider verification.
- `LedgerTransactionValidationPipeline` owns deterministic validation order.
- `MainCurrencyIssuancePolicy` separates treasury-authorized main issuance from ordinary Mint conservation and fails closed when treasury is not configured.
- `LedgerExecutor` is the facade for preflight, hashing, atomic context acquisition, pipeline execution, and confirmation envelope construction.
- `InMemoryLedgerStore` now implements `ILedgerConfirmationStore`; its async gate encloses state capture, validation callback, and append.
- `DisabledTokenCreationPolicy` is an explicit simulation opt-out. Omitting a policy uses the fail-closed real policy.
- EF model checks provide defense in depth for output/UTXO amount and asset-shape rules.

No new project reference or dependency inversion was introduced. Core remains independent; Ledger does not depend on persistence or Node; stores do not depend on Ledger.

## Automated commands

```powershell
dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~LedgerHardeningRegressionTests" --logger "console;verbosity=minimal"

dotnet test tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj -c Release --no-restore --logger "trx;LogFileName=ledger-hardening-full-final.trx" --results-directory bundles/2026-07-09-node-ledger-invariant-hardening/proof/automated --logger "console;verbosity=minimal"

dotnet list tests/CanDoItAll.Ledger.Tests/CanDoItAll.Ledger.Tests.csproj package --vulnerable --include-transitive --no-restore

git diff --check
```

The only remaining build warnings are the pre-existing `ncalc` NU1701 compatibility warning and simulator NU1510 pruning suggestion. The earlier SQLite high-severity vulnerability warning is gone.

## Docker and deployed negative proof

The Node and AccountApp images were rebuilt from the hardened sources. The demo ledger was reset/reseeded because the retained development database contained a stale token-registry projection from earlier proof work. Final readiness reports:

- storage: `Npgsql.EntityFrameworkCore.PostgreSQL`;
- sequence: 12;
- chain: `valid_payload_only`, zero issues;
- projection: `valid`, zero issues;
- projection hash: `81cc98344389e39de44d062357ab8028d8ef78c9f414a273e219f5dc58a1302f`.

Deployed probes against the rebuilt Node:

| Probe | Result |
| --- | --- |
| One output with main amount and token amount | Rejected: `invalid_transaction_shape` |
| Replay of four already-spent split outputs | Rejected: four `spent_input` errors |
| Mint without treasury/permission authority | Rejected: `unauthorized_token_creation` |
| Valid JSON, chunked body over configured 1.1 MB boundary | HTTP 413 |
| Supported treasury main issuance to Bob | Accepted at sequence 12; Bob refreshed from 1,000 to 1,001 main |

Details: `../proof/docker/summary.md`.

## Large-screen AccountApp proof

Only 1600x1000 was exercised, as requested. Through the actual rebuilt WASM apps and Node API:

1. Alice created a Post BO from `bo.posts`; the confirmed transaction emitted a one-token/zero-main BO carrier plus token change.
2. Alice transferred that BO to Bob; the next carrier retained the same token ID and amount, changed owner, and carried `Transfer` version semantics.
3. Bob split 1,000 main currency into 200 + 200 + 200 + 400.
4. Bob merged those four UTXOs back into 1,000 at ledger sequence 11.
5. A fresh Bob profile imported and unlocked with one click after the `oninput` binding repair.
6. After the final Node rebuild, Bob refreshed to 1,001 main and six transactions following a treasury-authorized main issuance at sequence 12.

Details and screenshots: `../proof/browser/summary.md`.

## Execution observations

- The first full post-refactor run exposed two SSR/simulation tests that had implicitly relied on missing token policy. Their fixtures now opt out explicitly; production/direct omission remains fail closed.
- Browser proof exposed first-click setup/unlock loss from Blazor `change` binding. Fourteen setup-sensitive fields now use `oninput`, backed by source-contract and browser proof.
- CodeAnalytics could not load the solution because its MCP host requested SDK 10.0.200 while the machine has 10.0.300. Dependency and architecture evidence therefore uses direct project/source inspection plus successful build/test; the failed snapshot is `snap-20260709210242-9c7cb5f2-failed`.

## Residual work and rollout conditions

These are not regressions hidden as passes:

1. Existing relational databases do not receive new EF check constraints through `EnsureCreated`; a reviewed migration/baseline is required before production rollout.
2. Readiness validates payload/hash/admin-signature integrity but not full semantic replay (`valid_payload_only`).
3. Hash-derived immutable token definitions, token fungibility/BO classification, and initial-supply metadata require a versioned protocol change.
4. JWT scopes need authorization-policy enforcement before scope provides security separation.
5. `ncalc` 1.3.8 should be isolated or replaced.
6. The Docker compose profile is intentionally insecure development infrastructure, not production deployment proof.

See `02-security-and-architecture-review.md` for severity and decisions.

## Raw-request closure

| Requested outcome | Closure |
| --- | --- |
| Detailed actual Node/Ledger analysis | Complete: threat model, 29 findings, architecture inventory/review. |
| Rules enforced by Node | Complete for audited v1 invariants; deployed negative probes included. |
| Spent/wrong transaction rejection | Complete: direct, concurrent, PostgreSQL, and deployed replay coverage. |
| Token/permission/BO gaps | Critical/high v1 gaps repaired; compatibility upgrades explicitly proposed. |
| Larger phased bundle and XLSX | Complete: eight subbundles and six-sheet workbook. |
| Good C# architecture/testability | Complete: cohesive validators/pipeline, dependency direction preserved, review gate passed. |
| Implement and test | Complete: 334/334 plus package/Docker/browser proof. |
| AccountApps in Docker with Playwright MCP | Complete at large screen only. |
