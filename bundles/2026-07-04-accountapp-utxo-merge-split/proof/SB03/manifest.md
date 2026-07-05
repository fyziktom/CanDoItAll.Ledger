# SB03 Proof Manifest

## Result

- Status: `Pass`
- Scope: final Docker rebuild/restart and large desktop browser proof for real UTXO Split and Merge transactions.

## Docker Proof

- Final rebuild/restart transcript: `bundle://proof/SB03/transcripts/14-docker-compose-rebuild-accountapp-after-outpoint-polish.txt`
- Final compose status transcript: `bundle://proof/SB03/transcripts/14b-docker-compose-ps-after-outpoint-polish.txt`
- Services after final rebuild:
  - Alice AccountApp: host port `5081`
  - Bob AccountApp: host port `5082`
  - Lab AccountApp: host port `5083`
  - Economy node: host port `8080`
  - Postgres: healthy on host port `54329`

## Browser Proof

- Final Playwright transcript: `bundle://proof/SB03/transcripts/16-playwright-accountapp-send-split-merge-final.txt`
- Result JSON: `bundle://proof/SB03/playwright-results.json`
- Viewport: `1920x1080`
- Console errors: none recorded.
- Failing-first proof: N/A process/non-production exemption; SB03 is docker/browser validation over SB01/SB02 production changes, and defects found in proof were repaired and rerun.
- Passing transcript: `bundle://proof/SB03/transcripts/16-playwright-accountapp-send-split-merge-final.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/03-anti-stub-audit-final.txt`
- SHA-256 sample: `841D3868FDDC5332795614D3219275128A8ADF764C528B332F598C467B4A4AB7` for `repo://src/CanDoItAll.Economy.Components/wwwroot/css/economy-components.css`.

## Transaction Proof

- Send transaction: `2e960d76bee085b18a7450fd99e6f63458e00148a554f4316c5f1bb6c9bd3d4f`
  - Inputs: 1
  - Outputs: 2
- Split transaction: `f6783209dc6789805bd5feb0d22bddcdfa84b9c82ba8e1153a63641c2481f9b8`
  - Inputs: 1
  - Outputs: 6
- Merge transaction: `0bc89af693783810fd931c6e069430dbe080ee1de2d75f31b3689e8cc9de4ca9`
  - Inputs: 3
  - Outputs: 1

## Screenshots

- Send form: `bundle://proof/SB03/screenshots/00-send-dialog.png`
- Send transaction details: `bundle://proof/SB03/screenshots/00-send-details-io.png`
- Split form: `bundle://proof/SB03/screenshots/01-split-dialog.png`
- Split transaction details: `bundle://proof/SB03/screenshots/02-split-details-io.png`
- Merge form: `bundle://proof/SB03/screenshots/03-merge-dialog.png`
- Merge transaction details: `bundle://proof/SB03/screenshots/04-merge-details-io.png`

## Regression Tests

- Combined final targeted regression transcript: `bundle://proof/transcripts/06-targeted-regression-tests-final.txt`
- Result: 34 passed, 0 failed.
