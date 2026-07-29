# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001 / R001 | `inventories/01-scope-inventory.md` | `01-active-project-and-namespace-identity` | Project/path/namespace scan, solution build/test | Atomic critical foundation. |
| N002 / R002 | `architecture/05-target-solution.md` | `01-active-project-and-namespace-identity` | `Ledger.Engine` assembly/reflection/path assertions | Engine selected over duplicate Core or broad Runtime. |
| N003 / R004-R005 | `requirements/01-normalized-requirements.md` | SB01 and `02-documentation-tooling-and-residual-guard` | Consumer build/tests, doc-link/path scan, identity validator | Historical evidence excluded explicitly. |
| N004 / R006 | `inputs/01-source-artifacts.md` | `02-documentation-tooling-and-residual-guard` | Local and SharedInfo Docker validators; .NET/docs/tool checks | Only this repository is mutable. |
| N005 / R007 | `requirements/01-normalized-requirements.md` | `02-documentation-tooling-and-residual-guard` | README/docs/Simulator source assertions | Must say WIP and experimental/not production-ready. |
| N006 / R003/R008 | `architecture/04-csharp-testability-plan.md` | `03-full-build-docker-accountapp-validation` | Clean build/test, Compose rebuild/restart, health/API and `1600x1000` browser smoke | Must test Alice and Bob AccountApps. |
| R009 | `analysis/02-assumptions-and-risks.md` | SB01/SB02 | Residual allowlist and compatibility tests | Existing ignored Docker data is never deleted. |
