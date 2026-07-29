# Source Artifacts

- User request preserved in `inputs/00-original-request.md`.
- Repository state: branch `renaming`, clean before bundle preparation.
- Canonical solution: `CanDoItAll.Ledger.slnx`.
- Current repository docs: `README.md`, `docs/README.md`,
  `docs/architecture/repository-boundaries.md`,
  `docs/architecture/namespace-compatibility.md`, and
  `docs/migration/import-inventory.md`.
- Runtime assets: `docker-compose.yml`, `docker/**/Dockerfile`,
  `scripts/*.ps1`, and the DemoSeeder tool.
- Shared standards source: `C:\repositories\CanDoItAll.SharedInfo`.
- Shared standards used: repository layout, documentation, .NET, Docker,
  tooling, NuGet packaging, and Git.
- CodeAnalytics attempts:
  `snap-20260729100803-74c7bb7f-failed` (solution) and
  `snap-20260729100957-17ef6d18-failed` (Engine project); both loaded zero
  projects because the service resolved an unrelated SDK context.
- Final CodeAnalytics retry:
  `snap-20260729120952-38e5e5b2-failed`; it again loaded zero projects because
  the host resolved `C:\repositories\CanDoItAll\global.json`, which requests the
  unavailable SDK 10.0.200.
