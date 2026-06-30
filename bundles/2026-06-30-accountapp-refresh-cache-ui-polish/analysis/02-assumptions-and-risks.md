# Assumptions And Risks

## Assumptions

- Polling can be conservative because the user explicitly said transactions are not coming too often.
- The newest-10 transaction fingerprint is sufficient as a cheap account-change probe for this demo app, with full workspace reload used only after a changed probe.
- Transaction details are immutable enough to cache by contextual tx id.

## Critical Path Risks

- If the cheap probe does not include a stable ordering and key fields, the loop could skip a real transaction or reload repeatedly.
- If localStorage cache is tied directly into the adapter instead of an interface, SSR/demo reuse would regress.
- If refresh runs while dialogs submit transactions, duplicate reloads or UI churn could occur.

## Validation Risks

- A test that only asserts a timer exists would not prove changed/unchanged fingerprint behavior.
- A screenshot alone would not prove inline alerts are gone after transaction send; DOM assertions are required.
- Existing browser localStorage may contain an imported Alice account from prior testing. Playwright proof must state whether it imports or uses an existing unlocked vault.

## Reopen Triggers

- Reopen SB01 if SB02 or SB03 shows the account still requires a manual browser refresh to show new transaction state.
- Reopen SB01 if tests prove the cache is global across accounts or scenarios.
- Reopen SB02 if the full-width change applies only to setup screens or leaves the unlocked shell capped.
