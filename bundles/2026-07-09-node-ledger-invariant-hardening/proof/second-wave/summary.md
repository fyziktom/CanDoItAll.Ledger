# Second-wave proof summary

Date: 2026-07-10

## Automated proof

- Final suite: 361 passed, 0 failed, 0 skipped; 1m23s.
- Restored full solution build: succeeded with 0 errors; warnings are the existing external IPFS backlog, accepted NCalc NU1701, simulator NU1510, and recorded AppHost MessagePack advisories.
- TRX: `second-wave-full-final-361.trx`.
- PostgreSQL migration focus: 4 passed; new, legacy, invalid legacy, and concurrent startup.
- Token-definition focus: 8 passed.
- Targeted package audits: Node, PostgreSQL provider, Rules.NCalc, and tests report no known vulnerable packages.
- `git diff --check`: no whitespace error; only repository line-ending notices.

## PostgreSQL/Docker proof

- Migrations: `20260710114728_InitialLegacySchemaBaseline`, `20260710114940_AdoptedSchemaInvariantUpgrade`.
- Idempotent generated SQL: `postgresql-idempotent-migrations.sql`.
- Retained database migration history contains both rows at EF product version 10.0.4.
- `Economy_LedgerTransactions.TokenDefinitionJson` and token registry protocol/definition/supply/classification columns exist.
- Five `CK_Economy_TxOutput_*` and five `CK_Economy_Utxo_*` constraints exist.
- Rebuilt Node readiness: HTTP 200; PostgreSQL; sequence 13; `valid_semantic_payload`; semantic issue count 0; projection `valid`; projection issue count 0; schema mode `Migrate`.
- Rebuilt AccountApp proxy spendable-output request: HTTP 200.

## Large-screen browser proof

- Viewport: 1600x1000 only.
- Fresh encrypted vault created in rebuilt AccountApp.
- Refresh produced seven successful Node API responses.
- Console errors: 0. Page errors: 0.
- Screenshot: `accountapp-large-refresh.png`.

## Key SHA-256 evidence

| Artifact | SHA-256 |
| --- | --- |
| PostgreSQL migrator source | `0C53F1067FD25D3EACD6DF7AB13367F36969FC25665A82B92DE122F003AD59D7` |
| Semantic replay source | `DE4FD1143CAD1D4CF0ED381011D2FDA637315FEACB25BC5CE355EABC204DE567` |
| Token definitions source | `653E767549281C964CC679F30D35D4C5B732A0CBEB47E9AC927AA05CA75732EE` |
| JWT scope source | `9BF990BB6B88238C9732B3D5952C171B9BD4F1BF921334FBB9B2FE66292461AE` |
| NCalc adapter source | `DCB5240DCDF968209F565D9889C7067F67CEB8611D998F4DD19775EEDE298573` |
| Checklist workbook | `4B6B1AEB1C8ADF2F81CB4E3C5F5195DE245AAB913B6BCA3528459ADF2A4A4279` |
| Final TRX | `F327756B941282A7F72B81D4B6DEB47DE5C379B3E32DF4543340D413191C76AD` |
| Browser screenshot | `947B566AEC2EBB702EAB1D7B305A3E274E69DB170E7AECA777A81C82A7D4F001` |

## Honest residuals

- NCalc 1.3.8 remains and emits NU1701; it is isolated, not replaced.
- Solution-wide audit reports pre-existing vulnerable MessagePack 2.5.192 under the separate AppHost project.
- Development Docker security settings remain demo-only.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| EF migration history + constraints | PostgreSQL schema migrator/migrations | Node startup and database engine | audit → baseline if valid → migrate → verify | invalid legacy rows refuse history; concurrent startup serialized |
| Semantic readiness result | versioned Ledger replay verifier | Node health endpoint/operator | ordered history → isolated replay → combined readiness | hash-valid imbalance and unsupported version fail |
| Registered token definition | confirmed v2 Mint/store projection | later token-output policy and readers | canonical definition → hash ID → confirmation → persistent registry | tampered ID/supply/classification rejected |
| JWT scope authorization result | ASP.NET policy handler | endpoint filter/handler | authenticate → active-token check → named policy → handler | insufficient read/write/admin scope returns 403 |
| Account rule evaluation result | injected evaluator driver | account rule executor | parse/evaluate → repository-owned result → rule orchestration | missing driver fails clearly; fake driver controls result |
