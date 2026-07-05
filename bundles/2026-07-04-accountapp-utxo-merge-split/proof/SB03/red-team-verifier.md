# SB03 Red-Team Verifier

## Checks

- Docker proof is not stale: final rebuild transcript `14-docker-compose-rebuild-accountapp-after-outpoint-polish.txt` completed after the last Razor/CSS change, and `14b-docker-compose-ps-after-outpoint-polish.txt` shows fresh containers.
- Browser proof is not screenshot-only: `playwright-results.json` records Send, Split, and Merge transaction IDs and exact input/output counts.
- Send is covered by the UI proof: final send details show 1 input and 2 outputs.
- Merge cannot be a mislabeled normal send: final merge details show 3 inputs and 1 output.
- Split cannot be a mislabeled normal send: final split details show 1 input and 6 outputs.
- UI layout claim is visually backed by final screenshots `00-send-details-io.png`, `02-split-details-io.png`, and `04-merge-details-io.png`.
- Regressions are covered by final targeted tests: 34 passed in `proof/transcripts/06-targeted-regression-tests-final.txt`.

## Residual Risk

- Package warnings `NU1701`, `NU1903`, and `NU1510` remain pre-existing project dependency warnings and are not introduced by this bundle.
