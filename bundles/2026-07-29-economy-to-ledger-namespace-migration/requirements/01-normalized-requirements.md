# Normalized Requirements

| ID | Requirement | Observable acceptance |
| --- | --- | --- |
| R001 | Rename every active `CanDoItAll.Economy.*` project/folder/assembly/root namespace to `CanDoItAll.Ledger.*`. | Project inventory, solution, build output, and identity audit contain the expected Ledger map and no unapproved old identity. |
| R002 | Rename `CanDoItAll.Economy.Ledger` to a non-duplicated responsibility name. | The project is `CanDoItAll.Ledger.Engine`; its public namespace, references, docs, and output assembly agree. |
| R003 | Preserve dependency direction and behavior. | Before/after project-reference topology is isomorphic under the rename map; no cycle appears; full build/tests pass. |
| R004 | Rename active code identifiers and configuration identities associated with the old product prefix. | C#/Razor types/usings, config sections, environment names, service names, images, and active file names use Ledger naming; documented compatibility exceptions are narrow. |
| R005 | Update examples, tools, tests, scripts, and current docs. | All active consumers build and current docs/commands resolve; historical/quarantined evidence remains labeled historical. |
| R006 | Apply relevant SharedInfo standards. | Canonical Compose naming, local Docker validator, shared Docker convention check, .NET solution conventions, documentation contract, and tooling path/safety rules pass or have an explicit local exception. |
| R007 | Mark Simulator as WIP and experimental. | Root README, docs index/architecture guidance, and Simulator README/host docs clearly state it is not production-ready. |
| R008 | Rebuild, restart, and test Dockerized AccountApps. | Compose images rebuild, services restart healthy, node API answers, Alice/Bob AccountApps load through a real browser at `1600x1000`, and no rename-related console/network failures occur. |
| R009 | Preserve durable compatibility where identity is data rather than code. | Persisted format/database/migration identifiers are either deliberately migrated with tests or listed as approved residual exceptions; no existing ignored Docker data is deleted. |
