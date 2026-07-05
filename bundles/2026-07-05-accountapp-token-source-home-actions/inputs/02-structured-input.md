# Structured Input

## Raw Notes

| ID | Exact wording | Normalized requirement |
| --- | --- | --- |
| N001 | "on home page we have list of tokens sources. It should show just real tokens source and not unique tokens/BOs. Token source is utxo with larger than 1 amount of that token." | Only non-BO token UTXOs with token amount greater than `1` may be shown as token sources on the home page. |
| N002 | "I think we should use just one card for those items. Balances card can show main currecny sources and tokens sources (except unique 1amount tokens). It will save space." | Remove the separate Token Sources card and make the Balances card show main balance plus real token-source UTXOs. |
| N003 | "Also add short labels to buttons that we have in group in home page. The ones in top bar of window must remains with icons only, but on home page we have space for labels for those buttons there. we need large screen only app now." | Show short visible labels on the home workspace action group while leaving the window top-frame action buttons icon-only. |

## Assumptions

- "Home page" means the unlocked AccountApp workspace rendered through `EconomyAccountWorkspace`.
- "Top bar of window" means the `account-app__workspace-frame` toolbar in `Home.razor`, not the in-workspace `cda-vef-account-actions` group.
- Large-screen only means no new mobile-specific design pass is required, but the layout should still avoid obvious broken overflow.

## Validation Expectations

- Add or update component tests for token-source filtering and visible home action labels.
- Preserve existing tests that assert the home security action exists.
- Run the AccountApp project build after changes.
- Capture Playwright proof at a large-screen viewport for the unlocked home page.
