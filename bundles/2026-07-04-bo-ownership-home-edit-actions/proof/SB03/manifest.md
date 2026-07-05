# SB03 Proof Manifest

## Scope

- Subbundle: `SB03 docker browser validation closure`
- Invariant: `SB03-DOCKER-CLOSURE`
- Status: `Completed`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed Files

Final post-validation SHA-256 hashes are recorded in `bundle://proof/SB03/transcripts/changed-file-hashes.txt`.

| File | Final SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor` | `8f5d6a1c33efc461219e6117a8e74fa5292ff24b8039d72a2fae2b0ce2bb4bdb` |
| `repo://src/CanDoItAll.Economy.Components/Components/Account/MessageSecurityPanel.razor` | `af0591af3b39e9c96879c1788aaf4a8efe6fd08cf568e2aef712dce05ddd729b` |
| `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCommerceDashboard.razor` | `218476a45e27311cae3ba234cfa4521e6dfd4a594f11185f3a6293223baf2097` |
| `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectCreationWizard.razor` | `52173f2a969d4623298ee3811e7fb93bfe3f4c42b61f4dc26c4647201606ba81` |
| `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerDetailsDialogContent.razor` | `22c86b69286fe6e69fefe76e741ea78b2d799fd856de59b3f0de488de952f4a8` |
| `repo://src/CanDoItAll.Economy.Components/Components/BusinessObjects/BusinessObjectLedgerGallery.razor` | `79d73de9bfdf10a94abd256d67a5d78d20c637036ffca5ce6894ead82fac6bee` |
| `repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor` | `cbd8dd7d49432d5b99e3add4a1ed1ec09f5ce3189f276dc8e8994a468c5a3f4c` |
| `repo://src/CanDoItAll.Economy.Components/Models/BusinessObjectCreationWizardModel.cs` | `5df2b78c022922a7f6e6767e5eb5ea94a6f35e0395ed95ba4d9d3c45b9d1b038` |
| `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectEditUpdateComponentTests.cs` | `5927e7fae4cb73aa740b575a7a685621c60bb2da7d9fb3f0c0fa10e1603f91c1` |
| `repo://tests/CanDoItAll.Ledger.Tests/BusinessObjectOwnershipPlacementComponentTests.cs` | `1448df7a8a7233a1d1994dd43e7388c8caff0d785443d794dbf67b4cf1409299` |

## Evidence

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-bo-details-verifier-present.txt` and `bundle://proof/SB02/transcripts/failing-first-no-bo-edit-update.txt` are the behavior-level negative baselines used by this closure phase.
- Passing transcript: `bundle://proof/SB03/transcripts/full-test-project.txt`
- Passing Docker/browser transcript: `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt`
- Passing Docker rebuild transcript: `bundle://proof/SB03/transcripts/docker-compose-build-node-accountapp.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Browser screenshots: `bundle://proof/SB03/browser/docker-home-security.png` and `bundle://proof/SB03/browser/docker-bo-edit-dialog.png`
- Final red-team artifact: `bundle://proof/SB03/red-team-final.md`

| Evidence | Path | Result |
| --- | --- | --- |
| Targeted component tests | `bundle://proof/SB02/transcripts/passing-tests-edit-update.txt` | `ExitCode: 0`, 4 tests passed |
| Solution build | `bundle://proof/SB03/transcripts/solution-build.txt` | `ExitCode: 0`, build succeeded with warnings only |
| Full test project | `bundle://proof/SB03/transcripts/full-test-project.txt` | `ExitCode: 0`, 268 tests passed |
| Docker availability | `bundle://proof/SB03/transcripts/docker-version.txt` | `ExitCode: 0`, Docker and Compose available |
| Docker image rebuild | `bundle://proof/SB03/transcripts/docker-compose-build-node-accountapp.txt` | `ExitCode: 0`, node and AccountApp images rebuilt |
| Docker app startup | `bundle://proof/SB03/transcripts/docker-compose-up-accountapp.txt` | `ExitCode: 0`, postgres, node, and AccountApp started |
| Demo seeding | `bundle://proof/SB03/transcripts/docker-compose-demo-seeder.txt` | `ExitCode: 0`, seeded Alice/Bob demo data |
| Docker browser proof | `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt` | `ExitCode: 0`, Home verifier and BO edit/update flows passed |
| Anti-stub audit | `bundle://proof/SB03/transcripts/anti-stub-audit.txt` | `ExitCode: 0`, no stub or fake-proof markers |
| Final hashes | `bundle://proof/SB03/transcripts/changed-file-hashes.txt` | `ExitCode: 0`, final hashes recorded |

## Closure Notes

- Docker-served AccountApp was validated on localhost port 5081 with the `view=gallery` query after rebuilding `economy-node` and `accountapp-alice`.
- Browser proof imported the seeded Alice backup, opened Home Security ownership verification, opened BO details Actions, verified owner QR and Edit while rejecting a BO details scanner, opened the edit wizard, changed the BO name, and clicked `Stage BO Update`.
- Earlier failed browser attempts are retained as negative proof and were not used as passing evidence.
