# SB02 Semantic Invariants - Desktop account shell and top toolbar polish

## Invariant SB02-INV-01

- Source raw note: R01/R02/R04/R14: remove post-login card stack, show success through notifications, use compact top bar, tooltips, and profile identity region.
- Expected behavior: AccountApp Home.razor renders a compact top frame with avatar/account identity, ledger facts, and icon actions using NotificationService and Tooltip.
- Disallowed shallow implementation: status-only, placeholder-only, fixture-only, or UI-only code that does not exercise production components/services.
- Failing-first or adversarial proof: Original input screenshot shows persistent success/account/action cards pushing the workspace down; final screenshots reject that visual failure.
- Passing proof: bundle://proof/SB07/transcripts/ledger-solution-build.txt; bundle://proof/SB07/transcripts/source-assertions.txt; bundle://inputs/artifacts/accountapp-after-login-current.png; bundle://proof/SB02/screenshots/accountapp-home-after-unlock-large.png; bundle://proof/SB02/screenshots/accountapp-toolbar-tooltips-large.png
- Changed source files and hashes: bundle://proof/SB07/changed-file-hashes.json
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt
- Red-team negative case: bundle://proof/SB07/red-team-verifier.md
- Downstream dependency check: bundle://reviews/01-execution-report.md

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| AccountWorkspaceTopFrame | repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor | repo://examples/CanDoItAll.Economy.AccountApp/Layout/MainLayout.razor | bundle://proof/SB02/screenshots/accountapp-home-after-unlock-large.png | bundle://inputs/artifacts/accountapp-after-login-current.png |
