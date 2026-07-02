# SB07 Semantic Invariants - Large-screen browser visual validation and closure

## Invariant SB07-INV-01

- Source raw note: R15/R16: capture and analyze large-screen visual proof and preserve ledger behavior.
- Expected behavior: Final AccountApp large-screen pass, workbook regeneration, full solution build, source assertions, proof manifest, red-team verifier closure, and Docker compose stack validation.
- Disallowed shallow implementation: status-only, placeholder-only, fixture-only, or UI-only code that does not exercise production components/services.
- Failing-first or adversarial proof: Final verifier checks reject pending rows, missing proof paths, missing manifests, and unfinished-code markers.
- Passing proof: bundle://proof/SB07/transcripts/ledger-solution-build.txt; bundle://proof/SB07/transcripts/workbook-regeneration.txt; bundle://proof/SB07/transcripts/source-assertions.txt; bundle://proof/SB07/screenshots/accountapp-final-home-large.png; bundle://proof/SB07/screenshots/dialogs-final-large.png; bundle://proof/SB07/screenshots/verification-final-large.png; bundle://proof/SB07/transcripts/docker-compose-build.txt; bundle://proof/SB07/transcripts/docker-demo-seeder-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-demo-seeder-repeat-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-compose-smoke-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-accountapp-browser-proof.json; bundle://proof/SB07/screenshots/docker-accountapp-alice-imported-large.png
- Changed source files and hashes: bundle://proof/SB07/changed-file-hashes.json
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt
- Red-team negative case: bundle://proof/SB07/red-team-verifier.md
- Downstream dependency check: bundle://reviews/01-execution-report.md

## Invariant SB07-INV-02

- Source raw note: Follow-up repair request on 2026-07-02: address dialog large-screen width, shared tooltips, profile picture loading, message security tabs, ownership verification card/details flow, top account info, and rebuilt Docker proof.
- Expected behavior: The rebuilt Docker AccountApp shows the compressed top account bar with tooltip-backed icon actions, opens a wide address directory dialog, previews profile image links, splits message security into tabs, verifies a real BO ownership payload, shows a gallery-style BO card, and opens the same BO detail dialog from that card.
- Disallowed shallow implementation: CSS-only or screenshot-only change that leaves the dialog grid narrow, uses native title-only tooltips, omits profile image preview, leaves ownership verification untested, or skips Docker rebuild/recreate.
- Failing-first or adversarial proof: bundle://proof/SB07/transcripts/playwright-ui-repair-validation-cjs.txt recorded the address grid still only `326.34375px` wide before the `Stack AlignItems.Stretch` fix; earlier Playwright attempts also failed when the local module and textarea change propagation were not handled.
- Passing proof: bundle://proof/SB07/transcripts/ledger-solution-build-ui-repair-final.txt; bundle://proof/SB07/transcripts/ledger-tests-ui-repair-final.txt; bundle://proof/SB07/transcripts/docker-compose-build-ui-repair-final.txt; bundle://proof/SB07/transcripts/docker-compose-up-ui-repair-final.txt; bundle://proof/SB07/transcripts/docker-smoke-ui-repair-final.txt; bundle://proof/SB07/transcripts/console-demo-create-bo-ui-repair.txt; bundle://proof/SB07/transcripts/playwright-ui-repair-validation-cjs-final.txt; bundle://proof/SB07/ui-repair-visual-validation.json; bundle://proof/SB07/screenshots/docker-ui-repair-home-top.png; bundle://proof/SB07/screenshots/docker-ui-repair-address-directory-wide.png; bundle://proof/SB07/screenshots/docker-ui-repair-profile-picture-preview.png; bundle://proof/SB07/screenshots/docker-ui-repair-message-security-tabs.png; bundle://proof/SB07/screenshots/docker-ui-repair-ownership-verification.png; bundle://proof/SB07/screenshots/docker-ui-repair-ownership-details-dialog.png
- Changed source files and hashes: bundle://proof/SB07/changed-file-hashes.json; bundle://proof/SB07/ui-repair-changed-file-hashes.json; latest `git status --short` captured in the active Codex transcript.
- Production assertions: bundle://proof/SB07/ui-repair-visual-validation.json records `tooltipVisible=true`, `addressGridWidth=1686`, `ownershipCardVisible=true`, and `ownershipDetailsDialogVisible=true`.
- Red-team negative case: The browser script requires a real `candoitall-bo-ownership-v1` payload from gallery details, then verifies it in the Ownership tab and opens BO details; it fails if the gallery payload, verification card, or details dialog is missing.
- Downstream dependency check: bundle://proof/SB07/transcripts/docker-smoke-ui-repair-final.txt confirms the rebuilt Docker node and AccountApp endpoints are live after recreate.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| BundleClosureProof | bundle://proof/00-evidence-manifest.md | bundle://reviews/01-execution-report.md | bundle://proof/SB07/transcripts/workbook-regeneration.txt | bundle://proof/SB07/red-team-verifier.md |
| DockerAccountAppProof | bundle://proof/SB07/transcripts/docker-compose-build.txt; repo://docker-compose.yml | bundle://proof/SB07/transcripts/docker-accountapp-browser-proof.json | bundle://proof/SB07/transcripts/docker-demo-seeder-repeat-after-treasury-fix.txt; bundle://proof/SB07/transcripts/docker-compose-smoke-after-treasury-fix.txt | bundle://proof/SB07/transcripts/docker-demo-seeder.txt |
| UiRepairProof | bundle://proof/SB07/transcripts/docker-compose-build-ui-repair-final.txt; bundle://proof/SB07/transcripts/console-demo-create-bo-ui-repair.txt | bundle://proof/SB07/ui-repair-visual-validation.json | bundle://proof/SB07/transcripts/docker-smoke-ui-repair-final.txt; bundle://proof/SB07/screenshots/docker-ui-repair-ownership-details-dialog.png | bundle://proof/SB07/transcripts/playwright-ui-repair-validation-cjs.txt |
