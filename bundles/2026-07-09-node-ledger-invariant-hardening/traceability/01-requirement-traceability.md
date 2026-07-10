# Requirement traceability

The editable execution matrix is `ledger-hardening-checklist.xlsx`. All RQ-001 through RQ-024 rows are complete and link to the proof families below.

| Requirement group | Owner | Production sources | Final proof |
| --- | --- | --- | --- |
| RQ-001 canonical history | SB00/SB06 | canonicalizer, confirmed history, projection verifier | golden vectors; final suite; readiness sequence 11/zero issues |
| RQ-002/RQ-003 output shape/value | SB01/SB06 | `TransactionShapeValidator` | direct regressions; SQLite constraints; deployed mixed-output rejection |
| RQ-004 input state | SB01/SB04/SB06 | `InputReferenceValidator`, confirmation stores | direct matrix; parallel store tests; PostgreSQL tests; deployed spent replay |
| RQ-005 signatures | SB01 | `InputSignatureValidator` | direct missing signature/payload test; existing tamper/provider suite |
| RQ-006-RQ-010 conservation/issuance | SB02/SB04 | conservation validator, main/token issuance policies, stores | exploit regressions; authorized treasury and unauthorized account main issuance; token policy suite; concurrent duplicate-token tests |
| RQ-011-RQ-015 BO semantics | SB03/SB06 | shape and BO transition validators | transition regressions; builder suite; browser create/transfer/carrier proof |
| RQ-016/RQ-017 preflight/overflow | SB01 | preflight and conservation validators | counting hasher remains unused; `amount_overflow` regression |
| RQ-018/RQ-019 atomicity | SB04 | in-memory and PostgreSQL confirmation stores | parallel double-spend/token registration; PostgreSQL concurrency |
| RQ-020 DB constraints | SB04 | EF record configurations | SQLite direct mixed-output and empty-UTXO failures |
| RQ-021 HTTP limit | SB05/SB06 | Kestrel registration and endpoint filter | host configuration test; deployed valid chunked JSON HTTP 413 |
| RQ-022 compatibility | SB06 | unchanged v1 model/canonicalizer | golden-vector and full suite pass |
| RQ-023 profiles | SB05 | Node host options/composition | production fail-closed tests; explicit simulation opt-out |
| RQ-024 end to end | SB06 | rebuilt Node and AccountApps | 334/334; Docker readiness; 1600x1000 signed browser flows/screenshots |

Residual protocol-v2 and rollout work is tracked in `../reviews/02-security-and-architecture-review.md`; it is not required to reinterpret completed v1 rows.
