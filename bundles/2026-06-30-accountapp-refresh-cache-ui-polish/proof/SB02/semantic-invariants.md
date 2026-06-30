# SB02 Semantic Invariant Contract

- Invariant ID: SB02-UI-FEEDBACK-WIDTH-001
- Source raw note: RN-03 and RN-04 require transaction feedback to appear only as transient notification UI and the unlocked app to use the full desktop width.
- Expected behavior: Sending a transaction shows a notification popup that disappears automatically, no inline receipt or refresh alert remains in the main app body, and the unlocked AccountApp root spans the 1920px viewport.
- Disallowed shallow implementation: Hiding one alert while leaving another inline receipt path, or changing max-width only on the locked/import screen.
- Failing-first test: N/A process/non-production exemption; the baseline was user-observed persistent inline text and source analysis of persistent alert parameters.
- Passing test: Playwright proof reports `inlineRefreshAlerts=0`, `inlineReceiptLike=0`, `afterDismissMatches=0`, and `rootWidth=1920`.
- Changed source files: repo://src/CanDoItAll.Economy.Components/Components/VenftSsrEconomyApp.razor, repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspaceAdapterHost.razor, repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor, repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css.
- Production assertions: Screenshots and DOM assertions cover the unlocked large desktop app after Alice import and a real submit through the Send dialog.
- Red-team negative case: Accepted transaction text must not still exist in `main` after the popup timeout.
- Downstream dependency check: AccountApp and Components.Demo both build after parameter removal and layout changes.
