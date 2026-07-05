# Assumptions And Risks

## Assumptions

- Merge/Split in this request are plain main-currency UTXO operations, not token consolidation and not BusinessObject object-state operations.
- Eligible UTXO means `BusinessObject is null` and `Token is null`; this is the conservative interpretation of "except BOs unique ones" because unique/BO/token outputs have additional semantics.
- The home send dialog target is `examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` and the shared reusable implementation target is `src/CanDoItAll.Economy.Components/Components/Transactions`.
- The large-screen desktop proof target can use a 1600px-or-wider viewport.

## Critical Path Risks

- SB01 is the critical domain foundation. If explicit-input selection is wrong, SB02 UI proof can pass while actual ledger transactions still spend auto-selected or disallowed outputs.
- SB02 is a critical UI foundation. If forms are not reusable or do not bind selected UTXOs reliably, SB03 browser proof cannot prove the requested reusable dialog behavior.
- Transaction details layout can regress only when real many-row details are opened; synthetic screenshots without actual submitted transactions are insufficient.

## Validation Risks

- Docker rebuild/restart and browser proof can be environment-dependent. Any failure must be captured as a transcript and repaired or explicitly blocked.
- The current AccountApp may need seeded or scripted setup to create enough plain UTXOs for merge/split proof.
- Playwright proof must inspect real transaction details, not only dialog DOM.

## Reopen Triggers

- If submitted Merge/Split transactions contain a BO, token, permission, or rule input, reopen SB01.
- If Merge creates more than one output or Split ignores selected UTXOs, reopen SB01.
- If Send/Merge/Split form logic remains embedded in a single non-reusable switch block, reopen SB02.
- If large transaction details clip, overlay, or hide rows after real Merge/Split transactions, reopen SB02 before closing SB03.
