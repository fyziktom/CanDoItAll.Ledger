# SB01 Semantic Invariants

## Invariant TOPBAR-REMOVAL-01

- Invariant ID: `TOPBAR-REMOVAL-01`
- Source raw note: The screenshot-marked top bar is useless and must be removed.
- Expected behavior: The unlocked AccountApp page renders workspace content without `account-app__workspace-frame`, while the locked page renders a large-screen `account-entry` surface.
- Disallowed shallow implementation: Hiding only a child label, moving the same top bar elsewhere, or leaving stale `account-app__workspace-frame` CSS/markup that can still render.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt` asserts the old frame is present and records the expected non-zero result.
- Passing test: `bundle://proof/SB01/transcripts/source-assertions.txt` checks `TOPBAR-REMOVAL-01` by proving the frame is absent and the account-entry test id exists; `bundle://proof/SB03/transcripts/playwright-large-screen.txt` proves the unlocked browser state also lacks the frame.
- Changed source files: `repo://examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` with hash `61C865F348DDF2664FF05ABF21E5D729B877B47E34E64CA368FE53FFAEA57CA1`; `repo://examples/CanDoItAll.Economy.AccountApp/wwwroot/css/app.css` with hash `63F5275A3BE29114CB9E526B1154FD6213847ECD0AB79C069D7B5C14D8E583D6`.
- Production assertions: `Home.razor` no longer emits the removed frame, `app.css` no longer styles it, and the browser screenshot `bundle://proof/SB03/screenshots/accountapp-unlocked-no-topbar-large.png` captures the real large-screen unlocked page.
- Red-team negative case: A source check that expects the stale frame fails in `bundle://proof/SB01/transcripts/failing-first.txt`.
- Downstream dependency check: SB02 and SB03 can trust the locked account-entry layout because `bundle://proof/SB01/transcripts/accountapp-build.txt` and `bundle://proof/SB03/transcripts/playwright-large-screen.txt` both pass.
