# SB01 Identity Migration Evidence

## Result

- Status: `Pass`
- Active repository-owned projects: 27
- Former Economy-named projects migrated: 21
- Former `CanDoItAll.Economy.Ledger` target:
  `CanDoItAll.Ledger.Engine`
- Normalized `ProjectReference` edges: 79 before and after
- Partial declarations: 14 before and after
- Active Economy-named paths: 0
- Active old namespaces/config identities: 0
- `CanDoItAll.Ledger.Ledger` or `LedgerLedger` collisions: 0

## Behavioral Proof

- `.\scripts\build.ps1`: passed, 0 errors, 113 existing warnings.
- `.\scripts\test.ps1`: passed, 596 passed, 0 failed, 0 skipped.
- Node host, SDK, persistence, examples, Simulator, and AccountApp projects all
  compiled under their Ledger identities.

## Compatibility Residuals

The following values intentionally retain `economy` because they are persisted
wire/storage format identifiers and changing them would break existing backups
or browser data:

- `candoitall-economy-accountapp-vault-backup-v1`
- `candoitall-economy-address-directory-v1`
- `candoitall-economy-tags-v1`
- `candoitall-economy-node-operations-public-v1`
- `candoitall-economy-accountapp-settings-backup-v1`
- `candoitall-economy-account-backup-v1`

The `Economy_` relational table/constraint prefix also remains stable to preserve
existing database compatibility. SB02's repository identity validator owns these
as an exact allowlist; no broad Economy exception is permitted.

## Tooling Gap

CodeAnalytics snapshots
`snap-20260729100803-74c7bb7f-failed` and
`snap-20260729100957-17ef6d18-failed` loaded zero projects because the service
resolved an unrelated SDK configuration. Manual edge comparison, compiler
resolution, and the full test suite are the trusted fallback evidence.
