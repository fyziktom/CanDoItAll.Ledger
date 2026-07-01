# Implementation Prompt

Implement the ticket multimint bundle in `C:\repositories\CanDoItAll.Ledger`.

Priorities:

- Preserve existing single-ticket minting behavior.
- Add a batch request model and sequential batch submission through `IEconomyAccountWorkspaceAdapter.SubmitTransactionAsync`.
- Add ticket multimint UI with own account, subaccount, address-book, and pasted list modes.
- Generate unique BO ids and ticket-number suffixes per draft.
- Align SSR/real-node create owner handling with WASM.
- Add tests before browser validation.

Do not introduce a second signing path or bypass the node API used by the WASM AccountApp.
