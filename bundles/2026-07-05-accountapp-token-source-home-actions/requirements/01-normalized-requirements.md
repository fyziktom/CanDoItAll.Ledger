# Normalized Requirements

| ID | Requirement | Acceptance evidence | Owning subbundle |
| --- | --- | --- | --- |
| R01 | The AccountApp home page must show only real token sources. A real token source is a spendable UTXO with `BusinessObject is null`, `Token is not null`, and `Token.Amount.Value > 1m`. | Component test and source assertion prove unique one-amount and BO-backed token outputs are excluded. | SB01 |
| R02 | The separate Token Sources card must be removed or collapsed so the Balances card owns main balance and token-source display in one card. | Source assertion and browser screenshot show no standalone Token Sources card and real token sources appear in the Balances card. | SB01, SB02 |
| R03 | Unique one-amount tokens and BO-backed token outputs must not be labeled or displayed as token sources. | Negative component test renders mixed outputs and asserts unique/BO output outpoints are absent. | SB01 |
| R04 | The in-home action button group must show short visible labels for the existing actions. | Component test or DOM/source assertion shows visible labels for Send, Token, Addresses, Security, Settings, Backup, Refresh, and Subaccount. | SB02 |
| R05 | The top window frame action group must remain icon-only. | Source assertion verifies `Home.razor` top-frame actions are unchanged or still do not opt into visible labels. | SB02 |
| R06 | The final UI is large-screen focused and must avoid incoherent overflow or overlapping text at a desktop viewport. | Playwright large-screen screenshot and review questions recorded in the execution report. | SB02 |

## Feedback Closure Matrix

| Raw note | Requirement IDs | Impacted surface | Planned proof | Owning subbundle | Exception status |
| --- | --- | --- | --- | --- | --- |
| N001 | R01, R03 | `EconomyAccountWorkspace` Balances/source area | Failing-first or negative component proof plus production source assertion | SB01 | None |
| N002 | R02 | `EconomyAccountWorkspace` card layout and CSS | Source diff, component test, large-screen browser screenshot | SB01, SB02 | Main source list is limited to existing balance/source data available in `BusinessObjectWorkspaceModel`. |
| N003 | R04, R05, R06 | `cda-vef-account-actions` and `account-app__frame-actions` | Component/source assertions and browser visual proof | SB02 | No mobile-specific redesign required. |
