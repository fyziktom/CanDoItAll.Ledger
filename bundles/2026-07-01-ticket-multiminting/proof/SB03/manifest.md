# SB03 Proof Manifest

## Result

- Status: Passed.
- Scope: Real Docker-hosted AccountApp browser validation.

## Browser Proof

- Script: `bundle://proof/scripts/ticket-multimint.playwright.mjs`
- Command transcript: `bundle://proof/SB03/transcripts/playwright-passing.txt`
- Passing proof transcript: `bundle://proof/SB03/transcripts/playwright-passing.txt`
- Failing-first proof: N/A process - browser proof was added after implementation; the proof script contains adversarial assertions for missing, duplicate, misowned, or unlinked tickets.
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- JSON result: `bundle://proof/results/ticket-multimint-result.json`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Viewport: `1700x1000`
- Routes: Alice AccountApp localhost port `5081`, views `?view=event`, `?view=ticket`, and `?view=gallery`

## Screenshots

- `bundle://proof/screenshots/ticket-multimint-event-picker-loaded.png`
- `bundle://proof/screenshots/ticket-multimint-setup.png`
- `bundle://proof/screenshots/ticket-multimint-gallery-after-submit.png`

## Ledger Result

- Event object id: `bo:event:multimint-event-1782943690501:8ded7bd48869740-0003-cca269ca3227`
- Event transaction: `c6dfa9cdf8a7d726e156ddab50da0f2b635395d51e34d503dd3c5bd064583d07`
- Ticket owner: `cdia1NTXHdDX3kHtpHwtEFyCFuHPNkqBiV8k4uU`
- Ticket count: `5`
- Ticket numbers: `MM-1782943690501-001` through `MM-1782943690501-005`

## Changed File Hashes

| File | SHA256 |
| --- | --- |
| `bundle://proof/scripts/ticket-multimint.playwright.mjs` | `1fad32630900b7161d41de8d91fc4f3f08e856adac5865b3160d605d70842ff9` |
| `bundle://proof/results/ticket-multimint-result.json` | `41148ad18640c9a6ba72f2bdf93af68f568b736b81105e6c4761ad421676a542` |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Event BO transaction | AccountApp event wizard browser proof | Ticket event picker browser proof | Created and selected by tx hash | Missing event linkage rejected by script assertions |
| Ticket multimint setup | AccountApp ticket wizard screenshot | Batch submit adapter proof | Enabled with 5 recipients to Bob | Wrong count or owner rejected by script assertions |
| Ledger ticket holdings | Node API projection query | Browser proof assertion | Queried after submit by owner/type | Missing, duplicate, or misowned tickets rejected by API assertions |

## Anti-Stub Audit

- Browser proof submitted real AccountApp transactions to the Docker node and verified node API state.
