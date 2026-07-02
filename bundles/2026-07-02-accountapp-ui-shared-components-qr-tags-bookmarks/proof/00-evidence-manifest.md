# Evidence Manifest

## Closure State

- Bundle status: `Completed`
- Closure date: `2026-07-02`
- Scope: large-screen AccountApp WASM PWA UI tuning and shared ledger component/service isolation.
- Explicitly deferred: mobile and medium-screen layout tuning.

## Build And Test Proof

| Area | Result | Evidence |
| --- | --- | --- |
| Ledger solution build | `Passed`, 56 existing package warnings, 0 errors | `proof/SB07/transcripts/ledger-solution-build.txt` |
| Account workspace local services | `Passed`, 5 tests | `proof/SB03/transcripts/account-workspace-local-services-tests.txt` |
| Shared QR component build | `Passed`, 0 warnings, 0 errors | `proof/SB01/transcripts/components-qrcode-build.txt` |
| Shared QR component tests | `Passed`, 9 tests | `proof/SB01/transcripts/components-qrcode-tests.txt` |
| Workbook regeneration | `Passed`, formula-error scan found 0 matches | `proof/SB07/transcripts/workbook-regeneration.txt` |
| Source assertions | `Passed`, implementation symbols present, no unfinished-code markers in touched implementation paths | `proof/SB07/transcripts/source-assertions.txt` |
| Completed-stage bundle validator | `Passed` | `proof/SB07/transcripts/bundle-validator-completed.txt` |
| Post-Docker completed-stage bundle validator | `Passed` | `proof/SB07/transcripts/bundle-validator-completed-after-docker.txt` |
| Final server listener check | `Passed`, no listeners on 5291 or 5299 | `proof/SB07/transcripts/server-listeners-final.txt` |

## Docker Compose Proof

| Area | Result | Evidence |
| --- | --- | --- |
| Image rebuild | `Passed`; node, AccountApp, and demo seeder images rebuilt with no-cache/pull | `proof/SB07/transcripts/docker-compose-build.txt` |
| Docker treasury configuration fix | `Passed`; `docker-compose.yml` uses finite `Economy__Treasury__AdminMainAmount=1000000000000` instead of `decimal.MaxValue` to avoid persisted transfer imbalance in the demo ledger | `repo://docker-compose.yml` SHA256 `E31A218198B809FCD64E0555A18BC9E1587A8A9557641AF593663D381B0CED37` |
| Components Docker context trim | `Passed`; sibling components repo has `.dockerignore` for Docker additional context | `repo://../CanDoItAll.Components/.dockerignore` SHA256 `208A2B534D9A61B7C4751E55F1EA9BB281AB3AF28B0F9C2F70393E02C6F94712` |
| Service recreation | `Passed`; Postgres, economy-node, and AccountApp Alice/Bob/Lab recreated after the config fix | `proof/SB07/transcripts/docker-compose-up-after-treasury-fix.txt` |
| Ledger reset | `Passed`; reset through `POST /api/v1/ledger/reset` after node recreation | `proof/SB07/transcripts/docker-ledger-reset-after-treasury-fix.txt` |
| Seed profile | `Passed`; Alice and Bob funded, token source minted, import backups generated | `proof/SB07/transcripts/docker-demo-seeder-after-treasury-fix.txt` |
| Repeat seed profile | `Passed`; second seed run was idempotent and submitted no duplicate funding transactions | `proof/SB07/transcripts/docker-demo-seeder-repeat-after-treasury-fix.txt` |
| HTTP smoke | `Passed`; node health/readiness/state and AccountApp ports 5081/5082/5083 returned 200 | `proof/SB07/transcripts/docker-compose-smoke-after-treasury-fix.txt` |
| Final service status/logs | `Passed`; services up and stable validation window shows successful responses | `proof/SB07/transcripts/docker-compose-ps-final.txt`, `proof/SB07/transcripts/docker-compose-logs-stable-tail.txt` |
| Docker browser validation | `Passed`; Alice backup import reached unlocked workspace with no setup cards, API errors, or console errors | `proof/SB07/transcripts/docker-accountapp-browser-proof.json`, `proof/SB07/screenshots/docker-accountapp-alice-imported-large.png` |

## Artifact-Backed Manifests

| Subbundle | Manifest | Semantic Invariants |
| --- | --- | --- |
| SB01 | `proof/SB01/manifest.md` | `proof/SB01/semantic-invariants.md` |
| SB02 | `proof/SB02/manifest.md` | `proof/SB02/semantic-invariants.md` |
| SB03 | `proof/SB03/manifest.md` | `proof/SB03/semantic-invariants.md` |
| SB04 | `proof/SB04/manifest.md` | `proof/SB04/semantic-invariants.md` |
| SB05 | `proof/SB05/manifest.md` | `proof/SB05/semantic-invariants.md` |
| SB06 | `proof/SB06/manifest.md` | `proof/SB06/semantic-invariants.md` |
| SB07 | `proof/SB07/manifest.md` | `proof/SB07/semantic-invariants.md` |

Red-team verifier: `proof/SB07/red-team-verifier.md`.
Changed-file hashes: `proof/SB07/changed-file-hashes.json`.

## Screenshot Proof

| Subbundle | Scenario | Evidence | Visual Review |
| --- | --- | --- | --- |
| SB01 | Shared QR dialog render | `proof/SB01/screenshots/qr-dialog-large.png` | QR SVG renders at usable size; dialog payload/copy area is visible. |
| SB02 | Home after unlock | `proof/SB02/screenshots/accountapp-home-after-unlock-large.png` | Success state moved to notification; old card stack removed. |
| SB02 | Icon toolbar tooltip | `proof/SB02/screenshots/accountapp-toolbar-tooltips-large.png` | Minimal action buttons expose labels through tooltip service. |
| SB03 | TagEditor suggestions | `proof/SB03/screenshots/tag-editor-suggestions-large.png` | Tag suggestions are shown inside the BO form instead of custom tag UI. |
| SB03 | Persisted tag badges | `proof/SB03/screenshots/tag-badges-after-reload-large.png` | Tags keep readable colored badges after reload. |
| SB04 | Address manager | `proof/SB04/screenshots/address-manager-large.png` | Dialog contains own/external filtering, favorite state, cached profile state, tags, and square-card management. |
| SB04 | Address selection in send flow | `proof/SB04/screenshots/address-select-send-large.png` | Same generic address directory is reused in selection mode. |
| SB05 | Settings backup export | `proof/SB05/screenshots/settings-backup-export-large.png` | Local settings export lives in a dialog and exposes versioned JSON. |
| SB05 | Settings backup import preview | `proof/SB05/screenshots/settings-backup-import-preview-large.png` | Import preview and section selection appear before applying browser-local settings. |
| SB06 | Account/address QR | `proof/SB06/screenshots/address-qr-large.png` | Address can be opened as QR from the workspace. |
| SB06 | Transaction hash QR | `proof/SB06/screenshots/tx-hash-qr-large.png` | Generic QR button handles tx hash payloads. |
| SB06 | Ownership QR | `proof/SB06/screenshots/ownership-qr-large.png` | BO ownership payload can be rendered as QR. |
| SB06 | QR scanner/manual entry | `proof/SB06/screenshots/scan-manual-entry-large.png` | Scanner component exposes manual entry fallback for camera-unavailable environments. |
| SB06 | Ownership verification | `proof/SB06/screenshots/ownership-verification-large.png` | Verification steps show payload format, object identity, owner address, current outpoint, and data hash. |
| SB06 | Ticket signed ownership QR | `proof/SB06/screenshots/ticket-ownership-signed-large.png` | Ticket ownership payload can be signed and rendered. |
| SB06 | Ticket Used state | `proof/SB06/screenshots/ticket-used-confirm-large.png` | Used ticket state is visible and Use Ticket is disabled after confirmation. |
| SB07 | Final AccountApp home | `proof/SB07/screenshots/accountapp-final-home-large.png` | Compact top frame, profile/avatar, facts, icon actions, notification, and dashboard fit large-screen layout. |
| SB07 | Final dialogs overview | `proof/SB07/screenshots/dialogs-final-large.png` | Dialog-first BO details/management flow remains readable. |
| SB07 | Final verification | `proof/SB07/screenshots/verification-final-large.png` | Verification dialog remains readable in final pass. |
| SB07 | Docker AccountApp imported workspace | `proof/SB07/screenshots/docker-accountapp-alice-imported-large.png` | Docker-hosted Alice AccountApp imports the seeded backup, opens the compact large-screen workspace, and shows seeded balances/transactions without the old setup/import card stack. |

## Semantic Invariants Checked

- Account login/unlock success is emitted through `NotificationService`, not a persistent success card stack.
- Main account profile access is in the identity/avatar region.
- Account and workspace action buttons are icon/minimal controls with tooltip labels.
- Tags use `TagEditor`, persisted suggestion counts, and readable persisted badge colors.
- Tag occurrence service supports both general opens and unique BO occurrence semantics.
- Address directory model supports own/external addresses, favorites, user tags, cached profile state, manage mode, and select mode.
- Browser-local AccountApp settings can be exported/imported through a versioned dialog flow.
- QR generation is isolated in `CanDoItAll.Components.QRCode`; ledger payload semantics remain in `CanDoItAll.Economy.Components`.
- QR scanning is exposed through the shared QR project and has manual entry fallback.
- BO ownership and ticket verification are service-driven and expose explicit verification steps.
- Ticket Used state requires explicit confirmation and is shown disabled after use.
- Node/AccountApp proof was run against `default / live` to exercise real ledger data rather than only a demo scenario.
- Docker proof was run against the compose stack on ports 8080, 5081, 5082, and 5083 after image rebuild, node reset, seed, repeat seed, HTTP smoke, and browser import validation.

## Residual Notes

- Build warnings are existing dependency/advisory warnings: `ncalc` compatibility, `MessagePack` advisories, `SQLitePCLRaw.lib.e_sqlite3` advisory, and one `NU1510` pruning warning.
- ZXingBlazor default camera UI includes some upstream localized button text; the implemented manual entry fallback keeps the flow usable when camera access is unavailable.
