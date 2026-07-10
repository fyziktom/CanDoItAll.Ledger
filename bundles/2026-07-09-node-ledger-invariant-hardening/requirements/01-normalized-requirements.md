# Normalized requirements

| ID | Requirement | Acceptance evidence |
| --- | --- | --- |
| RQ-001 | Treat confirmed transactions and ledger-entry hash chain as canonical; projections remain rebuildable derivatives. | Persistence and verification tests; architecture record. |
| RQ-002 | Reject any output carrying positive main currency and a token simultaneously. | Direct validator test, executor test, Docker/API rejection. |
| RQ-003 | Reject empty spendable outputs and non-positive token outputs. | Shape/preflight negative tests. |
| RQ-004 | Reject unknown, spent, duplicated, or wrong-owner input references. | In-memory, EF, and PostgreSQL tests. |
| RQ-005 | Verify every input signature payload, address/public-key binding, public-key hash, and cryptographic signature. | Direct negative tests and unchanged golden vectors. |
| RQ-006 | Conserve main and each token type for transfer/BO transactions and prevent burns from creating assets. | Conservation matrix tests. |
| RQ-007 | For mint, conserve all pre-existing input assets except explicitly authorized permission consumption; permit only authorized issuance deltas. | Failing-first mint exploit tests and passing policy tests. |
| RQ-008 | Never increase aggregate permission-token supply outside genesis. | Aggregate permission-output negative tests. |
| RQ-009 | Require treasury-controlled input for main issuance and treasury-origin permission for new token/rule issuance. | Token policy tests. |
| RQ-010 | Prevent duplicate token type registration under concurrency. | Store conflict and PostgreSQL concurrency tests. |
| RQ-011 | Require normal BO outputs to carry exactly one token and zero main currency. | Shape tests and Docker BO details. |
| RQ-012 | Preserve BO carrier token ID across update/transfer; an Update keeps owner and a Transfer changes owner. | BO transition negative tests. |
| RQ-013 | Require BO version progression and current-outpoint consumption; reject duplicate current object IDs. | In-memory and EF tests. |
| RQ-014 | Bind BO state hashes to inline/external transaction data where data changes. | Hash-binding tests. |
| RQ-015 | BO close/recycle must preserve or explicitly burn the carrier token according to transaction kind. | Builder/executor merge-close tests. |
| RQ-016 | Perform cheap structural/resource preflight before canonical serialization and hashing. | Oversized/malformed preflight tests and source assertion. |
| RQ-017 | Convert decimal overflow and invalid amount aggregation into deterministic validation errors, not server exceptions. | Maximum-value negative tests. |
| RQ-018 | In-memory confirmation must serialize validation plus append against one state and return conflicts deterministically. | Parallel confirmation tests. |
| RQ-019 | PostgreSQL confirmation must remain atomic and row-locked per ledger/input. | Existing and expanded PostgreSQL concurrency tests. |
| RQ-020 | Add persistence defense-in-depth constraints for non-negative/exclusive/non-empty output values. | Model metadata/SQLite relational tests. |
| RQ-021 | Enforce request-size limits while reading the HTTP body, not only when Content-Length is supplied. | Host options/endpoint integration test. |
| RQ-022 | Preserve canonical v1 hash golden vectors unless an explicit format-version decision is made. | Golden-vector test. |
| RQ-023 | Keep Docker demo insecurity explicit and prevent development defaults from satisfying the production profile. | Options tests and deployment-risk entry. |
| RQ-024 | Run the full suite and large-screen Docker AccountApp proof after code changes. | Transcripts, browser assertions, screenshots. |

