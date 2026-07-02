# SB07 Manifest - Large-screen browser visual validation and closure

## Status

- Completed

## Owned Requirements And Raw Notes

- R15/R16: capture and analyze large-screen visual proof and preserve ledger behavior.

## Shipped Behavior

Final AccountApp large-screen pass, workbook regeneration, full solution build, source assertions, proof manifest, red-team verifier closure, and Docker compose validation.

## Artifact Paths

- Semantic invariant contract: bundle://proof/SB07/semantic-invariants.md
- Changed-file hashes: bundle://proof/SB07/changed-file-hashes.json
- Source assertions and anti-stub audit: bundle://proof/SB07/transcripts/source-assertions.txt
- Source proof: repo://bundles/2026-07-02-accountapp-ui-shared-components-qr-tags-bookmarks/reviews/01-execution-report.md; repo://bundles/2026-07-02-accountapp-ui-shared-components-qr-tags-bookmarks/proof/00-evidence-manifest.md
- Test proof: bundle://proof/SB07/transcripts/ledger-solution-build.txt; bundle://proof/SB07/transcripts/workbook-regeneration.txt; bundle://proof/SB07/transcripts/source-assertions.txt
- Browser/visual proof: bundle://proof/SB07/screenshots/accountapp-final-home-large.png; bundle://proof/SB07/screenshots/dialogs-final-large.png; bundle://proof/SB07/screenshots/verification-final-large.png
- Docker proof: bundle://proof/SB07/transcripts/docker-compose-build.txt; bundle://proof/SB07/transcripts/docker-compose-up-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-ledger-reset-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-demo-seeder-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-demo-seeder-repeat-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-compose-smoke-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-accountapp-browser-proof.json; bundle://proof/SB07/screenshots/docker-accountapp-alice-imported-large.png; bundle://proof/SB07/transcripts/bundle-validator-completed-after-docker.txt
- Docker source proof: repo://docker-compose.yml SHA256 E31A218198B809FCD64E0555A18BC9E1587A8A9557641AF593663D381B0CED37; repo://../CanDoItAll.Components/.dockerignore SHA256 208A2B534D9A61B7C4751E55F1EA9BB281AB3AF28B0F9C2F70393E02C6F94712

## 2026-07-02 UI Repair Proof

- Local build: bundle://proof/SB07/transcripts/ledger-solution-build-ui-repair-final.txt
- Targeted tests: bundle://proof/SB07/transcripts/ledger-tests-ui-repair-final.txt
- Focused repair hashes: bundle://proof/SB07/ui-repair-changed-file-hashes.json
- Docker rebuild/recreate: bundle://proof/SB07/transcripts/docker-compose-build-ui-repair-final.txt; bundle://proof/SB07/transcripts/docker-compose-up-ui-repair-final.txt; bundle://proof/SB07/transcripts/docker-compose-ps-ui-repair-final.txt; bundle://proof/SB07/transcripts/docker-smoke-ui-repair-final.txt
- Real BO setup for ownership proof: bundle://proof/SB07/transcripts/console-demo-create-bo-ui-repair.txt
- Browser validation script and summary: bundle://proof/SB07/scripts/visual-validation-ui-repair.cjs; bundle://proof/SB07/transcripts/playwright-ui-repair-validation-cjs-final.txt; bundle://proof/SB07/ui-repair-visual-validation.json
- Large-screen screenshots: bundle://proof/SB07/screenshots/docker-ui-repair-home-top.png; bundle://proof/SB07/screenshots/docker-ui-repair-address-directory-wide.png; bundle://proof/SB07/screenshots/docker-ui-repair-profile-picture-preview.png; bundle://proof/SB07/screenshots/docker-ui-repair-message-security-tabs.png; bundle://proof/SB07/screenshots/docker-ui-repair-ownership-verification.png; bundle://proof/SB07/screenshots/docker-ui-repair-ownership-details-dialog.png
- Spreadsheet checklist and references: bundle://proof/SB07/ui-repair-implementation-checklist.xlsx; bundle://proof/SB07/ui-repair-implementation-checklist-verification.json; bundle://proof/SB07/spreadsheet-previews/summary-preview.png; bundle://proof/SB07/spreadsheet-previews/checklist-preview.png; bundle://proof/SB07/spreadsheet-previews/references-preview.png; bundle://proof/SB07/spreadsheet-previews/validation-preview.png

## 2026-07-02 UI Repair Visual Findings

- Address directory now consumes large-screen modal width; browser assertion measured `1686px` grid width with three cards visible.
- Header action tooltip is visible through the shared action tooltip path, and the bottom account info strip is absent.
- Profile setup loads and previews an image link.
- Message security is split into Signatures, Encryption, and Ownership tabs.
- Ownership verification uses a real Alice-owned BO, passes `candoitall-bo-ownership-v1` verification, renders a gallery-style BO card, and opens the shared BO details dialog from that card.

## Failing-First Or Adversarial Proof

Final verifier checks reject pending rows, missing proof paths, missing manifests, and unfinished-code markers.

## Passing Proof

The shipped behavior is proven by the cited build/test transcripts, source assertions, screenshot artifacts, Docker compose transcripts, repeat seed proof, and Docker browser import proof. The invariant id appears in this contract and the validator verifies the contract path exists.

## Anti-Stub Audit

bundle://proof/SB07/transcripts/source-assertions.txt records implementation symbols and a scoped scan with no TODO, FIXME, HACK, NotImplemented, or stubbed markers in touched implementation paths.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| BundleClosureProof | bundle://proof/00-evidence-manifest.md | bundle://reviews/01-execution-report.md | bundle://proof/SB07/transcripts/workbook-regeneration.txt | bundle://proof/SB07/red-team-verifier.md |
| DockerAccountAppProof | bundle://proof/SB07/transcripts/docker-compose-build.txt; repo://docker-compose.yml | bundle://proof/SB07/transcripts/docker-accountapp-browser-proof.json | bundle://proof/SB07/transcripts/docker-demo-seeder-repeat-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-compose-smoke-after-treasury-fix.txt | bundle://proof/SB07/transcripts/docker-demo-seeder.txt |
| UiRepairProof | bundle://proof/SB07/transcripts/docker-compose-build-ui-repair-final.txt; bundle://proof/SB07/transcripts/console-demo-create-bo-ui-repair.txt | bundle://proof/SB07/ui-repair-visual-validation.json | bundle://proof/SB07/transcripts/docker-smoke-ui-repair-final.txt; bundle://proof/SB07/screenshots/docker-ui-repair-ownership-details-dialog.png | bundle://proof/SB07/transcripts/playwright-ui-repair-validation-cjs-final.txt |
