# Full Build, Docker, And AccountApp Evidence

## Release Build And Tests

- `dotnet clean CanDoItAll.Ledger.slnx -c Release -m:1 --nologo
  -v:minimal`: pass.
- `scripts/build.ps1`: pass, 0 errors. The 857 warnings are pre-existing
  dependency and analyzer warnings, primarily from the sibling IPFS source and
  the existing test project.
- The first post-migration test run passed 595 of 596 tests and correctly
  exposed one stale assertion for
  `cdia.economy.golden-vectors.transaction-hashes.v1`.
- After aligning that assertion with the renamed fixture schema
  `cdia.ledger.golden-vectors.transaction-hashes.v1`, the rebuilt test assembly
  and `scripts/test.ps1` passed 596 of 596 tests.

## Identity, Boundary, And Docker Gates

- `tools/validation/Test-LedgerIdentity.ps1 -RunNegativeFixture`: pass; 724
  active files scanned, six exact persisted format identifiers allowed, and the
  negative fixture was detected.
- `scripts/audit-ledger-boundaries.ps1`: pass, including the Ledger repository
  basic-simulator ownership audit and its negative fixture.
- `tools/validation/Test-Docker.ps1 -RunBuildChecks`: pass; 0 policy errors,
  0 warnings, and Compose build checks passed for `node`, `demo-seeder`,
  `accountapp-alice`, `accountapp-bob`, and `accountapp-lab`.

## Rebuild And Restart

- The legacy local project `candoitall-ledger-accountapp-demo` contained
  PostgreSQL, `economy-node`, Alice, Bob, Lab, and a created DemoSeeder.
- `docker compose -p candoitall-ledger-accountapp-demo --env-file .env.example
  down --remove-orphans` removed only that project's containers and network.
  No `--volumes` option or data-directory deletion was used.
- `docker compose --env-file .env.example up -d --build --wait
  --wait-timeout 300` rebuilt and restarted the renamed local stack under
  `candoitall-ledger`.
- `docker compose --profile seed --env-file .env.example build demo-seeder`
  rebuilt the renamed DemoSeeder image.
- Final service state:

| Service | State | Published port |
| --- | --- | --- |
| `postgres` | healthy | internal only |
| `node` | healthy | `127.0.0.1:8080` |
| `accountapp-alice` | healthy | `127.0.0.1:5081` |
| `accountapp-bob` | healthy | `127.0.0.1:5082` |
| `accountapp-lab` | healthy | `127.0.0.1:5083` |

- Node startup logs show both existing EF Core migrations applied, followed by
  `Now listening on: http://[::]:8080` and `Application started`. The initial
  missing `__EFMigrationsHistory` query is EF Core's expected first-database
  probe before creating and populating the history table.
- The stack remains running because the user requested a rebuild and restart,
  not a disposable smoke teardown.

## HTTP And Browser Proof

All of the following returned HTTP 200:

- `http://127.0.0.1:8080/api/v1/health`
- `http://127.0.0.1:5081/`
- `http://127.0.0.1:5081/api/v1/health`
- `http://127.0.0.1:5082/`
- `http://127.0.0.1:5082/api/v1/health`
- `http://127.0.0.1:5083/`

Persistent Playwright browser validation used a `1600x1000` Chromium viewport:

- Alice and Bob both rendered the `CanDoItAll AccountApp` application and their
  instance-specific default names.
- Alice's `Pasted key pair` control changed the import form to the public/private
  key material state.
- Bob's empty backup submission remained safely at import step 1.
- Alice and Bob browser-originated `/api/v1/health` calls both returned 200.
- No request failures, HTTP error responses, console warnings, console errors,
  or page errors were observed.
- Chrome emitted four verbose-only advisories that password fields are not
  inside a form; these are not runtime failures.
- No visible Economy branding remained. Neither viewport had horizontal
  overflow. Bob's initial view fit without scrolling; Alice's denser
  post-interaction form had only 36 pixels of expected document scrolling, with
  its primary action visible.

Screenshots:

- `output/playwright/alice-1600x1000.png`
- `output/playwright/bob-1600x1000.png`

## Architecture Analytics

The final CodeAnalytics retry produced
`snap-20260729120952-38e5e5b2-failed`. The service again loaded zero projects
because its host resolved the unrelated
`C:\repositories\CanDoItAll\global.json`, which requests unavailable SDK
10.0.200. This external tool defect is bounded by the preserved 79-edge manual
graph proof, successful clean solution build, 596 passing tests, identity guard,
and live composition proof.
