# CanDoItAll.Ledger Docs

This folder contains the active documentation for the Ledger repository.

## Start Here

- `architecture/repository-boundaries.md` defines the Economy/Ledger split.
- `architecture/ledger-runtime.md` summarizes the current runtime modules and dependency direction.
- `architecture/namespace-compatibility.md` records the completed namespace migration and stable persistence compatibility identifiers.
- `operations/local-build-test-audit.md` is the local validation runbook.
- `operations/containers.md` covers the Dockerized node and AccountApps.
- `operations/backup-and-restore.md` defines PostgreSQL volume recovery.
- `migration/import-inventory.md` preserves the import and migration inventory.
- `migration/ledger-simulator-runtime-ownership-inventory.md` records the BO/project simulator landing decision.

## Experimental Simulator

The Ledger BO/project Simulator is work in progress and experimental. Its output
is not production-grade financial, accounting, forecasting, or economic
evidence. See the Simulator project and app READMEs for current limitations.

## Legacy Quarantine

`legacy-economy-ledger-docs/` contains copied Economy-era material. It is retained only as migration evidence and must not be treated as current Ledger product documentation.
