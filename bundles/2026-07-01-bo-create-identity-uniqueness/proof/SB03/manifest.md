# SB03 Proof Manifest

| Proof | Artifact | Result |
|---|---|---|
| AccountApp build | `proof/SB03/transcripts/01-accountapp-build.txt` | Passed |
| Docker rebuild/recreate | `proof/SB03/transcripts/02-docker-accountapp-rebuild.txt`, `proof/SB03/transcripts/03-docker-accountapp-recreate.txt` | Passed |
| Browser duplicate-name proof | `proof/SB03/transcripts/04-playwright-duplicate-image-name.txt` | Passed |
| Browser screenshot | `proof/SB03/screenshots/alice-duplicate-image-name-gallery.png` | Shows two same-title image BO cards |
| Full test project | `proof/SB03/transcripts/05-full-test-project.txt` | Passed, 226 tests |
| Final Docker validation | `proof/SB03/final-docker-validation.md` | Passed after final SSR create fix; 227 full tests |
| SSR default draft proof | `proof/SB03/scripts/ssr-demo-default-draft.playwright.js`, `proof/SB03/screenshots/ssr-default-draft-unique-messages.png` | Passed; projected exact draft IDs with no timestamp suffix |
