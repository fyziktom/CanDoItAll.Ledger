# SB03 Semantic Invariants

## Invariant Contract

- Invariant ID: `MM-PROOF-01`
- Source raw note: User asked not to mint 100 tickets; 5 to 10 is enough.
- Expected behavior: Browser proof mints a small batch of five tickets.
- Disallowed shallow implementation: A proof that mints no real tickets or mints an excessive batch.
- Failing-first test: N/A process exemption; real browser proof is the closure evidence.
- Passing test: `bundle://proof/SB03/transcripts/playwright-passing.txt`.
- Changed source files: `bundle://proof/scripts/ticket-multimint.playwright.mjs` hash `1fad32630900b7161d41de8d91fc4f3f08e856adac5865b3160d605d70842ff9`.
- Production assertions: JSON result records `ticketCount: 5`.
- Red-team negative case: Any result outside 5-10 would fail the script assertions and bundle acceptance checklist.
- Downstream dependency check: Closure report records the five-ticket run.

- Invariant ID: `MM-PROOF-02`
- Source raw note: User asked to validate real instances in the Docker-hosted WASM app.
- Expected behavior: Proof tickets are current Ledger BO holdings for the target recipient.
- Disallowed shallow implementation: DOM-only assertions or manually seeded JSON.
- Failing-first test: N/A process exemption; the proof queries the node API after submission.
- Passing test: `bundle://proof/SB03/transcripts/playwright-passing.txt`.
- Changed source files: `bundle://proof/results/ticket-multimint-result.json` hash `41148ad18640c9a6ba72f2bdf93af68f568b736b81105e6c4761ad421676a542`.
- Production assertions: Five ticket holdings are owned by Bob address `cdia1NTXHdDX3kHtpHwtEFyCFuHPNkqBiV8k4uU`.
- Red-team negative case: Tickets owned by Alice or missing from API would fail the proof script.
- Downstream dependency check: Final closure uses the same JSON and screenshots.

- Invariant ID: `MM-PROOF-03`
- Source raw note: Tickets are related to an event.
- Expected behavior: Proof tickets all reference the selected event BO.
- Disallowed shallow implementation: Batch tickets with no event linkage.
- Failing-first test: N/A process exemption; event linkage is asserted by Playwright after node query.
- Passing test: `bundle://proof/SB03/transcripts/playwright-passing.txt`.
- Changed source files: `bundle://proof/scripts/ticket-multimint.playwright.mjs` hash `1fad32630900b7161d41de8d91fc4f3f08e856adac5865b3160d605d70842ff9`.
- Production assertions: Proof checks event id, owner, date, location, coordinates, and used flag.
- Red-team negative case: Event mismatch would throw from the proof script.
- Downstream dependency check: Ticket event picker screenshot and result JSON agree on the same run.

- Invariant ID: `MM-PROOF-04`
- Source raw note: BO identity must be unique per transaction.
- Expected behavior: Proof tickets have distinct BO ids.
- Disallowed shallow implementation: Reusing one object id for five tickets.
- Failing-first test: N/A process exemption; distinctness is asserted after ledger query.
- Passing test: `bundle://proof/SB03/transcripts/playwright-passing.txt`.
- Changed source files: `bundle://proof/results/ticket-multimint-result.json` hash `41148ad18640c9a6ba72f2bdf93af68f568b736b81105e6c4761ad421676a542`.
- Production assertions: JSON result lists five distinct `bo:ticket:...` ids.
- Red-team negative case: A duplicate id would reduce the distinct-id count and fail the script.
- Downstream dependency check: Closure report lists the same five ids.

- Invariant ID: `MM-PROOF-05`
- Source raw note: Browser proof must include screenshots and ledger/API evidence.
- Expected behavior: Screenshots and JSON/API proof agree on the same run.
- Disallowed shallow implementation: Screenshot-only proof with no ledger state assertions.
- Failing-first test: N/A process exemption; Playwright proof records both screenshots and API result.
- Passing test: `bundle://proof/SB03/transcripts/playwright-passing.txt`.
- Changed source files: `bundle://proof/results/ticket-multimint-result.json` hash `41148ad18640c9a6ba72f2bdf93af68f568b736b81105e6c4761ad421676a542`.
- Production assertions: Screenshots show event picker, setup, and post-submit gallery; JSON shows same run id.
- Red-team negative case: Missing screenshot or API result would fail manifest path validation.
- Downstream dependency check: Final validator checks referenced artifacts exist.
