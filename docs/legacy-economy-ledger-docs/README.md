# Legacy Economy/Ledger Docs Quarantine

This directory is a non-product legacy quarantine.

The files here were copied during the repository split to preserve historical context. They may describe old Economy-era simulator, UI, node, and repository layouts that are not current Ledger product documentation.

Use active Ledger docs instead:

- `../README.md`
- `../architecture/repository-boundaries.md`
- `../architecture/ledger-runtime.md`
- `../architecture/namespace-compatibility.md`
- `../operations/local-build-test-audit.md`

## Governance

Owner: Ledger split maintainer.

Expected benefit: preserve migration evidence while preventing copied simulator docs from becoming active Ledger guidance.

Maintenance cost: each split-hardening pass must confirm active docs do not cite this folder as product truth.

Enforcement cost: the boundary audit allows simulator-specific terms here only because this quarantine marker exists.

Retirement condition: delete or move simulator-specific legacy documents after the active Ledger docs and namespace migration plan are complete.
