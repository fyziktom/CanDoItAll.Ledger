# Implementation Prompt

Implement the bundle one subbundle at a time.

- Verify the current subbundle entry gate before editing production files.
- Use `repo://src/CanDoItAll.Economy.Components/Components/Account/EconomyAccountWorkspace.razor` as the main implementation surface.
- For token sources, mirror the production domain predicate in `BusinessObjectAccount.GetTokenSourceOutputsAsync`: `BusinessObject is null`, token present, and token amount greater than `1`.
- Consolidate token-source display into the Balances card without adding another card-like wrapper.
- Keep `examples/CanDoItAll.Economy.AccountApp/Pages/Home.razor` top-frame actions icon-only.
- Show visible short labels only for the in-home action group.
- Add targeted tests before or with the implementation so they would catch unique token sources, BO-backed token sources, missing visible labels, and top-frame regression.
- Capture command transcripts under `proof/SBxx/transcripts/`, then update `reviews/01-execution-report.md`.
- Stop and mark the active subbundle blocked if the proof manifest, source assertions, or browser proof cannot be honestly produced.
