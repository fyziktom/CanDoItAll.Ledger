# SB01 Semantic Invariants

## Invariant Contract

- Invariant ID: `MM-ARCH-01`
- Source raw note: User asked to study `VEBlazor/Components/Minting` and bring multiminting into Ledger AccountApp.
- Expected behavior: The Ledger design preserves VEFramework multimint semantics without copying `AppData.Account` coupling.
- Disallowed shallow implementation: A UI-only clone of `MultimintForm.razor` that bypasses Ledger adapters or hardcodes old account drivers.
- Failing-first test: N/A process exemption; SB01 is non-production architecture mapping.
- Passing test: `bundle://proof/SB01/transcripts/prepared-validator.txt`.
- Changed source files: `bundle://analysis/01-current-state.md` hash `ef997ea99a495d5eaf51bb0fac453a000f7dd44191d4d128425d4ab0d5146942`.
- Production assertions: `bundle://architecture/01-target-solution.md` maps batch drafts to existing adapter boundaries.
- Red-team negative case: Copying Neblio/NFT driver behavior directly is rejected by the architecture notes.
- Downstream dependency check: SB02 consumed this mapping through batch draft and adapter implementation.

- Invariant ID: `MM-ARCH-02`
- Source raw note: User required multiminting for multiple tickets and recipients.
- Expected behavior: Multimint creates independent Ledger BO transaction drafts.
- Disallowed shallow implementation: One transaction or one BO reused for all tickets.
- Failing-first test: N/A process exemption; behavior is proven in SB02 and SB03.
- Passing test: `bundle://proof/SB01/transcripts/prepared-validator.txt`.
- Changed source files: `bundle://architecture/01-target-solution.md` hash `b32f37d5fa054636bc5551e28254816196e5b3d1f6b0eac98df099e9cdad1243`.
- Production assertions: SB02 source assertions prove the production batch model exists.
- Red-team negative case: Reusing one object id is rejected by SB02 tests and SB03 API proof.
- Downstream dependency check: SB03 browser proof created five distinct ticket BOs.

- Invariant ID: `MM-ARCH-03`
- Source raw note: User asked for ticket multiminting for event scenarios.
- Expected behavior: Event linkage remains part of ticket payload semantics.
- Disallowed shallow implementation: Batch tickets that omit event id, event owner, event date, or location fields.
- Failing-first test: N/A process exemption; event linkage is proven in SB03.
- Passing test: `bundle://proof/SB01/transcripts/prepared-validator.txt`.
- Changed source files: `bundle://architecture/01-target-solution.md` hash `b32f37d5fa054636bc5551e28254816196e5b3d1f6b0eac98df099e9cdad1243`.
- Production assertions: `bundle://proof/SB03/transcripts/playwright-passing.txt` validates linked ticket payload fields.
- Red-team negative case: Ticket batches without event linkage would fail SB03 event-field assertions.
- Downstream dependency check: SB03 passed with event selected by transaction hash.
