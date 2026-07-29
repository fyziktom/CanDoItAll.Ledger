# SB02 Documentation, Tooling, And Standards Evidence

## Result

- Status: `Pass`
- `Test-LedgerIdentity.ps1`: 723 active files scanned, six exact persisted
  format IDs allowed, positive repository case passed.
- Negative fixture: old `CanDoItAll.Economy.Node` import and `Economy__`
  configuration prefix both detected.
- Ledger no-basic-Simulator audit: passed, including its adversarial fixture.
- Ledger repository boundary audit: passed.
- Local Docker validation: passed.
- SharedInfo Docker conventions: 0 errors, 0 warnings.
- Compose-aware Dockerfile checks: node, Seeder, and all three AccountApp
  services passed across enabled profiles.

## Standards Changes

- Canonical Compose file is `compose.yaml` with default project
  `candoitall-ledger`.
- Application ports publish on loopback; PostgreSQL is internal-only.
- PostgreSQL and Data Protection state use project-scoped named volumes.
- Required passwords/connection string are externalized through ignored `.env`;
  `.env.example` contains placeholders only.
- Node and AccountApp services have readiness checks and long-form
  `depends_on`; application images run non-root.
- Logs and workstation resource use are bounded; normal teardown preserves
  volumes.
- Repository-owned `tools/validation/Test-Docker.ps1` composes SharedInfo policy
  with additional-context-aware Dockerfile checks.
- The deleted `CanDoItAll/ExternalPackages` build-context dependency was replaced
  by build-time packaging from the declared Components source context.
- Build/test scripts now preserve external tool failures and return a pass
  object on success.

## Documentation And Simulator Status

- Root, active architecture/migration docs, project READMEs, validation runbook,
  container runbook, and backup/restore runbook describe current Ledger names.
- The Simulator is prominently marked work in progress, experimental,
  non-production, and non-authoritative in root/docs/runtime/app sources.
- A visible warning is rendered on the Simulator home page.

## Compatibility Policy

Only the six versioned backup/browser format IDs and the existing relational
`Economy_`/constraint/index names remain. They are frozen persistence contracts,
not active namespaces or examples for new naming.

## Docker Build-Check Exception

The portable SharedInfo `RunBuildChecks` implementation calls plain
`docker build --check` without Compose additional contexts. The Ledger adapter
therefore runs the shared policy checks normally, then runs
`docker compose --profile '*' build --check` so the declared Components context
is present. Both layers pass.
