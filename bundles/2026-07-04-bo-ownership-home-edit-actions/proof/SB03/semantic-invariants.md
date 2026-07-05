# SB03 Semantic Invariants

## SB03-DOCKER-CLOSURE

- Invariant ID: `SB03-DOCKER-CLOSURE`
- Source raw note: `N003` requires real validations and Docker app rebuilds before closure.
- Expected behavior: final closure includes passing targeted tests, passing broader build/test validation, rebuilt Docker app images, seeded Docker app runtime, browser proof against the Docker-served AccountApp, anti-stub audit, and completed-stage bundle validation.
- Disallowed shallow implementation: replacing Docker rebuilds with local-only tests, replacing browser proof with component tests, leaving raw notes pending, or closing without proof manifests fails the invariant.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-bo-details-verifier-present.txt` and `bundle://proof/SB02/transcripts/failing-first-no-bo-edit-update.txt` are the behavior-level failing-first transcripts; SB03 is a process closure subbundle over those implemented behaviors.
- Passing test: `bundle://proof/SB03/transcripts/full-test-project.txt` passed the full test project with 268 tests, and `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt` passed the Docker browser assertions.
- Changed source files: final hashes are recorded in `bundle://proof/SB03/transcripts/changed-file-hashes.txt` for all changed production and test files.
- Production assertions: `bundle://proof/SB03/transcripts/docker-compose-build-node-accountapp.txt`, `bundle://proof/SB03/transcripts/docker-compose-up-accountapp.txt`, `bundle://proof/SB03/transcripts/docker-compose-demo-seeder.txt`, and `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt` prove rebuilt Docker apps were exercised with the Home ownership and BO edit flows.
- Red-team negative case: `bundle://proof/SB03/transcripts/playwright-docker-accountapp-failed-origin-mismatch.txt`, `bundle://proof/SB03/transcripts/playwright-docker-accountapp-failed-visible-text-selector.txt`, and `bundle://proof/SB03/transcripts/playwright-docker-accountapp-failed-dialog-not-closed.txt` show rejected browser attempts were not used as passing proof.
- Downstream dependency check: final raw-note closure in `bundle://reviews/01-execution-report.md` cites SB01, SB02, and SB03 proof artifacts with no pending closure rows.

## Evidence

- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Passing full test transcript: `bundle://proof/SB03/transcripts/full-test-project.txt`
- Passing Docker browser transcript: `bundle://proof/SB03/transcripts/playwright-docker-accountapp.txt`
- Docker rebuild transcript: `bundle://proof/SB03/transcripts/docker-compose-build-node-accountapp.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
